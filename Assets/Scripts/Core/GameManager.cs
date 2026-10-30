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
    private Dictionary<ulong, string> playerNamesMap = new Dictionary<ulong, string>();

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

    private Dictionary<ulong, NetworkCard?> activePlayerAbilities = new Dictionary<ulong, NetworkCard?>();
    private Dictionary<ulong, int> abilityExpiryRounds = new Dictionary<ulong, int>();

    private bool hasTakenActionThisTurn = false;


    private void Awake()
    {
        Instance = this;
    }

    public override void OnNetworkSpawn()
    {
        string myName = (OnlineLobbyManager.Instance != null && !string.IsNullOrEmpty(OnlineLobbyManager.Instance.LocalPlayerName))
            ? OnlineLobbyManager.Instance.LocalPlayerName
            : PlayerPrefs.GetString("PlayerName", $"Detective_{Random.Range(100, 999)}");

        SubmitPlayerNameServerRpc(myName);

        if (IsServer)
        {
            StartCoroutine(InitializeGameRoutine());
        }
    }

    [Rpc(SendTo.Server)]
    public void SubmitPlayerNameServerRpc(string playerName, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (string.IsNullOrWhiteSpace(playerName))
        {
            playerName = $"Detective_{senderId + 1}";
        }

        playerNamesMap[senderId] = playerName;
        Debug.Log($"[GameManager] Player #{senderId} as '{playerName}'");
    }

    private IEnumerator InitializeGameRoutine()
    {
        Debug.Log("[GameManager] initializegameroutine");
        yield return new WaitForSeconds(1.5f);

        turnOrder.Clear();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            turnOrder.Add(client.ClientId);
        }

        draftOrder = new List<ulong>(turnOrder);
        draftOrder.Reverse();

        Debug.Log($"[GameManager] Connected players: {turnOrder.Count}");

        List<FixedString32Bytes> namesList = new List<FixedString32Bytes>();
        foreach (ulong id in turnOrder)
        {
            string pName = playerNamesMap.ContainsKey(id) ? playerNamesMap[id] : $"Detective #{id}";
            namesList.Add(new FixedString32Bytes(pName));
        }

        SetupIslandsClientRpc(turnOrder.ToArray(), namesList.ToArray());

        yield return new WaitForSeconds(0.5f);

        CurrentState.Value = GameState.DraftingDetectiveCats;
        ActiveDraftIndex.Value = 0;

        Debug.Log("<color=cyan>[GameManager] starting Detective Cat Draft</color>");
        PromptNextDraftPlayer();
    }

    [ClientRpc]
    private void SetupIslandsClientRpc(ulong[] orderedPlayers, FixedString32Bytes[] playerNames)
    {
        Dictionary<ulong, string> namesDict = new Dictionary<ulong, string>();
        for (int i = 0; i < orderedPlayers.Length; i++)
        {
            namesDict[orderedPlayers[i]] = playerNames[i].ToString();
        }

        if (BoardLayoutManager.Instance != null)
        {
            BoardLayoutManager.Instance.SetupPlayerIslands(new List<ulong>(orderedPlayers), namesDict);
        }
    }


    private void PromptNextDraftPlayer()
    {
        if (ActiveDraftIndex.Value < draftOrder.Count)
        {
            ulong choosingPlayerId = draftOrder[ActiveDraftIndex.Value];
            string chooserName = playerNamesMap.ContainsKey(choosingPlayerId) ? playerNamesMap[choosingPlayerId] : $"Player #{choosingPlayerId}";

            NotifyPlayerToPickCatClientRpc(choosingPlayerId, chooserName, availableCatPool.ToArray());
        }
        else
        {
            CloseDraftPanelsClientRpc();
            StartCoroutine(DealStartingHandsRoutine());
        }
    }

    [ClientRpc]
    private void NotifyPlayerToPickCatClientRpc(ulong targetPlayerId, string chooserName, DetectiveCatType[] remainingCats)
    {
        bool isMe = NetworkManager.Singleton.LocalClientId == targetPlayerId;

        if (DraftPanelController.Instance != null)
        {
            if (isMe)
            {
                DraftPanelController.Instance.OpenDraftForChooser(remainingCats);
            }
            else
            {
                DraftPanelController.Instance.ShowWaitingForChooser(chooserName);
            }
        }
    }

    [ClientRpc]
    private void CloseDraftPanelsClientRpc()
    {
        if (DraftPanelController.Instance != null)
        {
            DraftPanelController.Instance.HideAll();
        }
    }

    [Rpc(SendTo.Server)]
    public void SubmitChosenCatServerRpc(DetectiveCatType catType, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        chosenDetectives[senderId] = catType;
        availableCatPool.Remove(catType);

        BroadcastDetectiveChosenClientRpc(senderId, catType);

        ActiveDraftIndex.Value++;
        PromptNextDraftPlayer();
    }

    [ClientRpc]
    public void BroadcastDetectiveChosenClientRpc(ulong playerId, DetectiveCatType catType)
    {
        var opponentIsland = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(playerId) : null;
        if (opponentIsland != null)
        {
            opponentIsland.SetDetectiveCat(catType);
        }

        if (playerId == NetworkManager.Singleton.LocalClientId && PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.SetLocalDetectiveCat(catType);
        }
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
            UpdateOpponentHandCountClientRpc(clientId, 3);
        }

        yield return new WaitForSeconds(1.5f);

        CurrentRound.Value = 1;
        StartPlayerTurn(turnOrder[0]);
    }

    [ClientRpc]
    private void ReceiveStartingHandClientRpc(NetworkCard[] hand, ClientRpcParams clientRpcParams = default)
    {
        if (PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.SetHand(hand);
        }
    }


    public void StartPlayerTurn(ulong playerId)
    {
        CurrentState.Value = GameState.PlayerTurn_Draw;
        ActiveTurnPlayerId.Value = playerId;

        hasTakenActionThisTurn = false;

        string playerName = GetPlayerName(playerId);

        if (activePlayerAbilities.ContainsKey(playerId) && activePlayerAbilities[playerId].HasValue)
        {
            if (abilityExpiryRounds.ContainsKey(playerId) && CurrentRound.Value >= abilityExpiryRounds[playerId])
            {
                NetworkCard expiredCard = activePlayerAbilities[playerId].Value;
                activePlayerAbilities[playerId] = null;

                Debug.Log($"[GameManager] {playerName}'s {expiredCard.CardId} expired and goes to discard pile.");

                ClearPlayerActiveAbilityClientRpc(playerId);
                DeckManager.Instance.Discard(expiredCard);
            }
        }

        UpdateTurnUIClientRpc(playerId, playerName, CurrentRound.Value);
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

    [ClientRpc]
    private void ReceiveDrawnCardClientRpc(NetworkCard card, ClientRpcParams clientRpcParams = default)
    {
        if (PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.AddCard(card);
        }
    }



    public void CheckHandLimitOrProceedToAction(ulong playerId)
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
            NotifyActionPhaseClientRpc(playerId);
        }
    }

    [ClientRpc]
    public void BroadcastDiscardCardClientRpc(NetworkCard card)
    {
        if (DiscardPile2D.Instance != null)
        {
            DiscardPile2D.Instance.AddCardToDiscard(card);
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

    [ClientRpc]
    private void NotifyActionPhaseClientRpc(ulong activePlayerId)
    {
        bool isMe = NetworkManager.Singleton.LocalClientId == activePlayerId;
        if (isMe && TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.ShowActionPhaseModal();
        }
    }


    //todo: bug ending player turn immediately when discarding
    [Rpc(SendTo.Server)]
    public void SubmitDiscardServerRpc(NetworkCard cardToDiscard, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != ActiveTurnPlayerId.Value) return;

        if (cardToDiscard.Type == CardType.WhiskerBlower) return;

        if (playerHands[senderId].Remove(cardToDiscard))
        {
            DeckManager.Instance.Discard(cardToDiscard);
            UpdateOpponentHandCountClientRpc(senderId, playerHands[senderId].Count);
        }

        int limit = GetMaxHandLimit(senderId);
        int currentHandCount = playerHands[senderId].Count;

        if (currentHandCount > limit)
        {
            CurrentState.Value = GameState.PlayerTurn_DiscardPrompt;
            PromptDiscardClientRpc(senderId, currentHandCount, limit);
            return;
        }

        if (!hasTakenActionThisTurn)
        {
            CurrentState.Value = GameState.PlayerTurn_Action;
            NotifyActionPhaseClientRpc(senderId);
        }
        else
        {
            AdvanceToNextTurn();
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
    private void SetPlayerActiveAbilityClientRpc(ulong playerId, string cardId)
    {
        if (playerId == NetworkManager.Singleton.LocalClientId && PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.SetActiveAbility(cardId);
        }

        var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(playerId) : null;
        if (island != null)
        {
            island.SetActiveAbility(cardId);
        }
    }


    [Rpc(SendTo.Server)]
    public void SubmitPlayAbilityCardServerRpc(NetworkCard card, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != ActiveTurnPlayerId.Value) return;

        Debug.Log($"<color=cyan>{GetPlayerName(senderId)} played {card.CardId}</color>");

        playerHands[senderId].Remove(card);
        UpdateOpponentHandCountClientRpc(senderId, playerHands[senderId].Count);

        if (card.CardId == "NineLives" || card.CardId == "Zoomies" || card.CardId == "CatNap")
        {
            activePlayerAbilities[senderId] = card;
            abilityExpiryRounds[senderId] = CurrentRound.Value + 1;

            SetPlayerActiveAbilityClientRpc(senderId, card.CardId.ToString());
        }
        else
        {
            DeckManager.Instance.Discard(card);
        }

        CheckHandLimitOrProceedToAction(senderId);
        AdvanceToNextTurn();
    }

    [ClientRpc]
    private void ClearPlayerActiveAbilityClientRpc(ulong playerId)
    {
        if (playerId == NetworkManager.Singleton.LocalClientId && PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.ClearActiveAbility();
        }

        var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(playerId) : null;
        if (island != null)
        {
            island.ClearActiveAbility();
        }
    }

    [ClientRpc]
    private void RemoveStolenCardClientRpc(NetworkCard card, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"<color=orange>[Stolen] An opponent stole {card.CardId} from your hand</color>");
        if (PlayerHandController.Instance != null)
        {
            PlayerHandController.Instance.RemoveCard(card);
        }
    }

    public void ExecuteSteal(ulong thiefId, ulong victimId, int cardIndex)
    {
        hasTakenActionThisTurn = true;

        if (!playerHands.ContainsKey(victimId) || cardIndex >= playerHands[victimId].Count) return;

        NetworkCard stolenCard = playerHands[victimId][cardIndex];
        playerHands[victimId].RemoveAt(cardIndex);

        ClientRpcParams victimParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { victimId } }
        };
        RemoveStolenCardClientRpc(stolenCard, victimParams);

        UpdateOpponentHandCountClientRpc(victimId, playerHands[victimId].Count);

        if (stolenCard.Type == CardType.Trap)
        {
            Debug.Log($"[Steal] {GetPlayerName(thiefId)} stole a card from {GetPlayerName(victimId)}.");
            DeckManager.Instance.Discard(stolenCard);
            AnnounceTrapTriggerClientRpc(thiefId, victimId, stolenCard.CardId.ToString());
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
        }

        if (stolenCard.CardId == "CatNap")
        {
            activePlayerAbilities[thiefId] = stolenCard;
            abilityExpiryRounds[thiefId] = CurrentRound.Value + 1;
            SetPlayerActiveAbilityClientRpc(thiefId, "CatNap");
        }

        CheckHandLimitOrProceedToAction(thiefId);
        int limit = GetMaxHandLimit(thiefId);
        if (playerHands[thiefId].Count > limit)
        {
            CurrentState.Value = GameState.PlayerTurn_DiscardPrompt;
            PromptDiscardClientRpc(thiefId, playerHands[thiefId].Count, limit);
        }
        else
        {
            AdvanceToNextTurn();
        }
    }

    [ClientRpc]
    private void AnnounceTrapTriggerClientRpc(ulong thiefId, ulong victimId, string trapName)
    {
        Debug.LogWarning($"ALERT: Player {thiefId} triggered {trapName} TRAP by Player {victimId}");
    }

    [Rpc(SendTo.Server)]
    public void SubmitCustodyGuessServerRpc(ulong accusedPlayerId, RpcParams rpcParams = default)
    {
        ulong guesserId = rpcParams.Receive.SenderClientId;

        bool isCorrect = playerHands[accusedPlayerId].Exists(c => c.Type == CardType.WhiskerBlower);

        if (isCorrect)
        {
            CurrentState.Value = GameState.GameOver;
            string winnerName = GetPlayerName(guesserId);
            AnnounceWinnerClientRpc(guesserId, $"{winnerName} correctly guessed the Custodian with 3 Custody cards");
        }
        else
        {
            Debug.Log($"[Guess Failed] Player #{guesserId} guessed incorrectly");
            AdvanceToNextTurn();
        }
    }


    public void AdvanceToNextTurn()
    {
        if (!IsServer) return;

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

            if (CurrentRound.Value == 3 || CurrentRound.Value == 6 || CurrentRound.Value == 9)
            {
                DeckManager.Instance.ReshuffleMilestone();
            }
        }

        StartPlayerTurn(turnOrder[nextIndex]);
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

        string winnerName = GetPlayerName(winnerId);
        AnnounceWinnerClientRpc(winnerId, $"{winnerName} held the WhiskerBlower at Round 10");
    }

    [ClientRpc]
    public void UpdateOpponentHandCountClientRpc(ulong playerId, int newCount)
    {
        var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(playerId) : null;
        if (island != null)
        {
            island.UpdateCardCount(newCount);
        }
    }

    public int GetPlayerHandCount(ulong playerId) => playerHands.ContainsKey(playerId) ? playerHands[playerId].Count : 0;
    public string GetPlayerName(ulong playerId)
    {
        if (playerNamesMap.TryGetValue(playerId, out string pName) && !string.IsNullOrWhiteSpace(pName))
        {
            return pName;
        }
        return $"Detective_{playerId + 1}";
    }
    public DetectiveCatType GetPlayerDetective(ulong playerId) => chosenDetectives.ContainsKey(playerId) ? chosenDetectives[playerId] : DetectiveCatType.HoarderMolly;

    [ClientRpc]
    private void AnnounceWinnerClientRpc(ulong winnerId, string reason)
    {
        string winnerName = GetPlayerName(winnerId);
        Debug.Log($"<color=yellow>GAME OVER | Winner: {winnerName} | Reason: {reason}</color>");

        if (GameOverUI.Instance != null)
        {
            GameOverUI.Instance.ShowWinner(winnerName, reason);
        }
    }

}