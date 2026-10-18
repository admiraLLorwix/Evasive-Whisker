using System.Collections.Generic;

namespace EvasiveWhisker.Core
{
    /// <summary>
    /// Host-only, non-networked draw pile. Deck order is hidden information and must never be
    /// a NetworkVariable; clients only ever see the count (via MatchState.DeckCount).
    ///
    /// The top of the deck is the end of the list, so drawing is O(1).
    /// </summary>
    public class Deck
    {
        private readonly List<CardInstance> _cards = new List<CardInstance>();
        private readonly System.Random _rng = new System.Random();

        public int Count => _cards.Count;

        /// <summary>Builds a deck from the given cards and shuffles it.</summary>
        public Deck(IEnumerable<CardInstance> cards)
        {
            _cards.AddRange(cards);
            Shuffle();
        }

        /// <summary>Removes and returns the top card; false if the deck is empty.</summary>
        public bool TryDraw(out CardInstance card)
        {
            if (_cards.Count == 0)
            {
                card = default;
                return false;
            }

            int last = _cards.Count - 1;
            card = _cards[last];
            _cards.RemoveAt(last);
            return true;
        }

        /// <summary>
        /// Puts cards back into the deck and reshuffles the whole thing. The input is copied,
        /// so the caller may clear its own list afterwards.
        /// </summary>
        public void ReturnCardsAndShuffle(IEnumerable<CardInstance> cards)
        {
            _cards.AddRange(cards);
            Shuffle();
        }

        private void Shuffle()
        {
            for (int i = _cards.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                (_cards[i], _cards[j]) = (_cards[j], _cards[i]);
            }
        }
    }
}
