using System.Collections.Generic;
using System.Linq;
using EvasiveWhisker.Core;
using Fusion;
using UnityEngine;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// File: Assets/Scripts/Networking/ActionResolver.cs
    /// Purpose: Implements ITurnActionResolver — the actual game-state
    /// mutation behind draw/steal/play. Owns the only two pieces of real
    /// hidden state in the whole match: the host-only Deck (Core.Deck) and
    /// the host-only per-player hand contents dictionary. Also owns the
    /// discard pile's hidden portion (everything except the public top card,
    /// which lives on MatchState).
    /// Runs on: Server/Host ONLY. Every public method assumes
    /// Object.HasStateAuthority; TurnStateMachine (the only caller) already
    /// guarantees it only calls these host-side, but each method still checks
    /// defensively since this class is a boundary around real hidden state.
    /// Network responsibility: Never networks hand contents or non-top discard
    /// cards. After every mutation, republishes only the PUBLIC facts the
    /// architecture allows: PlayerState.HandCount, MatchState.DeckCount,
    /// MatchState.DiscardTopCardId/DefinitionId. The one drawn card per turn
    /// is delivered to its owner via a single-target RPC (private hand
    /// delivery) — full hand resync on reconnect is a separate, broader piece
    /// of work for the disconnection-handling step, not duplicated here.
    /// </summary>
    [RequireComponent(typeof(MatchState))]
    [RequireComponent(typeof(WhiskerBlowerTracker))]
    public sealed class ActionResolver : NetworkBehaviour, ITurnActionResolver
    {
        private MatchState _matchState;
        private WhiskerBlowerTracker _whiskerTracker;

        /// <summary>Host-only. Set via Initialize() by match-setup logic (lobby/match-start step, not yet built).</summary>
        private Deck _deck;

        /// <summary>Host-only real hand contents. Never networked, never sent to a client except one card at a time via targeted RPC.</summary>
        private readonly Dictionary<PlayerRef, List<Card>> _hands = new Dictionary<PlayerRef, List<Card>>();

        /// <summary>
        /// Host-only discard pile, full contents. Index 0 = top (public).
        /// MatchState.DiscardTopCardId/DefinitionId always mirrors _discardPile[0].
        /// </summary>
        private readonly List<Card> _discardPile = new List<Card>();

        /// <summary>Host-side only RNG for random steal-card selection — never client-side, same rule as Deck shuffling.</summary>
        private readonly System.Random _rng = new System.Random();

        public override void Spawned()
        {
            _matchState = GetComponent<MatchState>();
            _whiskerTracker = GetComponent<WhiskerBlowerTracker>();
        }

        /// <summary>
        /// Host-only setup call from match-start logic: supplies the shuffled
        /// deck and each player's initial dealt hand. Also registers whichever
        /// player starts holding the whisker blower with the tracker.
        /// </summary>
        public void Initialize(Deck deck, IReadOnlyDictionary<PlayerRef, List<Card>> initialHands, PlayerRef initialWhiskerBlowerHolder)
        {
            if (!Object.HasStateAuthority) return;
            _deck = deck;
            _hands.Clear();
            foreach (var kvp in initialHands)
                _hands[kvp.Key] = new List<Card>(kvp.Value);
            _discardPile.Clear();
            _matchState.DeckCount = _deck.Count;
            _matchState.DiscardTopCardId = -1;
            _matchState.DiscardTopDefinitionId = 0;
            _whiskerTracker.SetHolder(initialWhiskerBlowerHolder);
        }

        // ---------------------------------------------------------------
        // ITurnActionResolver
        // ---------------------------------------------------------------

        public void ResolveDraw(PlayerRef player) => DrawCardInternal(player);

        public void ResolveAutoDraw(PlayerRef player) => DrawCardInternal(player);

        public void ResolveSteal(PlayerRef requester, PlayerRef target, int slotIndex) => StealCardInternal(requester, target, slotIndex);

        public void ResolveAutoSteal(PlayerRef requester, PlayerRef target) => StealCardInternal(requester, target, slotIndex: -1);

        public void ResolvePlayCard(PlayerRef player, int cardId) => PlayCardInternal(player, cardId);

        // ---------------------------------------------------------------
        // Draw
        // ---------------------------------------------------------------

        private void DrawCardInternal(PlayerRef player)
        {
            EnsureDeckHasCards();

            if (_deck.Count == 0)
            {
                // Deck still empty even after attempting a discard reshuffle —
                // unresolved edge case (see ActionResolver Initialize doc /
                // architecture notes). Logging rather than throwing so a live
                // match doesn't hard-crash; the turn will stall here until a
                // rule is defined.
                Debug.LogError($"Cannot draw for {player}: deck is empty and discard pile has no reshuffle-able cards. This edge case needs a rule.");
                return;
            }

            var card = _deck.Draw();
            GetOrCreateHand(player).Add(card);

            var playerState = FindPlayerState(player);
            if (playerState != null) playerState.HandCount = GetOrCreateHand(player).Count;
            _matchState.DeckCount = _deck.Count;

            RpcDeliverDrawnCard(player, card.CardId, (int)card.Type, card.DefinitionId);
        }

        /// <summary>
        /// Deck-empty rule: reshuffle every discard card except the current
        /// public top card back into the deck. The top card stays where it is
        /// — it's the one piece of discard-pile information players can see,
        /// and pulling it back into a hidden shuffled deck would effectively
        /// "erase" public information they already have.
        /// </summary>
        private void EnsureDeckHasCards()
        {
            if (_deck.Count > 0) return;
            if (_discardPile.Count <= 1) return; // nothing (or only the top card) to reshuffle — caller handles the still-empty case

            var topCard = _discardPile[0];
            var toReshuffle = _discardPile.Skip(1).ToList();
            _discardPile.RemoveRange(1, _discardPile.Count - 1);

            _deck.ReturnCardsAndShuffle(toReshuffle);
            _matchState.DeckCount = _deck.Count;
            // Top card / DiscardTop* fields are unchanged — still topCard.
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        private void RpcDeliverDrawnCard([RpcTarget] PlayerRef owner, int cardId, int cardType, int definitionId)
        {
            // Client-side (only runs on the owning client, via [RpcTarget]):
            // hand off to local hand/UI state. Stub — wiring to UI is a later,
            // presentation-layer concern.
        }

        // ---------------------------------------------------------------
        // Steal
        // ---------------------------------------------------------------

        /// <summary>
        /// slotIndex &lt; 0 means "host picks randomly" (the auto-steal timeout
        /// path). slotIndex &gt;= 0 means a player chose that hand position
        /// blind — they know the target's HandCount (public) but not card
        /// contents, so this is a position pick, not a card pick. Bounds are
        /// validated here since it's client-supplied input and never trusted.
        /// </summary>
        private void StealCardInternal(PlayerRef requester, PlayerRef target, int slotIndex)
        {
            var targetHand = GetOrCreateHand(target);
            if (targetHand.Count == 0)
            {
                Debug.LogWarning($"Steal from {target} requested but their hand is empty — ignored.");
                return;
            }

            int index;
            if (slotIndex < 0)
            {
                index = _rng.Next(targetHand.Count); // auto-steal: host picks a random slot
            }
            else if (slotIndex >= targetHand.Count)
            {
                Debug.LogWarning($"Player {requester} requested steal slot {slotIndex} but {target}'s hand only has {targetHand.Count} cards — ignored (stale slot count on client?).");
                return;
            }
            else
            {
                index = slotIndex; // player-chosen slot position, content unknown to them until this resolves
            }

            var card = targetHand[index];
            targetHand.RemoveAt(index);
            GetOrCreateHand(requester).Add(card);

            var targetState = FindPlayerState(target);
            var requesterState = FindPlayerState(requester);
            if (targetState != null) targetState.HandCount = targetHand.Count;
            if (requesterState != null) requesterState.HandCount = GetOrCreateHand(requester).Count;

            if (card.IsWhiskerBlower)
            {
                _whiskerTracker.SetHolder(requester);
            }

            if (card.IsTrap)
            {
                // Trap steal result is public: broadcast who stole what from whom.
                RpcBroadcastTrapSteal(requester, target, card.CardId, card.DefinitionId);
            }
            // Non-trap, non-whisker-blower steals stay private beyond the
            // hand-count change, which is already public via HandCount.
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcBroadcastTrapSteal(PlayerRef requester, PlayerRef target, int cardId, int definitionId)
        {
            // Client-side, all peers: surface "requester stole a trap card
            // (definitionId) from target!" Stub — presentation-layer concern.
        }

        // ---------------------------------------------------------------
        // Play
        // ---------------------------------------------------------------

        private void PlayCardInternal(PlayerRef player, int cardId)
        {
            var hand = GetOrCreateHand(player);
            int index = hand.FindIndex(c => c.CardId == cardId);
            if (index < 0)
            {
                Debug.LogWarning($"Player {player} tried to play card {cardId}, which is not in their hand — ignored.");
                return;
            }

            var card = hand[index];
            hand.RemoveAt(index);

            var playerState = FindPlayerState(player);
            if (playerState != null) playerState.HandCount = hand.Count;

            // Played card identity is always public — becomes the new discard top.
            _discardPile.Insert(0, card);
            _matchState.DiscardTopCardId = card.CardId;
            _matchState.DiscardTopDefinitionId = card.DefinitionId;

            // Card-specific gameplay effects (abilities, including whisker-
            // blower reveal-condition cards) are explicitly out of scope for
            // the networking layer per this engagement's rules. This is the
            // hook a future card-effect system would call into.
            TriggerCardEffectHook(player, card);
        }

        private void TriggerCardEffectHook(PlayerRef player, Card card)
        {
            // Intentionally empty. Left as an extension point.
        }

        // ---------------------------------------------------------------
        // Helpers
        // ---------------------------------------------------------------

        private List<Card> GetOrCreateHand(PlayerRef player)
        {
            if (!_hands.TryGetValue(player, out var hand))
            {
                hand = new List<Card>();
                _hands[player] = hand;
            }
            return hand;
        }

        /// <summary>
        /// Looks up the PlayerState for a given PlayerRef among this
        /// NetworkObject's siblings/scene objects. Placeholder lookup —
        /// expected to be replaced with a proper player-object registry once
        /// the lobby/match-setup script (later step) establishes one.
        /// </summary>
        private PlayerState FindPlayerState(PlayerRef player)
        {
            foreach (var ps in FindObjectsByType<PlayerState>(FindObjectsSortMode.None))
            {
                if (ps.Owner == player) return ps;
            }
            Debug.LogWarning($"No PlayerState found for {player}.");
            return null;
        }
    }
}
