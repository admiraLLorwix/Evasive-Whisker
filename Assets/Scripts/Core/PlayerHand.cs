using UnityEngine;
using System.Collections.Generic;
using EvasiveWhisker.Cards;


namespace EvasiveWhisker.Core
{
    public class PlayerHand : MonoBehaviour
    {
        [SerializeField] private int baseMaxHandSize = 5;
        public List<Card> CardsInHand { get; private set; } = new List<Card>();
        public Card ActiveAbilitySlot { get; private set; }
        public Card ChosenDetectiveCat { get; private set;  }


        // molly ability
        public int CurrentHandLimit => (ChosenDetectiveCat != null && ChosenDetectiveCat.cardName == "Hoarder Molly") ? 6 : baseMaxHandSize;


        public bool IsOverHandLimit => CardsInHand.Count > CurrentHandLimit;


        public void AssignDetectiveCat(Card catCard)
        {
            ChosenDetectiveCat = catCard;
        }

        public void AddCard(Card card)
        {
            if (card != null)
                CardsInHand.Add(card);
        }


        public bool RemoveCard(Card card)
        {
            return CardsInHand.Remove(card);
        }


        public void MoveToAbilitySlot(Card abilityCard)
        {
            ActiveAbilitySlot = abilityCard;
            CardsInHand.Remove(abilityCard);
        }


        public Card ClearAbilitySlot()
        {
            Card completedAbility = ActiveAbilitySlot;
            ActiveAbilitySlot = null;
            return completedAbility;
        }


        public int GetCustodyCardCount()
        {
            int count = 0;
            foreach (var card in CardsInHand)
            {
                if (card.cardType == CardType.Custody)
                    count++;
            }
            return count;
        }

        public bool HoldsWhiskerBlower()
        {
            foreach(var card in CardsInHand){
                if (card.cardType == CardType.WhiskerBlower)
                    return true;
            }
            return false;
        }
    }
}