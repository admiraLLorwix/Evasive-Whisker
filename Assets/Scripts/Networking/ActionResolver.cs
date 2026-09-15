using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Fusion;
using EvasiveWhisker.Core;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Host-only game-state mutation logic. Owns the real hand contents and the discard
    /// pile. Nothing in this class is [Networked] — hidden information never crosses the
    /// network as shared state. Public info (hand counts, discard top card) is pushed out
    /// via PlayerState/MatchState after each mutation.
    /// </summary>
    public class ActionResolver : ITurnActionResolver
    {
        private readonly Deck _deck;
        private readonly Dictionary<PlayerRef, List<Card>> _hands = new Dictionary<PlayerRef, List<Card>>();
        private readonly List<Card> _discardPile = new List<Card>(); // last element = public top card

        private readonly Dictionary<PlayerRef, PlayerState> _playerStates;
        private readonly MatchState _matchState;
        private readonly WhiskerBlowerTracker _whiskerBlowerTracker;
        private readonly HiddenInfoChannel _hiddenInfoChannel;

        public ActionResolver(
            Deck deck,
            Dictionary<PlayerRef, PlayerState> playerStates,
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
                _hands[player] = new List<Card>();
            }

            SyncDeckCount();
        }

        public void ResolveDraw(PlayerRef requester)
        {
            var card = DrawWithReshuffleFallback();
            if (card == null) return; // deck + discard both exhausted — see TODO below

            _hands[requester].Add(card.Value);
            SyncHandCount(requester);
            SyncDeckCount();

            // Per-turn single-card delivery: only the drawing player learns what they drew.
            _hiddenInfoChannel.DeliverCard(requester, card.Value);
        }

        public void ResolveAutoDraw(PlayerRef currentTurnPlayer)
        {
            ResolveDraw(currentTurnPlayer);
        }

        public void ResolveSteal(PlayerRef requester, PlayerRef target, int slotIndex)
        {
            if (!_hands.TryGetValue(target, out var targetHand)) return;
            if (slotIndex < 0 || slotIndex >= targetHand.Count) return; // host-side bounds validation

            var stolenCard = targetHand[slotIndex];
            targetHand.RemoveAt(slotIndex);
            _hands[requester].Add(stolenCard);

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

        public void ResolveAutoSteal(PlayerRef currentTurnPlayer)
        {
            var eligibleTargets = _hands
                .Where(kv => kv.Key != currentTurnPlayer && kv.Value.Count > 0)
                .Select(kv => kv.Key)
                .ToList();

            if (eligibleTargets.Count == 0) return;

            var rng = new System.Random();
            var target = eligibleTargets[rng.Next(eligibleTargets.Count)];
            int slotIndex = rng.Next(_hands[target].Count);

            // Auto-steal on timeout is fully host-random for both target and slot.
            ResolveSteal(currentTurnPlayer, target, slotIndex);
        }

        public void ResolvePlayCard(PlayerRef requester, int cardId)
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

        // ---- Deck / discard reshuffle ----

        private Card? DrawWithReshuffleFallback()
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
            // decision (skip the draw? end the round? something else?) per the open
            // question in the project state summary.
            Debug.LogError(
                "ActionResolver: draw requested but deck and discard pile are both exhausted.");
            return null;
        }

        // ---- Disconnect handling ----

        /// <summary>
        /// Host-only: forfeits a disconnected non-Whisker-Blower player's hand by
        /// reshuffling their cards back into the deck (not the discard pile).
        /// </summary>
        public void ForfeitHandOnDisconnect(PlayerRef player)
        {
            if (!_hands.TryGetValue(player, out var hand)) return;

            _deck.ReturnCardsAndShuffle(hand);
            hand.Clear();

            SyncHandCount(player);
            SyncDeckCount();
        }

        // ---- Reconnection handling (Item 5 + Item 6 trigger) ----

        /// <summary>
        /// Host-only: call this once a returning client has been re-associated with its
        /// existing PlayerRef/PlayerState. Marks the player connected again and pushes
        /// their full hand back down in one resync push, correcting any local state the
        /// client lost while disconnected.
        ///
        /// This method assumes identity re-association has already happened — matching a
        /// reconnecting client back to its original PlayerRef is a Fusion session/lobby
        /// concern (Photon reconnection tokens, or reserved player slots) that isn't wired
        /// up yet. Call this from wherever that detection ends up living — likely a
        /// connection-manager component built as part of Item 6/Item 8 — once it fires.
        /// </summary>
        public void HandlePlayerReconnected(PlayerRef player)
        {
            if (!_hands.TryGetValue(player, out var hand)) return;

            if (_playerStates.TryGetValue(player, out var state))
            {
                state.ConfirmReconnected();
            }

            _hiddenInfoChannel.DeliverFullHandResync(player, hand);

            // NOTE (open question, not yet resolved): if this player had previously been
            // granted a conditional Whisker Blower reveal before disconnecting
            // (WhiskerBlowerTracker.DoesPlayerKnow(player) == true), the host still has
            // that record, but nothing currently re-sends it here. Worth confirming
            // whether a reconnecting player who used to know the holder's identity should
            // have that knowledge re-pushed as part of this same resync, or whether it's
            // acceptable for them to have lost it client-side until the next reveal event.
        }

        // ---- Match-end custodian guess resolution ----

        /// <summary>
        /// Host-only: resolves the custodian's end-of-match guess. On a correct guess,
        /// triggers the public reveal of the Whisker Blower holder (trigger #3). On an
        /// incorrect guess, nothing is revealed.
        /// </summary>
        public bool ResolveCustodianGuess(PlayerRef guessedPlayer)
        {
            bool correct = _whiskerBlowerTracker.GetHolderHostOnly() == guessedPlayer;
            if (correct)
            {
                _whiskerBlowerTracker.RevealToAll();
                // TODO (Item 5/6): the "reveal all hands" half of trigger #3 needs its own
                // broadcast channel — likely the same hidden-info delivery channel being
                // built for reconnection hand resync.
            }
            return correct;
        }

        // ---- Sync helpers (push public info to networked state) ----

        private void SyncHandCount(PlayerRef player)
        {
            if (_playerStates.TryGetValue(player, out var state))
            {
                state.HandCount = _hands[player].Count;
            }
        }

        private void SyncDeckCount()
        {
            _matchState.DeckCount = _deck.Count;
        }

        private void SyncDiscardTop()
        {
            var top = _discardPile[_discardPile.Count - 1];
            _matchState.DiscardTopCardId = top.Id;
            _matchState.DiscardTopDefinitionId = top.DefinitionId;
        }

        private void RevealCardPublicly(Card card)
        {
            // A card's *type* becoming public knowledge (e.g. "a Trap was just stolen") is
            // a UI/ability concern kept out of scope here beyond updating discard-top
            // visibility for played cards. This is left as an explicit hook point for
            // whatever event system drives client-side reveal UI.
        }
    }
}
