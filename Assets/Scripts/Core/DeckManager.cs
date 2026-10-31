using EvasiveWhisker.Cards;
using System.Collections.Generic;
using UnityEngine;

public class DeckManager : MonoBehaviour
{
    public static DeckManager Instance { get; private set; }

    private List<NetworkCard> drawDeck = new List<NetworkCard>();
    private List<NetworkCard> discardPile = new List<NetworkCard>();

    public int RemainingDrawCount => drawDeck.Count;
    public int DiscardCount => discardPile.Count;

    private void Awake()
    {
        Instance = this;
    }

    public void InitializeDeck(int playerCount, out Dictionary<ulong, List<NetworkCard>> initialHands, List<ulong> playerIds)
    {
        initialHands = new Dictionary<ulong, List<NetworkCard>>();
        List<NetworkCard> masterPool = new List<NetworkCard>();

        // 5 Custody Cards
        AddCardsToPool(masterPool, "InformantMiley", CardType.Custody, 1);
        AddCardsToPool(masterPool, "ProsecutorGarfield", CardType.Custody, 1);
        AddCardsToPool(masterPool, "SergeantDrake", CardType.Custody, 1);
        AddCardsToPool(masterPool, "UndercoverLoki", CardType.Custody, 1);
        AddCardsToPool(masterPool, "WitnessMellow", CardType.Custody, 1);

        // 11 Traps
        AddCardsToPool(masterPool, "Swipe", CardType.Trap, 3);
        AddCardsToPool(masterPool, "RobinPaw", CardType.Trap, 3);
        AddCardsToPool(masterPool, "CatNap", CardType.Trap, 3);
        AddCardsToPool(masterPool, "CatFlash", CardType.Trap, 2);

        // 18 Abilities
        AddCardsToPool(masterPool, "Declaw", CardType.Ability, 3);
        AddCardsToPool(masterPool, "NineLives", CardType.Ability, 3);
        AddCardsToPool(masterPool, "Zoomies", CardType.Ability, 3);
        AddCardsToPool(masterPool, "CatInstinct", CardType.Ability, 3);
        AddCardsToPool(masterPool, "BombasticCatEyes", CardType.Ability, 3);
        AddCardsToPool(masterPool, "Scatter", CardType.Ability, 3);

        // 8 Counterplays
        AddCardsToPool(masterPool, "CopyCat", CardType.Counterplay, 3);
        AddCardsToPool(masterPool, "EyeForAnEye", CardType.Counterplay, 3);
        AddCardsToPool(masterPool, "Hiss", CardType.Counterplay, 2);

        ShuffleList(masterPool);

        int startingPoolCount = (3 * playerCount) - 1;
        List<NetworkCard> startingDeal = new List<NetworkCard>();
        for (int i = 0; i < startingPoolCount; i++)
        {
            startingDeal.Add(masterPool[0]);
            masterPool.RemoveAt(0);
        }

        startingDeal.Add(new NetworkCard("WhiskerBlower", CardType.WhiskerBlower));
        ShuffleList(startingDeal);

        foreach (ulong id in playerIds)
        {
            initialHands[id] = new List<NetworkCard>();
            for (int c = 0; c < 3; c++)
            {
                initialHands[id].Add(startingDeal[0]);
                startingDeal.RemoveAt(0);
            }
        }

        drawDeck.Clear();
        discardPile.Clear();
        drawDeck.AddRange(masterPool);

        Debug.Log($"[DeckManager] Main Draw Deck ready with {drawDeck.Count} cards.");
    }

    private void AddCardsToPool(List<NetworkCard> pool, string cardId, CardType type, int count)
    {
        for (int i = 0; i < count; i++)
        {
            pool.Add(new NetworkCard(cardId, type));
        }
    }

    public NetworkCard? DrawCard()
    {
        if (drawDeck.Count == 0) return null;
        DrawDeckClick.Instance?.UpdateRemainingCount(drawDeck.Count);
        NetworkCard card = drawDeck[0];
        drawDeck.RemoveAt(0);
        return card;
    }

    public void Discard(NetworkCard card)
    {
        discardPile.Add(card);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.BroadcastDiscardCardClientRpc(card);
        }
        else
        {
            DiscardPile2D.Instance?.AddCardToDiscard(card);
        }
    }

    public void ReshuffleMilestone()
    {
        if (discardPile.Count <= 3) return;

        int keepCount = 3;
        List<NetworkCard> cardsToKeep = discardPile.GetRange(discardPile.Count - keepCount, keepCount);
        List<NetworkCard> cardsToReshuffle = discardPile.GetRange(0, discardPile.Count - keepCount);

        drawDeck.AddRange(cardsToReshuffle);
        ShuffleList(drawDeck);

        discardPile = cardsToKeep;
        DrawDeckClick.Instance?.UpdateRemainingCount(drawDeck.Count);
        Debug.Log($"[Deck] Reshuffled {cardsToReshuffle.Count} cards into Draw Deck.");
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            int rnd = Random.Range(i, list.Count);
            T temp = list[i];
            list[i] = list[rnd];
            list[rnd] = temp;
        }
    }
}