using UnityEngine;
using System.Collections.Generic;
using EvasiveWhisker.Cards;


namespace EvasiveWhisker.Core
{
    public class DeckManager : MonoBehaviour
    {
        [Header("Card Pools")]
        [SerializeField] private Card whiskerBlowerCard;
        [SerializeField] private List<Card> custodyCards = new List<Card>();
        [SerializeField] private List<Card> abilityAndTrapPool = new List<Card>();

        // Note: active piles during game
        private readonly List<Card> drawDeck = new List<Card>();
        private readonly List<Card> discardPile = new List<Card>();



        public int DrawDeckCount => drawDeck.Count;
        public int DiscardPileCount => discardPile.Count;


        // Card Dealing Setup. 3 Cards per player then minus 1 to mix in the WhiskerBlower
        public Dictionary<int, List<Card>> SetupGameDeck(int totalPlayers)
        {
            drawDeck.Clear();
            discardPile.Clear();


            List<Card> poolCopy = new List<Card>(abilityAndTrapPool);
            Shuffle(poolCopy);

            int initialDealCount = (3 * totalPlayers) - 1;
            List<Card> setupPile = new List<Card>();


            for (int i = 0; i<initialDealCount; i++)
            {
                if (poolCopy.Count == 0) break;
                setupPile.Add(poolCopy[0]);
                poolCopy.RemoveAt(0);
            }

            // mix the whiskerblower card into the pile to be dealt to the players
            setupPile.Add(whiskerBlowerCard);
            Shuffle(setupPile);


            Dictionary<int, List<Card>> initialHands = new Dictionary<int, List<Card>>();
            for (int p = 0; p < totalPlayers; p++)
            {
                initialHands[p] = new List<Card>();
                for (int c = 0; c<3; c++)
                {
                    initialHands[p].Add(setupPile[0]);
                    setupPile.RemoveAt(0);
                }
            }

            // remaining cards punta sa main deck
            drawDeck.AddRange(poolCopy);
            drawDeck.AddRange(custodyCards);
            Shuffle(drawDeck);


            return initialHands;
        }

        public Card DrawCard()
        {
            if (drawDeck.Count == 0)
            {
                Debug.LogWarning("Deck is empty.");
                return null;
            }

            Card drawnCard= drawDeck[0];
            drawDeck.RemoveAt(0);
            return drawnCard;
        }


        public bool DiscardCard(Card card)
        {
            if (card == null) return false;
            if (!card.canBeDiscarded)
            {
                Debug.LogWarning($"{card.cardName} cannot be discarded.");
                return false;
            }


            discardPile.Add(card);
            return true;
        }

        // reshuffle logic. if round number is 3, 6, or 9 magreshuffle yung discard pile into the main deck except the top 3 cards.
        public void ReshuffleDiscardIfEligible(int roundNum)
        {
            if (roundNum != 3 && roundNum != 6 && roundNum != 9)
                return;

            if (discardPile.Count <= 3)
                return;

            int toReturnCount = discardPile.Count - 3;
            List<Card> returningCards = discardPile.GetRange(0, toReturnCount);
            discardPile.RemoveRange(0, toReturnCount);


            drawDeck.AddRange(returningCards);
            Shuffle(drawDeck);
        }


        public Card PeekTopDiscard()
        {
            return discardPile.Count > 0 ? discardPile[discardPile.Count - 1] : null;
        }

        public Card TakeTopDiscard()
        {
            if (discardPile.Count == 0) return null;
            Card top = discardPile[discardPile.Count - 1];
            discardPile.RemoveAt(discardPile.Count - 1);
            return top;
        }

        private void Shuffle(List<Card> list)
        {
            for (int i= list.Count - 1; i>0; i--)
            {
                int r = Random.Range(0, i + 1);
                Card temp = list[i];
                list[i] = list[r];
                list[r] = temp;
            }
        }



#if UNITY_EDITOR
        [ContextMenu("Debug Deck")]
        private void PrintDeckCounts()
        {
            Debug.Log($"Draw Deck: {drawDeck.Count} cards ||| Discard Pile: {discardPile.Count} cards");
        }
#endif

    }
}
