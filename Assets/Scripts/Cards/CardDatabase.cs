using System.Collections.Generic;
using UnityEngine;

namespace EvasiveWhisker.Cards
{
    [System.Serializable]
    public class DetectiveCatVisual
    {
        public DetectiveCatType catType;
        public Sprite islandProfileSprite;
        public Sprite localHandCardSprite;
    }

    [System.Serializable]
    public class CardAssetEntry
    {
        public string cardID;
        public CardType cardType;
        public Sprite cardSprite;
    }

    public class CardDatabase : MonoBehaviour
    {
        public static CardDatabase Instance { get; private set; }

        [SerializeField] private List<Card> allCards = new List<Card>();

        [Header("detective cats")]
        [SerializeField] private List<DetectiveCatVisual> detectiveCatVisuals = new List<DetectiveCatVisual>();

        [Header("png assets")]
        [SerializeField] private List<CardAssetEntry> cardArtList = new List<CardAssetEntry>();

        private Dictionary<string, Card> cardLookup = new Dictionary<string, Card>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            foreach (var card in allCards)
            {
                if (card != null && !cardLookup.ContainsKey(card.cardID))
                {
                    cardLookup.Add(card.cardID, card);
                }
            }
        }

        public Card GetCard(string cardId)
        {
            if (cardLookup.TryGetValue(cardId, out Card card))
            {
                return card;
            }
            return null;
        }

        public Sprite GetCardSprite(string cardId)
        {
            var entry = cardArtList.Find(c => c.cardID == cardId);
            return entry != null ? entry.cardSprite : null;
        }

        public Sprite GetIslandProfileSprite(DetectiveCatType catType)
        {
            var visual = detectiveCatVisuals.Find(v => v.catType == catType);
            return visual != null ? visual.islandProfileSprite : null;
        }

        public Sprite GetLocalHandCardSprite(DetectiveCatType catType)
        {
            var visual = detectiveCatVisuals.Find(v => v.catType == catType);
            return visual != null ? visual.localHandCardSprite : null;
        }
    }
}