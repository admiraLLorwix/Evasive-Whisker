using System;
using System.Collections.Generic;
using System.Linq;

namespace EvasiveWhisker.Core
{
    /// <summary>
    /// File: Assets/Scripts/Core/Deck.cs
    /// Purpose: Host-authoritative deck: holds the actual ordered card list,
    /// exposes draw/shuffle/reconstruct operations. This is the single source
    /// of truth for "what cards exist and where are they."
    /// Runs on: Server/Host ONLY. This class must never be instantiated or
    /// touched on a client with real data — clients only ever see DeckCount
    /// (an int) via MatchState's networked property, never this object.
    /// Network responsibility: None itself — it is deliberately NOT a
    /// NetworkBehaviour and holds no networked properties. Deck order and
    /// contents are exactly the kind of hidden information the architecture
    /// forbids from being networked as shared state. Callers (the turn/action
    /// resolution scripts) are responsible for publishing only Deck.Count
    /// through MatchState after any mutating operation.
    ///
    /// This class also owns deck reconstruction for host migration: given the
    /// full known card set minus every card whose location is now known
    /// (reported hands + discard pile + any revealed cards), it rebuilds a
    /// fresh, reshuffled remainder. Original draw order is intentionally not
    /// recoverable — only fairness/hiddenness of what's left is preserved.
    /// </summary>
    public sealed class Deck
    {
        private readonly List<Card> _cards; // Top of deck = index 0 for draw simplicity.
        private readonly Random _rng;

        public int Count => _cards.Count;

        /// <summary>
        /// Standard construction: build the full card set from a definition table
        /// and shuffle it. Used once, at match start.
        /// </summary>
        public Deck(IReadOnlyList<Card> fullCardSet, int seed)
        {
            _rng = new Random(seed);
            _cards = new List<Card>(fullCardSet);
            Shuffle();
        }

        /// <summary>
        /// Reconstruction path for host migration. `fullCardSet` is the complete,
        /// fixed definition of every card that exists in this match (design-time
        /// data, not runtime state, so it's safe for the new host to rebuild from
        /// scratch). `knownCards` is the union of all cards whose location the
        /// new host has confirmed: every reported hand (via ReportMyHand RPCs),
        /// the discard pile, and any cards already publicly revealed. Whatever
        /// remains is, by construction, exactly the unseen deck remainder —
        /// it's reshuffled fresh since true original order can't be recovered.
        /// </summary>
        public Deck(IReadOnlyList<Card> fullCardSet, IEnumerable<Card> knownCards, int seed)
        {
            _rng = new Random(seed);
            var known = new HashSet<int>(knownCards.Select(c => c.CardId));
            _cards = fullCardSet.Where(c => !known.Contains(c.CardId)).ToList();
            Shuffle();
        }

        /// <summary>
        /// Host-side Fisher-Yates shuffle. Never called on a client — this is
        /// exactly the kind of RNG the architecture requires to stay server-side.
        /// </summary>
        private void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }

        /// <summary>
        /// Pops the top card. Throws if empty — callers (turn/draw resolution)
        /// are expected to check Count first and handle an empty-deck rule
        /// (e.g. reshuffle discard, or a game-specific end condition) before
        /// calling this; this class doesn't decide that policy.
        /// </summary>
        public Card Draw()
        {
            if (_cards.Count == 0)
                throw new InvalidOperationException("Deck.Draw() called on an empty deck.");

            var card = _cards[0];
            _cards.RemoveAt(0);
            return card;
        }

        /// <summary>
        /// Used on standard-player disconnect timeout: the departing player's
        /// hand is reshuffled back into the deck rather than discarded. Cards
        /// are re-inserted at random positions, not appended, so their return
        /// doesn't create a predictable "bottom of deck" pattern.
        /// </summary>
        public void ReturnCardsAndShuffle(IEnumerable<Card> cards)
        {
            _cards.AddRange(cards);
            Shuffle();
        }
    }
}
