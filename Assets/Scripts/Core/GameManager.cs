using UnityEngine;
using EvasiveWhisker.Cards;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;

public enum GameState
{
    WaitingToStart,
    DraftingDetectiveCats,
    DealingStartingCards,
    PlayerTurn_Draw,
    PlayerTurn_Action,
    PlayerTurn_DiscardPrompt,
    GameOver
}

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public NetworkVariable<GameState> CurrentState = new NetworkVariable<GameState>(GameState.WaitingToStart);
    public NetworkVariable<int> CurrentRound = new NetworkVariable<int>(1);
    public NetworkVariable<ulong> ActiveTurnPlayerId = new NetworkVariable<ulong>();
    public NetworkVariable<int> ActiveDraftIndex = new NetworkVariable<int>(0);


    private List<ulong> turnOrder = new List<ulong>();

    private List<ulong> draftOrder = new List<ulong>(); 



    private Dictionary<ulong, List<NetworkCard>> playerHands = new Dictionary<ulong, List<NetworkCard>>();
    private Dictionary<ulong, DetectiveCatType> chosenDetectives = new Dictionary<ulong, DetectiveCatType>();
    private List<DetectiveCatType> availableCatPool = new List<DetectiveCatType>
    {
        DetectiveCatType.HoarderMolly,
        DetectiveCatType.InspectorBebang,
        DetectiveCatType.DetectiveNido,
        DetectiveCatType.DecoyMokang,
        DetectiveCatType.AgentPutol,
        DetectiveCatType.SleightOfPawShadow,
        DetectiveCatType.BombSquadWhamos,
        DetectiveCatType.DumpsterDiverShimo
    };

    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        string myName = (OnlineLobbyManager.Instance != null && !string.IsNullOrEmpty(OnlineLobbyManager.Instance.LocalPlayerName))
            ? OnlineLobbyManager.Instance.LocalPlayerName
            : $"Player {NetworkManager.Singleton.LocalClientId}";

        SubmitPlayerNameServerRpc(myName);

        if (IsServer)
        {
            StartCoroutine(InitializeGameRoutine());
        }
    }
    private Dictionary<ulong, string> playerNamesMap = new Dictionary<ulong, string>();

    [Rpc(SendTo.Server)]
    public void SubmitPlayerNameServerRpc(string playerName, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        playerNamesMap[senderId] = playerName;
    }

    private IEnumerator InitializeGameRoutine()
    {
        //yield return new WaitForSeconds(1.0f); not returning player names, not enough time

        int expectedPlayers = NetworkManager.Singleton.ConnectedClientsList.Count;
        float timeout = 8.0f;

        while (playerNamesMap.Count < expectedPlayers && timeout > 0f)
        {
            timeout -= 0.2f;
            yield return new WaitForSeconds(0.2f);
        }

        Debug.Log($"[GameManager] player names received: {playerNamesMap.Count}/{expectedPlayers}");


        turnOrder.Clear();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            turnOrder.Add(client.ClientId);
        }

        draftOrder = new List<ulong>(turnOrder);
        draftOrder.Reverse();

        List<FixedString32Bytes> namesList = new List<FixedString32Bytes>();
        foreach (ulong id in turnOrder)
        {
            string pName = playerNamesMap.ContainsKey(id) ? playerNamesMap[id] : $"Detective #{id}";
            namesList.Add(new FixedString32Bytes(pName));
        }

        SetupIslandsClientRpc(turnOrder.ToArray(), namesList.ToArray());

        CurrentState.Value = GameState.DraftingDetectiveCats;
        ActiveDraftIndex.Value = 0;
        PromptNextDraftPlayer();
    }




    [ClientRpc]
    private void SetupIslandsClientRpc(ulong[] orderedPlayers, Unity.Collections.FixedString32Bytes[] playerNames)
    {
        Dictionary<ulong, string> namesDict = new Dictionary<ulong, string>();
        for (int i = 0; i < orderedPlayers.Length; i++)
        {
            namesDict[orderedPlayers[i]] = playerNames[i].ToString();
            //namesDict[orderedPlayers[i]] = playerNames[i];
        }

        // Visuals-related (setup islands)
        BoardLayoutManager.Instance.SetupPlayerIslands(new List<ulong>(orderedPlayers), namesDict);
    }


    private void PromptNextDraftPlayer()
    {
        if (ActiveDraftIndex.Value < draftOrder.Count)
        {
            ulong choosingPlayerId = draftOrder[ActiveDraftIndex.Value];

            NotifyPlayerToPickCatClientRpc(choosingPlayerId, availableCatPool.ToArray());
        }
        else
        {
            StartCoroutine(DealStartingHandsRoutine());
        }
    }


    [ClientRpc]
    private void NotifyPlayerToPickCatClientRpc(ulong targetPlayerId, DetectiveCatType[] remainingCats)
    {
        bool isMe = NetworkManager.Singleton.LocalClientId == targetPlayerId;
        if (isMe && DraftPanelController.Instance != null)
        {
            DraftPanelController.Instance.OpenDraftSelection(remainingCats);
        }
    }

    [Rpc(SendTo.Server)]
    public void SubmitChosenCatServerRpc(DetectiveCatType catType, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        chosenDetectives[senderId] = catType;

        availableCatPool.Remove(catType);

        Debug.Log($"[Draft] Player {senderId} picked {catType}. {availableCatPool.Count} cats remain");

        BroadcastDetectiveChosenClientRpc(senderId, catType);

        ActiveDraftIndex.Value++;
        PromptNextDraftPlayer();
    }



    private IEnumerator DealStartingHandsRoutine()
    {
        CurrentState.Value = GameState.DealingStartingCards;

        DeckManager.Instance.InitializeDeck(turnOrder.Count, out playerHands, turnOrder);

        foreach (var pair in playerHands)
        {
            ulong clientId = pair.Key;
            NetworkCard[] cards = pair.Value.ToArray();

            ClientRpcParams clientParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
            };
            ReceiveStartingHandClientRpc(cards, clientParams);
        }

        foreach (var client in turnOrder)
        {
            UpdateOpponentHandCountClientRpc(client, 3);
        }

        yield return new WaitForSeconds(1.5f);

        CurrentRound.Value = 1;
        ActiveTurnPlayerId.Value = turnOrder[0];
        StartTurn(ActiveTurnPlayerId.Value);
    }

    [ClientRpc]
    private void ReceiveStartingHandClientRpc(NetworkCard[] hand, ClientRpcParams clientRpcParams = default)
    {
        PlayerHandController.Instance.SetHand(hand);
    }

    [ClientRpc]
    private void UpdateTurnUIClientRpc(ulong activePlayerId, string activePlayerName, int roundNumber)
    {
        bool isMe = NetworkManager.Singleton.LocalClientId == activePlayerId;

        if (TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.UpdateRoundDisplay(roundNumber);
            TurnActionUI.Instance.ShowDrawPrompt(isMe, activePlayerName);
        }
    }

    private void StartTurn(ulong playerId)
    {
        CurrentState.Value = GameState.PlayerTurn_Draw;
        ActiveTurnPlayerId.Value = playerId;

        string playerName = playerNamesMap.ContainsKey(playerId) ? playerNamesMap[playerId] : $"Player {playerId}";

        UpdateTurnUIClientRpc(playerId, playerName, CurrentRound.Value);

        Debug.Log($"[Turn] Started turn for {playerName}. Waiting for Draw");
    }

    public void StartPlayerTurn(ulong playerId)
    {
        CurrentState.Value = GameState.PlayerTurn_Draw;
        ActiveTurnPlayerId.Value = playerId;

        string playerName = playerNamesMap.ContainsKey(playerId) ? playerNamesMap[playerId] : $"Player {playerId}";

        NotifyTurnPhaseClientRpc(playerId, playerName, GameState.PlayerTurn_Draw);
    }

    [ClientRpc]
    private void NotifyTurnPhaseClientRpc(ulong activePlayerId, string activePlayerName, GameState phase)
    {
        bool isMe = NetworkManager.Singleton.LocalClientId == activePlayerId;

        if (TurnActionUI.Instance == null) return;

        switch (phase)
        {
            case GameState.PlayerTurn_Draw:
                TurnActionUI.Instance.ShowDrawPrompt(isMe, activePlayerName);
                break;
            case GameState.PlayerTurn_Action:
                if (isMe) TurnActionUI.Instance.ShowActionPrompt();
                break;
        }
    }

    [Rpc(SendTo.Server)]
    public void RequestDrawCardServerRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != ActiveTurnPlayerId.Value || CurrentState.Value != GameState.PlayerTurn_Draw) return;

        NetworkCard? drawn = DeckManager.Instance.DrawCard();
        if (drawn.HasValue)
        {
            playerHands[senderId].Add(drawn.Value);

            ClientRpcParams clientParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { senderId } }
            };
            ReceiveDrawnCardClientRpc(drawn.Value, clientParams);

            UpdateOpponentHandCountClientRpc(senderId, playerHands[senderId].Count);
        }

        CheckHandLimitOrProceedToAction(senderId);
    }

    private void CheckHandLimitOrProceedToAction(ulong playerId)
    {
        int limit = GetMaxHandLimit(playerId);
        int handCount = playerHands[playerId].Count;

        if (handCount > limit)
        {
            CurrentState.Value = GameState.PlayerTurn_DiscardPrompt;
            PromptDiscardClientRpc(playerId, handCount, limit);
        }
        else
        {
            CurrentState.Value = GameState.PlayerTurn_Action;
            string pName = playerNamesMap.ContainsKey(playerId) ? playerNamesMap[playerId] : $"Player {playerId}";
            NotifyTurnPhaseClientRpc(playerId, pName, GameState.PlayerTurn_Action);
        }
    }

    [ClientRpc]
    private void PromptDiscardClientRpc(ulong targetPlayerId, int currentCount, int maxLimit)
    {
        if (NetworkManager.Singleton.LocalClientId == targetPlayerId && TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.ShowDiscardPrompt(currentCount, maxLimit);
        }
    }

    [Rpc(SendTo.Server)]
    public void SubmitDiscardServerRpc(NetworkCard cardToDiscard, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != ActiveTurnPlayerId.Value) return;

        if (cardToDiscard.Type == CardType.WhiskerBlower)
        {
            Debug.LogWarning("[GameManager] WhiskerBlower card cannot be discarded");
            return;
        }

        if (playerHands[senderId].Remove(cardToDiscard))
        {
            DeckManager.Instance.Discard(cardToDiscard);
            UpdateOpponentHandCountClientRpc(senderId, playerHands[senderId].Count);
            Debug.Log($"[GameManager] Player {senderId} discarded {cardToDiscard.CardId}.");
        }

        CheckHandLimitOrProceedToAction(senderId);
    }

    [Rpc(SendTo.Server)]
    public void SubmitPlayAbilityCardServerRpc(NetworkCard card, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != ActiveTurnPlayerId.Value) return;

        Debug.Log($"[GameManager] Player #{senderId} played Ability card: {card.CardId}");

        playerHands[senderId].Remove(card);
        UpdateOpponentHandCountClientRpc(senderId, playerHands[senderId].Count);

        DeckManager.Instance.Discard(card);

        CheckHandLimitOrProceedToAction(senderId);
        AdvanceToNextTurn();
    }

    public void AdvanceToNextTurn()
    {
        if (!IsServer) return;

        int currentIndex = turnOrder.IndexOf(ActiveTurnPlayerId.Value);
        int nextIndex = (currentIndex + 1) % turnOrder.Count;

        if (nextIndex == 0)
        {
            CurrentRound.Value++;
            Debug.Log($"[GameManager] Round {CurrentRound.Value} begun");

            if (CurrentRound.Value > 10)
            {
                ResolveRound10EndGame();
                return;
            }

            if (CurrentRound.Value == 3 || CurrentRound.Value == 6 || CurrentRound.Value == 9)
            {
                DeckManager.Instance.ReshuffleMilestone();
            }
        }

        StartTurn(turnOrder[nextIndex]);
    }

    [ClientRpc]
    private void ReceiveDrawnCardClientRpc(NetworkCard card, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[Draw] drew: {card.CardId} ({card.Type})");
        if (PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.AddCard(card);
            PlayerHandController.Instance.UpdateHandFanLayout();
        }
    }

    private void CheckHandLimitOrProceed(ulong playerId)
    {
        int maxHand = GetMaxHandLimit(playerId);
        if (playerHands[playerId].Count > maxHand)
        {
            CurrentState.Value = GameState.PlayerTurn_DiscardPrompt;
            PromptDiscardClientRpc(playerId);
        }
        else
        {
            CurrentState.Value = GameState.PlayerTurn_Action;
        }
    }

    public int GetMaxHandLimit(ulong playerId)
    {
        if (chosenDetectives.TryGetValue(playerId, out DetectiveCatType cat))
        {
            if (cat == DetectiveCatType.HoarderMolly) return 6;
        }
        return 5;
    }

    [ClientRpc]
    private void PromptDiscardClientRpc(ulong playerId)
    {
        if (NetworkManager.Singleton.LocalClientId == playerId)
        {
            Debug.LogWarning("Hand limit exceeded, Choose a card to discard");
        }
    }

    public void EndTurn()
    {
        if (!IsServer) return;

        int round = CurrentRound.Value;
        if (round == 3 || round == 6 || round == 9)
        {
            DeckManager.Instance.ReshuffleMilestone();
        }

        int currentIndex = turnOrder.IndexOf(ActiveTurnPlayerId.Value);
        int nextIndex = (currentIndex + 1) % turnOrder.Count;

        if (nextIndex == 0)
        {
            CurrentRound.Value++;
            if (CurrentRound.Value > 10)
            {
                ResolveRound10EndGame();
                return;
            }
        }

        ActiveTurnPlayerId.Value = turnOrder[nextIndex];
        StartTurn(ActiveTurnPlayerId.Value);
    }

    [Rpc(SendTo.Server)]
    public void RequestEndTurnServerRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != ActiveTurnPlayerId.Value) return;

        AdvanceToNextTurn();
    }

    public int GetPlayerHandCount(ulong playerId)
    {
        return playerHands.ContainsKey(playerId) ? playerHands[playerId].Count : 0;
    }

    public void ExecuteSteal(ulong thiefId, ulong victimId, int cardIndex)
    {
        if (!playerHands.ContainsKey(victimId) || cardIndex >= playerHands[victimId].Count) return;

        NetworkCard stolenCard = playerHands[victimId][cardIndex];
        playerHands[victimId].RemoveAt(cardIndex);

        UpdateOpponentHandCountClientRpc(victimId, playerHands[victimId].Count);

        if (stolenCard.Type == CardType.Trap)
        {
            Debug.Log($"<color=red>[TRAP TRIGGERED!] Thief #{thiefId} stole {stolenCard.CardId} from #{victimId}</color>");
            DeckManager.Instance.Discard(stolenCard);

            ResolveTrapTrigger(thiefId, victimId, stolenCard.CardId.ToString());
        }
        else
        {
            playerHands[thiefId].Add(stolenCard);
            UpdateOpponentHandCountClientRpc(thiefId, playerHands[thiefId].Count);

            ClientRpcParams thiefParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { thiefId } }
            };
            ReceiveDrawnCardClientRpc(stolenCard, thiefParams);
            Debug.Log($"[Steal] Thief #{thiefId} successfully stole a {stolenCard.Type}");
        }
        CheckHandLimitOrProceedToAction(thiefId);
        AdvanceToNextTurn();
    }

    private void ResolveTrapTrigger(ulong thiefId, ulong victimId, string trapName)
    {
        AnnounceTrapTriggerClientRpc(thiefId, victimId, trapName);

        switch (trapName)
        {
            case "CatNap":
                //to set: Thief skips next turn
                break;
            case "CatFlash":
                //to set: Thief reveals whole hand to victim
                break;
            case "Swipe":
                // to set: Thief gives a card back to victim
                break;
        }
    }

    [ClientRpc]
    private void AnnounceTrapTriggerClientRpc(ulong thiefId, ulong victimId, string trapName)
    {
        Debug.LogWarning($"ALERT: Player {thiefId} triggered a {trapName} TRAP set by Player {victimId}");
    }


    private void ResolveRound10EndGame()
    {
        CurrentState.Value = GameState.GameOver;

        ulong winnerId = 0;
        foreach (var pair in playerHands)
        {
            if (pair.Value.Exists(c => c.Type == CardType.WhiskerBlower))
            {
                winnerId = pair.Key;
                break;
            }
        }

        AnnounceWinnerClientRpc(winnerId, "Held the Whisker-Blower at Round 10");
    }

    [Rpc(SendTo.Server)]
    public void SubmitCustodyGuessServerRpc(ulong accusedPlayerId, RpcParams rpcParams = default)
    {
        ulong guesserId = rpcParams.Receive.SenderClientId;

        bool hasWhiskerBlower = playerHands[accusedPlayerId].Exists(c => c.Type == CardType.WhiskerBlower);

        if (hasWhiskerBlower)
        {
            CurrentState.Value = GameState.GameOver;
            AnnounceWinnerClientRpc(guesserId, "Correctly guessed the Custodian with 3 Custody cards");
        }
        else
        {
            Debug.Log($"[Guess Failed] Player {guesserId} guessed wrong");
        }
    }

    [ClientRpc]
    private void AnnounceWinnerClientRpc(ulong winnerId, string reason)
    {
        Debug.Log($"<color=yellow>GAME OVER, Winner is Player #{winnerId} ; Win Condition: {reason}</color>");
    }

    [ClientRpc]
    public void BroadcastDetectiveChosenClientRpc(ulong playerId, DetectiveCatType catType)
    {
        var opponentIsland = BoardLayoutManager.Instance.GetOpponentIsland(playerId);
        if (opponentIsland != null)
        {
            opponentIsland.SetDetectiveCat(catType);
        }

        if (playerId == NetworkManager.Singleton.LocalClientId && PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.SetLocalDetectiveCat(catType);
        }
    }



    [ClientRpc]
    public void UpdateOpponentHandCountClientRpc(ulong playerId, int newCount)
    {
        var island = BoardLayoutManager.Instance.GetOpponentIsland(playerId);
        if (island != null)
        {
            island.UpdateHandCardCount(newCount);
        }
    }

    [ClientRpc]
    public void UpdateOpponentActiveAbilityClientRpc(ulong playerId, string abilityCardId)
    {
        var island = BoardLayoutManager.Instance.GetOpponentIsland(playerId);
        if (island != null)
        {
            Card abilityCard = string.IsNullOrEmpty(abilityCardId)
                ? null
                : CardDatabase.Instance.GetCard(abilityCardId);

            island.SetActiveAbility(abilityCard);
        }
    }

}