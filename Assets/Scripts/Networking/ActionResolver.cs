using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using EvasiveWhisker.Core;
using EvasiveWhisker.Cards;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Host-only game-state mutation logic. Owns the real hand contents and the discard
    /// pile. Nothing in this class is a NetworkVariable — hidden information never crosses
    /// the network as shared state. Public info (hand counts, discard top card) is pushed out
    /// via PlayerState/MatchState after each mutation.
    ///
    /// Players are identified by Netcode client ID (ulong).
    /// </summary>
    public class ActionResolver : ITurnActionResolver
    {
        private readonly Deck _deck;
        private readonly Dictionary<ulong, List<CardInstance>> _hands = new Dictionary<ulong, List<CardInstance>>();
        private readonly List<CardInstance> _discardPile = new List<CardInstance>(); // last element = public top card
        private readonly System.Random _rng = new System.Random();

        private readonly IReadOnlyDictionary<ulong, PlayerState> _playerStates;
        private readonly MatchState _matchState;
        private readonly WhiskerBlowerTracker _whiskerBlowerTracker;
        private readonly HiddenInfoChannel _hiddenInfoChannel;

        public ActionResolver(
            Deck deck,
            IReadOnlyDictionary<ulong, PlayerState> playerStates,
            MatchState matchState,
            WhiskerBlowerTracker whiskerBlowerTracker,
            HiddenInfoChannel hiddenInfoChannel)
        {
            _deck = deck;
            _playerStates = playerStates;
            _matchState = matchState;
            _whiskerBlowerTracker = whiskerBlowerTracker;
            _hiddenInfoChannel = hiddenInfoChannel;

            foreach (var player in playerStates.Keys)
            {
                _hands[player] = new List<CardInstance>();
            }

            SyncDeckCount();
        }

        public void ResolveDraw(ulong requester)
        {
            if (!_hands.TryGetValue(requester, out var hand)) return;

            var card = DrawWithReshuffleFallback();
            if (card == null) return; // deck + discard both exhausted — see TODO below

            hand.Add(card.Value);
            SyncHandCount(requester);
            SyncDeckCount();

            // Per-turn single-card delivery: only the drawing player learns what they drew.
            _hiddenInfoChannel.DeliverCard(requester, card.Value);
        }

        public void ResolveAutoDraw(ulong currentTurnPlayer)
        {
            ResolveDraw(currentTurnPlayer);
        }

        public void ResolveSteal(ulong requester, ulong target, int slotIndex)
        {
            if (requester == target) return; // can't steal from yourself
            if (!_hands.TryGetValue(requester, out var requesterHand)) return;
            if (!_hands.TryGetValue(target, out var targetHand)) return;
            if (slotIndex < 0 || slotIndex >= targetHand.Count) return; // host-side bounds validation

            var stolenCard = targetHand[slotIndex];
            targetHand.RemoveAt(slotIndex);
            requesterHand.Add(stolenCard);

            // Holder status follows the card: whoever now holds the Whisker Blower is the holder.
            // Done before any delivery so disconnect/reveal logic never sees a stale holder.
            if (stolenCard.Type == CardType.WhiskerBlower)
            {
                _whiskerBlowerTracker.TransferHolder(requester);
            }

            SyncHandCount(requester);
            SyncHandCount(target);

            // Public-reveal trigger #2: stealing a Trap card reveals it to everyone.
            if (stolenCard.Type == CardType.Trap)
            {
                RevealCardPublicly(stolenCard);
            }

            // Per-turn single-card delivery: the requester blind-picked a slot and needs
            // their own local hand state updated with whichever card they actually got.
            // This is separate from the public Trap reveal above — that tells everyone
            // "a Trap was stolen"; this privately tells the requester which literal card
            // is now in their hand.
            _hiddenInfoChannel.DeliverCard(requester, stolenCard);
        }

        public void ResolveAutoSteal(ulong currentTurnPlayer)
        {
            var eligibleTargets = _hands
                .Where(kv => kv.Key != currentTurnPlayer && kv.Value.Count > 0)
                .Select(kv => kv.Key)
                .ToList();

            if (eligibleTargets.Count == 0) return;

            var target = eligibleTargets[_rng.Next(eligibleTargets.Count)];
            int slotIndex = _rng.Next(_hands[target].Count);

            // Auto-steal on timeout is fully host-random for both target and slot.
            ResolveSteal(currentTurnPlayer, target, slotIndex);
        }

        public void ResolvePlayCard(ulong requester, int cardId)
        {
            if (!_hands.TryGetValue(requester, out var hand)) return;

            int index = hand.FindIndex(c => c.Id == cardId);
            if (index < 0) return; // host-side validation: card isn't actually in requester's hand

            var card = hand[index];
            hand.RemoveAt(index);
            SyncHandCount(requester);

            _discardPile.Add(card);
            SyncDiscardTop();

            // Public-reveal trigger #1: playing a card reveals it to everyone.
            RevealCardPublicly(card);

            // NOTE: card-ability *effects* (what a Trap does when triggered, conditional
            // Whisker-Blower reveals granted by specific cards, etc.) are explicitly out of
            // scope for the networking layer — this method only handles the state
            // transition of "card leaves hand, enters discard, becomes publicly known."
            // Hook ability resolution here once ability scripting exists.
        }

        /// <summary>
        /// Host-only: sets every player's starting hand and privately delivers each card to its
        /// owner. Call once at match start, after all clients have loaded the game scene.
        /// </summary>
        public void DealInitialHands(IReadOnlyDictionary<ulong, List<CardInstance>> initialHands)
        {
            foreach (var kv in initialHands)
            {
                if (!_hands.TryGetValue(kv.Key, out var hand)) continue;

                hand.Clear();
                hand.AddRange(kv.Value);
                SyncHandCount(kv.Key);

                foreach (CardInstance card in kv.Value)
                {
                    _hiddenInfoChannel.DeliverCard(kv.Key, card);
                }
            }
        }

        // ---- Deck / discard reshuffle ----

        private CardInstance? DrawWithReshuffleFallback()
        {
            if (_deck.TryDraw(out var card))
            {
                return card;
            }

            // Deck empty: reshuffle the discard pile, excluding the current public top
            // card, back into the deck, then retry the draw.
            if (_discardPile.Count > 1)
            {
                var topCard = _discardPile[_discardPile.Count - 1];
                var toReshuffle = _discardPile.Take(_discardPile.Count - 1).ToList();

                _discardPile.Clear();
                _discardPile.Add(topCard); // public top card stays in place

                _deck.ReturnCardsAndShuffle(toReshuffle);

                if (_deck.TryDraw(out var reshuffledCard))
                {
                    return reshuffledCard;
                }
            }

            // TODO (open edge case — unresolved): deck AND discard pile both cannot supply
            // enough cards to fulfill a draw. Currently logs and stalls; needs a design
            // decision (skip the draw? end the round? something else?).
            Debug.LogError(
                "ActionResolver: draw requested but deck and discard pile are both exhausted.");
            return null;
        }

        // ---- Disconnect handling ----

        /// <summary>
        /// Host-only: forfeits a disconnected non-Whisker-Blower player's hand by
        /// reshuffling their cards back into the deck (not the discard pile).
        /// Called by DisconnectCoordinator; never call this for the Whisker Blower holder
        /// (their disconnect ends the match instead).
        /// </summary>
        public void ForfeitHandOnDisconnect(ulong player)
        {
            if (!_hands.TryGetValue(player, out var hand)) return;

            // Safety net: the Whisker Blower card must never be silently shuffled into the deck.
            // This fires if the tracker says this player isn't the holder but their hand
            // actually contains the card (e.g. the card was stolen and the tracker wasn't updated).
            if (hand.Any(c => c.Type == CardType.WhiskerBlower))
            {
                Debug.LogError(
                    $"ActionResolver: player {player} forfeited while holding the Whisker Blower " +
                    "card, but the tracker doesn't list them as holder. Holder tracking is out of sync.");
            }

            _deck.ReturnCardsAndShuffle(hand);
            hand.Clear();

            SyncHandCount(player);
            SyncDeckCount();
        }

        // ---- Match-end custodian guess resolution ----

        /// <summary>
        /// Host-only: resolves the custodian's end-of-match guess. On a correct guess,
        /// publicly reveals the Whisker Blower holder and every hand (trigger #3). On an
        /// incorrect guess, nothing is revealed.
        /// </summary>
        public bool ResolveCustodianGuess(ulong guessedPlayer)
        {
            bool correct = _whiskerBlowerTracker.HasAssignedHolder &&
                           _whiskerBlowerTracker.GetHolderHostOnly() == guessedPlayer;
            if (correct)
            {
                _whiskerBlowerTracker.RevealToAll();
                RevealAllHands();
            }
            return correct;
        }

        private void RevealAllHands()
        {
            var ids = new List<ulong>();
            var sizes = new List<int>();
            var cards = new List<CardInstance>();

            foreach (var kv in _hands)
            {
                ids.Add(kv.Key);
                sizes.Add(kv.Value.Count);
                cards.AddRange(kv.Value);
            }

            _hiddenInfoChannel.BroadcastAllHands(ids.ToArray(), sizes.ToArray(), cards.ToArray());
        }

        // ---- Sync helpers (push public info to networked state) ----

        private void SyncHandCount(ulong player)
        {
            if (_playerStates.TryGetValue(player, out var state))
            {
                state.HandCount.Value = _hands[player].Count;
            }
        }

        private void SyncDeckCount()
        {
            _matchState.DeckCount.Value = _deck.Count;
        }

        private void SyncDiscardTop()
        {
            var top = _discardPile[_discardPile.Count - 1];
            _matchState.DiscardTopCardId.Value = top.Id;
            _matchState.DiscardTopDefinitionId.Value = top.DefinitionId;
        }

        private void RevealCardPublicly(CardInstance card)
        {
            // A card's *type* becoming public knowledge (e.g. "a Trap was just stolen") is
            // a UI/ability concern kept out of scope here beyond updating discard-top
            // visibility for played cards. This is left as an explicit hook point for
            // whatever event system drives client-side reveal UI.
        }
    }
}
