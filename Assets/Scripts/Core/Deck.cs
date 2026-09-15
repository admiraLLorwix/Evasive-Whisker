using System;
using System.Collections.Generic;
using System.Linq;

namespace EvasiveWhisker.Core
{
    /// <summary>
    /// Host-only, non-networked deck of Cards. Deck order is private host state and must
    /// never be exposed as [Networked] data — only aggregate info (count) is ever synced
    /// out via MatchState.DeckCount.
    /// </summary>
    public class Deck
    {
        private readonly List<Card> _cards;
        private readonly Random _random;

        public int Count => _cards.Count;

        /// <summary>
        /// Standard construction: build a fresh deck from the full card pool and shuffle it.
        /// Used at match start.
        /// </summary>
        public Deck(IEnumerable<Card> fullCardPool, int? randomSeed = null)
        {
            _random = randomSeed.HasValue ? new Random(randomSeed.Value) : new Random();
            _cards = new List<Card>(fullCardPool);
            Shuffle();
        }

        /// <summary>
        /// Host-migration reconstruction: start from the full known card pool, subtract every
        /// card already accounted for elsewhere (in hands, the discard pile, etc.), and
        /// shuffle the remainder to form the reconstructed deck.
        ///
        /// Note: the previous host's private shuffle order cannot be recovered by a promoted
        /// client, so the reconstructed deck is intentionally re-shuffled rather than
        /// order-preserving. Card *identity* is preserved (no cards created or lost), only
        /// order is not.
        /// </summary>
        public static Deck ReconstructAfterMigration(
            IEnumerable<Card> fullCardPool,
            IEnumerable<Card> knownCardsElsewhere,
            int? randomSeed = null)
        {
            var known = new HashSet<Card>(knownCardsElsewhere);
            var remainder = fullCardPool.Where(c => !known.Contains(c));
            return new Deck(remainder, randomSeed);
        }

        public bool TryDraw(out Card card)
        {
            if (_cards.Count == 0)
            {
                card = default;
                return false;
            }

            int lastIndex = _cards.Count - 1;
            card = _cards[lastIndex];
            _cards.RemoveAt(lastIndex);
            return true;
        }

        public Card Draw()
        {
            if (!TryDraw(out var card))
                throw new InvalidOperationException("Deck.Draw() called on an empty deck.");
            return card;
        }

        /// <summary>
        /// Returns the given cards to the deck and reshuffles. Used for:
        /// - discard-pile reshuffle when the deck runs dry (caller excludes the current
        ///   public top-of-discard card before passing cards in here)
        /// - non-whisker-blower disconnect forfeiture (hand cards go back into the deck,
        ///   not the discard pile)
        /// </summary>
        public void ReturnCardsAndShuffle(IEnumerable<Card> cards)
        {
            _cards.AddRange(cards);
            Shuffle();
        }

        private void Shuffle()
        {
            // Fisher-Yates
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }
    }
}
