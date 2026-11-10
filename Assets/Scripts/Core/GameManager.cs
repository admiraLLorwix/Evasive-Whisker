using UnityEngine;
using EvasiveWhisker.Cards;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using System.Linq;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public enum GameState
{
    WaitingToStart,
    DraftingDetectiveCats,
    DealingStartingCards,
    PlayerTurn_Draw,
    PlayerTurn_Action,
    PlayerTurn_DiscardPrompt,
    PlayerTurn_Resolving,
    GameOver
}

public enum TurnActionKind
{
    Ability,
    Steal,
    Guess
}

public partial class GameManager : NetworkBehaviour
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
    private Dictionary<ulong, int> abilityTurnDuration = new Dictionary<ulong, int>();

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
    //private Dictionary<ulong, int> abilityExpiryRounds = new Dictionary<ulong, int>();
    private HashSet<ulong> trapImmunePlayers = new HashSet<ulong>();
    private HashSet<ulong> pendingZoomiesPlayers = new HashSet<ulong>();
    private Dictionary<ulong, int> remainingActionsThisTurn = new Dictionary<ulong, int>();

    private bool hasTakenActionThisTurn = false;
    private TurnActionKind? firstActionKind = null;
    public const ulong NoTarget = ulong.MaxValue;


    private void Awake()
    {
        Instance = this;

        CardEffectRegistry.RegisterAbility("Declaw", new DeclawEffect(), requiresTarget: true);
        CardEffectRegistry.RegisterAbility("NineLives", new NineLivesEffect());
        CardEffectRegistry.RegisterAbility("Zoomies", new ZoomiesEffect());
        CardEffectRegistry.RegisterInteractiveAbility("CatInstinct", new CatInstinctEffect(), requiresTarget: true);
        CardEffectRegistry.RegisterInteractiveAbility("BombasticCatEyes", new BombasticCatEyesEffect(), requiresTarget: true);
        CardEffectRegistry.RegisterInteractiveAbility("Scatter", new ScatterEffect());

        CardEffectRegistry.RegisterTrap("CatNap", new CatNapTrapEffect());
        CardEffectRegistry.RegisterInteractiveTrap("CatFlash", new CatFlashTrapEffect());
        CardEffectRegistry.RegisterInteractiveTrap("Swipe", new SwipeTrapEffect());
        CardEffectRegistry.RegisterInteractiveTrap("RobinPaw", new RobinPawTrapEffect());

        CardEffectRegistry.RegisterCounterplay("Hiss", new HissEffect());
        CardEffectRegistry.RegisterCounterplay("CopyCat", new CopyCatEffect());
        CardEffectRegistry.RegisterCounterplay("EyeForAnEye", new EyeForAnEyeEffect());

        CardEffectRegistry.RegisterDetective(DetectiveCatType.InspectorBebang, new InspectorBebangTrait());
        CardEffectRegistry.RegisterDetective(DetectiveCatType.DumpsterDiverShimo, new DumpsterDiverShimoTrait());
        CardEffectRegistry.RegisterDetective(DetectiveCatType.DetectiveNido, new DetectiveNidoTrait());
    }
    private void Update()
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (_matchEnded) return;
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        if (keyboard.f1Key.wasPressedThisFrame)
        {
            Debug.Log("<color=blue>[dev] give 3 Custody Cards</color>");
            if (PlayerHandController.Instance != null)
            {
                PlayerHandController.Instance.AddCard(new NetworkCard("InformantMiley", CardType.Custody));
                PlayerHandController.Instance.AddCard(new NetworkCard("ProsecutorGarfield", CardType.Custody));
                PlayerHandController.Instance.AddCard(new NetworkCard("SergeantDrake", CardType.Custody));
            }
        }

        if (keyboard.f2Key.wasPressedThisFrame && IsServer)
        {
            Debug.Log("<color=blue>[dev] ff to Round 10</color>");
            CurrentRound.Value = 10;
            if (TurnActionUI.Instance != null) TurnActionUI.Instance.UpdateRoundDisplay(10);
            AdvanceToNextTurn();
        }
        if (keyboard.f3Key.wasPressedThisFrame)
        {
            Debug.Log("<color=blue>[dev] force trigger GameOverModal</color>");
            if (IsServer)
            {
                CurrentState.Value = GameState.GameOver;
                DebugTriggerGameOverClientRpc();
            }
            else
            {
                DebugTriggerGameOverServerRpc();
            }
        }
#endif
    }
    [Rpc(SendTo.Server)]
    private void DebugTriggerGameOverServerRpc(RpcParams rpcParams = default)
    {
        if (_matchEnded) return;
        CurrentState.Value = GameState.GameOver;
        DebugTriggerGameOverClientRpc();
    }

    [ClientRpc]
    private void DebugTriggerGameOverClientRpc()
    {
        _matchEnded = true;
        GameOverUI.Instance?.ShowWinner("Detective (Debug)", "Testing the Game Over Screen");
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleServerClientDisconnect;
            CurrentState.OnValueChanged += HandleStateChangedServer;
        }
        else
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientConnectionLost;
        }

        if (OnlineLobbyManager.Instance != null && OnlineLobbyManager.Instance.CurrentLobby != null)
        {
            MatchContext.PreviousLobbyId = OnlineLobbyManager.Instance.CurrentLobby.Id;
            MatchContext.PreviousLobbyName = OnlineLobbyManager.Instance.CurrentLobby.Name;
            MatchContext.PreviousLobbyCode = OnlineLobbyManager.Instance.CurrentLobby.LobbyCode;
            MatchContext.PreviousMaxPlayers = OnlineLobbyManager.Instance.CurrentLobby.MaxPlayers;
        }

        string myName = (OnlineLobbyManager.Instance != null && !string.IsNullOrEmpty(OnlineLobbyManager.Instance.LocalPlayerName))
            ? OnlineLobbyManager.Instance.LocalPlayerName
            : $"Detective_{Random.Range(100, 999)}";

        Debug.Log($"[client] '{myName}'");
        SubmitPlayerNameServerRpc(myName);
    }

    public override void OnNetworkDespawn()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleServerClientDisconnect;
            NetworkManager.Singleton.OnClientDisconnectCallback -= HandleClientConnectionLost;
        }
        CurrentState.OnValueChanged -= HandleStateChangedServer;
    }

    [Rpc(SendTo.Server)]
    public void SubmitPlayerNameServerRpc(string playerName, RpcParams rpcParams = default)
    {
        if (_matchEnded) return;
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (CurrentState.Value != GameState.WaitingToStart)
        {
            Debug.LogWarning($"[Server] kick player #{senderId}, game is already in progress");
            NetworkManager.Singleton.DisconnectClient(senderId);
            return;
        }
        if (string.IsNullOrWhiteSpace(playerName)) playerName = $"Detective_{Random.Range(100, 999)}";

        playerNamesMap[senderId] = playerName;
        Debug.Log($"<color=green>[Server] '{playerName}' from #{senderId} ({playerNamesMap.Count}/{NetworkManager.Singleton.ConnectedClientsList.Count})</color>");

        if (IsServer && CurrentState.Value == GameState.WaitingToStart)
        {
            if (playerNamesMap.Count >= NetworkManager.Singleton.ConnectedClientsList.Count)
            {
                StartCoroutine(InitializeGameRoutine());
            }
        }
    }


    //test if still using not proper names
    private IEnumerator InitializeGameRoutine()
    {
        Debug.Log("<color=cyan>[gm] starting match setup...</color>");
        yield return new WaitForSeconds(0.2f);

        turnOrder.Clear();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            turnOrder.Add(client.ClientId);
        }

        draftOrder = new List<ulong>(turnOrder);
        draftOrder.Reverse();

        List<FixedString32Bytes> namesList = new List<FixedString32Bytes>();
        List<ulong> idsList = new List<ulong>();

        foreach (ulong id in turnOrder)
        {
            string pName = playerNamesMap.ContainsKey(id) ? playerNamesMap[id] : $"Detective_{Random.Range(100, 999)}";
            namesList.Add(new FixedString32Bytes(pName));
            idsList.Add(id);
        }

        SyncAllPlayerNamesClientRpc(idsList.ToArray(), namesList.ToArray());
        SetupIslandsClientRpc(turnOrder.ToArray(), namesList.ToArray());

        yield return new WaitForSeconds(0.5f);

        CurrentState.Value = GameState.DraftingDetectiveCats;
        ActiveDraftIndex.Value = 0;
        PromptNextDraftPlayer();
    }

    [ClientRpc]
    private void SyncAllPlayerNamesClientRpc(ulong[] ids, FixedString32Bytes[] names)
    {
        playerNamesMap.Clear();
        for (int i = 0; i < ids.Length; i++)
        {
            playerNamesMap[ids[i]] = names[i].ToString();
        }
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

    public string GetPlayerActiveAbilityId(ulong playerId)
    {
        if (activePlayerAbilities.TryGetValue(playerId, out NetworkCard? ability) && ability.HasValue)
        {
            return ability.Value.CardId;
        }
        return null;
    }


    private void PromptNextDraftPlayer()
    {
        if (ActiveDraftIndex.Value < draftOrder.Count)
        {
            ulong choosingPlayerId = draftOrder[ActiveDraftIndex.Value];

            string chooserName = GetPlayerName(choosingPlayerId);

            NotifyPlayerToPickCatClientRpc(choosingPlayerId, chooserName, availableCatPool.ToArray());
            StartDraftTimer(choosingPlayerId);
        }
        else
        {
            CancelDecisionTimer();
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
        if (_matchEnded) return;
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (CurrentState.Value != GameState.DraftingDetectiveCats) return;
        if (ActiveDraftIndex.Value >= draftOrder.Count || draftOrder[ActiveDraftIndex.Value] != senderId) return;
        if (!availableCatPool.Contains(catType)) return;

        ApplyChosenCat(senderId, catType);
    }

    private void ApplyChosenCat(ulong senderId, DetectiveCatType catType)
    {
        CancelDecisionTimer();
        chosenDetectives[senderId] = catType;
        availableCatPool.Remove(catType);

        BroadcastDetectiveChosenClientRpc(senderId, catType);

        ActiveDraftIndex.Value++;
        PromptNextDraftPlayer();
    }

    [ClientRpc]
    public void BroadcastDetectiveChosenClientRpc(ulong playerId, DetectiveCatType catType)
    {
        chosenDetectives[playerId] = catType;
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
            UpdateDrawDeckCountClientRpc(DeckManager.Instance.RemainingDrawCount);
            UpdateOpponentHandCountClientRpc(clientId, 3);
        }
        InitShuffleCharges();
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

    //dont check here
    public void StartPlayerTurn(ulong playerId)
    {
        ClearShuffleLocks();
        CancelDecisionTimer();
        ResetShuffleCharges(playerId);
        TickGuessImmunity(playerId);
        queuedEyeForAnEye = null; queuedCopyCat = null;
        if (ShouldSkipTurnForCatNap(playerId))
        {
            StartCoroutine(SkipTurnRoutine(playerId));
            return;
        }
        CurrentState.Value = GameState.PlayerTurn_Draw;
        ActiveTurnPlayerId.Value = playerId;
        hasTakenActionThisTurn = false;
        firstActionKind = null;

        string playerName = GetPlayerName(playerId);

        if (pendingZoomiesPlayers.Contains(playerId))
        {
            remainingActionsThisTurn[playerId] = 2;
            pendingZoomiesPlayers.Remove(playerId);
            Debug.Log($"<color=green>[turn] {playerName} has zoomies, 2 actions granted this turn</color>");
        }
        else
        {
            remainingActionsThisTurn[playerId] = 1;
        }


        UpdateTurnUIClientRpc(playerId, playerName, CurrentRound.Value);
        Debug.Log($"[turn] {playerName}");
        StartDrawTimer(playerId);
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
        else
        {
            Debug.LogError("[gm] null turnactionui");
        }

        if (BoardLayoutManager.Instance != null)
        {
            BoardLayoutManager.Instance.HighlightActivePlayer(activePlayerId);
        }
    }

    [ClientRpc]
    public void UpdateDrawDeckCountClientRpc(int remainingCount)
    {
        DrawDeckClick.Instance?.UpdateRemainingCount(remainingCount);
    }

    [Rpc(SendTo.Server)]
    public void RequestDrawCardServerRpc(RpcParams rpcParams = default)
    {
        if (_matchEnded) return;
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != ActiveTurnPlayerId.Value || CurrentState.Value != GameState.PlayerTurn_Draw) return;
        PerformDraw(senderId);
    }

    private void PerformDraw(ulong senderId)
    {
        CancelDecisionTimer();
        NetworkCard? drawn = DeckManager.Instance.DrawCard();
        LogActionClientRpc($"{GetPlayerName(senderId)} drew a card.");
        if (drawn.HasValue)
        {
            NetworkCard drawnCard = drawn.Value;
            PresentDrawnCard(senderId, drawnCard, $"You drew {drawnCard.CardId}!", $"{GetPlayerName(senderId)} drew a card!", false);
        }
        else
        {
            Debug.LogWarning("[gm] draw deck is empty - skipping the draw");
            SendNotice(senderId, "<color=red>The draw deck is empty - no card drawn</color>");
            CheckHandLimitOrProceedToAction(senderId);
        }
    }

    private void PresentDrawnCard(ulong playerId, NetworkCard card, string ownMessage, string othersMessage, bool publicCard)
    {
        CurrentState.Value = GameState.PlayerTurn_Resolving;
        LockShuffle(playerId);

        ClientRpcParams playerParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { playerId } }
        };
        ShowDrawSpotlightClientRpc(card.CardId.ToString(), ownMessage, false, playerParams);


        List<ulong> spectatorIds = new List<ulong>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != playerId) spectatorIds.Add(client.ClientId);
        }

        if (spectatorIds.Count > 0)
        {
            ClientRpcParams spectatorParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams { TargetClientIds = spectatorIds.ToArray() }
            };
            ShowDrawSpotlightClientRpc(publicCard ? card.CardId.ToString() : "", othersMessage, !publicCard, spectatorParams);
        }

        UpdateDrawDeckCountClientRpc(DeckManager.Instance.RemainingDrawCount);
        StartCoroutine(ResolveDrawAfterSpotlightRoutine(playerId, card));
    }

    [ClientRpc]
    private void ShowDrawSpotlightClientRpc(string cardId, string message, bool isSpectator, ClientRpcParams clientRpcParams = default)
    {
        DrawDeckClick.Instance?.SetGlow(false);
        if (CardSpotlightController.Instance != null)
        {
            Color glow = isSpectator ? new Color(0.6f, 0.4f, 1f, 1f) : new Color(0.2f, 1f, 0.4f, 1f);
            CardSpotlightController.Instance.PlaySpotlight(cardId, message, glow, 2.5f, isSpectator);
        }
    }

    private IEnumerator ResolveDrawAfterSpotlightRoutine(ulong drawerId, NetworkCard drawnCard)
    {
        yield return new WaitForSeconds(2.8f);

        playerHands[drawerId].Add(drawnCard);

        ClientRpcParams drawerParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { drawerId } }
        };

        //upd
        ReceiveDrawnCardClientRpc(drawnCard, drawerParams);
        UpdateOpponentHandCountClientRpc(drawerId, playerHands[drawerId].Count); ClearShuffleLocks();
        yield return new WaitForSeconds(0.45f);
        CheckHandLimitOrProceedToAction(drawerId);
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
            BeginDiscardTimer(playerId);
        }
        else
        {
            PromptActionPhase(playerId);
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
    private void NotifyActionPhaseClientRpc(ulong activePlayerId, bool abilityOnly, bool allowSkip)
    {
        bool isMe = NetworkManager.Singleton.LocalClientId == activePlayerId;
        if (isMe && TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.ShowActionPhaseModal(abilityOnly, allowSkip);
        }
    }


    [Rpc(SendTo.Server)]
    public void SubmitDiscardServerRpc(NetworkCard cardToDiscard, RpcParams rpcParams = default)
    {
        if (_matchEnded) return;
        ProcessDiscard(rpcParams.Receive.SenderClientId, cardToDiscard);
    }

    private void ProcessDiscard(ulong senderId, NetworkCard cardToDiscard)
    {
        if (senderId != ActiveTurnPlayerId.Value || CurrentState.Value != GameState.PlayerTurn_DiscardPrompt)
        {
            RejectCardRequest(senderId, cardToDiscard, "You can't discard right now");
            return;
        }

        if (cardToDiscard.Type == CardType.WhiskerBlower)
        {
            RejectCardRequest(senderId, cardToDiscard, "The WhiskerBlower cannot be discarded");
            return;
        }

        if (!playerHands[senderId].Remove(cardToDiscard))
        {
            RejectCardRequest(senderId, cardToDiscard, "That card isn't in your hand");
            return;
        }
        CancelDecisionTimer();
        DeckManager.Instance.Discard(cardToDiscard);
        UpdateOpponentHandCountClientRpc(senderId, playerHands[senderId].Count);

        int limit = GetMaxHandLimit(senderId);
        int currentHandCount = playerHands[senderId].Count;

        if (currentHandCount > limit)
        {
            PromptDiscardClientRpc(senderId, currentHandCount, limit);
            BeginDiscardTimer(senderId);
            return;
        }

        if (!hasTakenActionThisTurn)
        {
            PromptActionPhase(senderId);
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
    public void SubmitPlayAbilityCardServerRpc(NetworkCard card, ulong targetId = NoTarget, RpcParams rpcParams = default)
    {
        if (_matchEnded) return;
        ProcessPlayAbility(rpcParams.Receive.SenderClientId, card, targetId);
    }
    private void ProcessPlayAbility(ulong senderId, NetworkCard card, ulong targetId)
    {
        string error = ValidateAbilityPlay(senderId, card, ref targetId);
        if (error != null)
        {
            RejectCardRequest(senderId, card, error);
            return;
        }
        LogActionClientRpc($"{GetPlayerName(senderId)} played {card.CardId}.");
        CurrentState.Value = GameState.PlayerTurn_Resolving;
        CancelDecisionTimer();
        if (card.CardId == "Scatter") LockShuffleForEveryone();
        else if (targetId != NoTarget) LockShuffle(targetId);

        playerHands[senderId].Remove(card);
        UpdateOpponentHandCountClientRpc(senderId, playerHands[senderId].Count);

        string actorName = GetPlayerName(senderId);
        float spotlightSeconds = (targetId != NoTarget && card.CardId != "BombasticCatEyes") ? ReactionWindowSeconds : 3.5f;
        BroadcastAbilitySpotlightClientRpc(card.CardId.ToString(), $"{actorName} played {card.CardId}!", spotlightSeconds);


        StartCoroutine(ResolveAbilityAfterSpotlightRoutine(senderId, targetId, card));
    }

    [ClientRpc]
    private void BroadcastAbilitySpotlightClientRpc(string cardId, string message, float duration)
    {
        Color glow = new Color(1f, 0.85f, 0.2f, 1f);
        if (CardSpotlightController.Instance != null)
        {
            CardSpotlightController.Instance.PlaySpotlight(cardId, message, glow, duration);
        }
        else
        {
            var spotlight = FindAnyObjectByType<CardSpotlightController>(FindObjectsInactive.Include);
            spotlight?.PlaySpotlight(cardId, message, glow, duration);
            //(cardId, message, new Color(1f, 0.85f, 0.2f, 1f), 2.5f);
        }
    }

    private IEnumerator ResolveAbilityAfterSpotlightRoutine(ulong senderId, ulong targetId, NetworkCard card)
    {
        if (targetId == NoTarget || card.CardId == "BombasticCatEyes")
        {
            yield return new WaitForSeconds(3f);
            RunAbilityEffect(senderId, targetId, card);
            yield break;
        }

        StartReactionWindow(new ReactionRequest
        {
            Attacker = senderId,
            Defender = targetId,
            ActionTags = AbilityReactionTags(card.CardId),
            Description = $"{GetPlayerName(senderId)} played {card.CardId} on you",
            OnProceed = () => RunAbilityEffect(senderId, targetId, card),
            OnCancelled = endTurn => CancelAbility(senderId, card, endTurn)
        });
    }

    private void RunAbilityEffect(ulong senderId, ulong targetId, NetworkCard card)
    {
        bool finished = CardEffectRegistry.TriggerAbility(card.CardId, senderId, targetId, card, () => FinishAbilityResolution(senderId));
        if (finished) FinishAbilityResolution(senderId);
    }
    private void CancelAbility(ulong senderId, NetworkCard card, bool endAttackerTurn)
    {
        DeckManager.Instance.Discard(card);

        if (endAttackerTurn) EndTurnAfterCancel(senderId, TurnActionKind.Ability);
        else FinishAbilityResolution(senderId);
    }

    public void FinishAbilityResolution(ulong senderId)
    {
        if (!IsServer || CurrentState.Value != GameState.PlayerTurn_Resolving) return;
        AfterAction(senderId, TurnActionKind.Ability);
    }

    private void CompleteSteal(ulong thiefId, NetworkCard stolenCard)
    {
        playerHands[thiefId].Add(stolenCard);
        UpdateOpponentHandCountClientRpc(thiefId, playerHands[thiefId].Count);

        ReceiveDrawnCardClientRpc(stolenCard, SingleTarget(thiefId));

        AfterAction(thiefId, TurnActionKind.Steal);
    }

    private IEnumerator ResolveTrapAfterSpotlightRoutine(ulong thiefId, ulong victimId, NetworkCard trapCard, bool thiefImmune, System.Action onDone)
    {
        yield return new WaitForSeconds(3.8f);

        if (thiefImmune)
        {
            DeckManager.Instance.Discard(trapCard);
            onDone?.Invoke();
            yield break;
        }
        bool finished = CardEffectRegistry.TriggerTrap(trapCard.CardId, thiefId, victimId, trapCard, onDone);
        if (finished) onDone?.Invoke();
    }
    public void FinishTrapResolution(ulong thiefId)
    {
        if (!IsServer || CurrentState.Value != GameState.PlayerTurn_Resolving) return;



        ClearThiefTurnUIClientRpc(SingleTarget(thiefId));
        AfterAction(thiefId, TurnActionKind.Steal);
    }
    [ClientRpc]
    private void ClearThiefTurnUIClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.HideAllActions();
        }
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

    [ClientRpc]
    private void ShowStealSpotlightClientRpc(string cardId, string message, bool isSpectator, float duration, ClientRpcParams clientRpcParams = default)
    {
        if (CardSpotlightController.Instance != null)
        {
            Color glow = isSpectator ? new Color(0.6f, 0.4f, 1f, 1f) : new Color(0f, 0.85f, 1f, 1f);
            CardSpotlightController.Instance.PlaySpotlight(cardId, message, glow, duration, isSpectator);
        }
    }
    [ClientRpc]
    private void CloseThiefStealUIClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (StealManager.Instance != null)
        {
            StealManager.Instance.CancelSelection();
        }
        if (TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.HideAllActions();
        }
    }
    public void ExecuteSteal(ulong thiefId, ulong victimId, int cardIndex)
    {
        if (!IsServer) return;

        string error = ValidateSteal(thiefId, victimId, cardIndex);
        if (error != null)
        {
            ReleaseStealTargetLock(thiefId);
            RejectAction(thiefId, error);
            return;
        }
        LogActionClientRpc($"{GetPlayerName(thiefId)} is stealing from {GetPlayerName(victimId)}.");
        PlayVersusSpotlightClientRpc(thiefId, victimId, "is stealing from");
        CancelDecisionTimer();
        CurrentState.Value = GameState.PlayerTurn_Resolving;

        LockShuffle(thiefId);
        LockShuffle(victimId);

        CloseThiefStealUIClientRpc(SingleTarget(thiefId));
        //NetworkCard stolenCard = playerHands[victimId][cardIndex];
        ////playerHands[victimId].RemoveAt(cardIndex);

        //string thiefName = GetPlayerName(thiefId); string victimName = GetPlayerName(victimId);

        //ClientRpcParams thiefOnlyParams = new ClientRpcParams
        //{
        //    Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { thiefId } }
        //};
        //CloseThiefStealUIClientRpc(thiefOnlyParams);

        //StartReactionWindow(new ReactionRequest
        //{
        //    Attacker = thiefId,
        //    Defender = victimId,
        //    ActionTags = StealReactionTags,
        //    Description = $"{thiefName} is stealing from you",
        //    Reveal = new ReactionReveal
        //    {
        //        Card = stolenCard,
        //        AttackerWindowMsg = $"Waiting for {victimName} to react...",
        //        DefenderWindowMsg = $"{thiefName} is stealing {stolenCard.CardId} from you!",
        //        SpectatorWindowMsg = $"{thiefName} is stealing a card from {victimName}!",
        //        AttackerResultMsg = $"You stole {stolenCard.CardId} from {victimName}!",
        //        DefenderResultMsg = $"{thiefName} stole {stolenCard.CardId} from you!",
        //        SpectatorResultMsg = $"{thiefName} stole a card from {victimName}!"
        //    },

        //    OnProceed = () => ProceedSteal(thiefId, victimId, cardIndex, stolenCard),
        //    OnCancelled = endTurn => CancelSteal(thiefId, endTurn)
        //});
        StartCoroutine(DelayStealReaction(thiefId, victimId, cardIndex));
    }
    private void ProceedSteal(ulong thiefId, ulong victimId, int cardIndex, NetworkCard stolenCard)
    {
        List<NetworkCard> victimHand = playerHands[victimId];

        if (cardIndex >= 0 && cardIndex < victimHand.Count && victimHand[cardIndex].Equals(stolenCard)) victimHand.RemoveAt(cardIndex);
        else victimHand.Remove(stolenCard);

        RemoveStolenCardClientRpc(stolenCard, SingleTarget(victimId));
        UpdateOpponentHandCountClientRpc(victimId, victimHand.Count);

        if (stolenCard.Type != CardType.Trap)
        {
            CompleteSteal(thiefId, stolenCard);
            return;
        }
        TriggerStolenTrap(thiefId, victimId, stolenCard, () => FinishTrapResolution(thiefId));
    }

    private void CancelSteal(ulong thiefId, bool endAttackerTurn)
    {
        if (endAttackerTurn) EndTurnAfterCancel(thiefId, TurnActionKind.Steal);
        else AfterAction(thiefId, TurnActionKind.Steal);
    }

    private bool IsImmuneToTraps(ulong playerId) => HasTrapImmunity(playerId) || IsGuessImmune(playerId);





    //for broadcasting to client

    [ClientRpc]
    private void BroadcastTrapSpotlightClientRpc(string cardId, string message, string thiefName, string victimName)
    {
        if (CardSpotlightController.Instance != null)
        {
            CardSpotlightController.Instance.PlaySpotlight(cardId, message, new Color(1f, 0.15f, 0.15f, 1f), 3.5f);
        }
    }
    [ClientRpc]
    private void PlayVersusSpotlightClientRpc(ulong attackerId, ulong victimId, string actionText)
    {
        CardSpotlightController.Instance?.PlayVersusSpotlight(attackerId, victimId, actionText, 2.5f);
    }

    [ClientRpc]
    private void UnlockStealSelectionClientRpc(ClientRpcParams clientRpcParams = default)
    {
        if (StealManager.Instance != null)
        {
            StealManager.Instance.CancelSelection();
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
        if (_matchEnded) return;
        ProcessCustodyGuess(rpcParams.Receive.SenderClientId, accusedPlayerId);
    }

    private void ProcessCustodyGuess(ulong guesserId, ulong accusedPlayerId)
    {
        string error = ValidateGuess(guesserId, accusedPlayerId);
        if (error != null)
        {
            RejectAction(guesserId, error);
            return;
        }
        LogActionClientRpc($"{GetPlayerName(guesserId)} accused {GetPlayerName(accusedPlayerId)}!");
        CancelDecisionTimer();
        bool isCorrect = playerHands[accusedPlayerId].Exists(c => c.Type == CardType.WhiskerBlower);

        if (isCorrect)
        {
            CloseThiefStealUIClientRpc(SingleTarget(guesserId));
            StartCoroutine(ResolveCorrectGuessRoutine(guesserId, accusedPlayerId));
        }
        else
        {
            CurrentState.Value = GameState.PlayerTurn_Resolving;
            StartCoroutine(ResolveWrongGuessRoutine(guesserId, accusedPlayerId));
        }
    }

    [Rpc(SendTo.Server)]
    public void SubmitEndTurnServerRpc(RpcParams rpcParams = default)
    {
        if (_matchEnded) return;
        ulong senderId = rpcParams.Receive.SenderClientId;
        if (senderId != ActiveTurnPlayerId.Value || CurrentState.Value != GameState.PlayerTurn_Action) return;
        if (!firstActionKind.HasValue) return;

        CancelDecisionTimer();
        CloseThiefStealUIClientRpc(SingleTarget(senderId));
        EndTurnSequence(senderId);
    }

    private void AfterAction(ulong playerId, TurnActionKind kind)
    {
        //eye for an eye follow up
        CancelDecisionTimer();
        if (TryRunQueuedCounterFollowUp(kind, () => AfterAction(playerId, kind))) return;


        ClearShuffleLocks();
        if (!firstActionKind.HasValue) firstActionKind = kind;
        hasTakenActionThisTurn = true;
        ConsumeAction(playerId);

        bool canActAgain = GetRemainingActions(playerId) > 0 &&
                           (kind == TurnActionKind.Ability || HandHasPlayableAbility(playerId));

        if (canActAgain)
        {
            Debug.Log($"<color=yellow>[zoomies] {GetPlayerName(playerId)} has an action remaining</color>");
            PromptActionPhase(playerId);
            return;
        }

        EndTurnSequence(playerId);
    }

    // Hand limit checked here, once, at the END of the turn (not after each action)
    private void EndTurnSequence(ulong playerId)
    {
        int limit = GetMaxHandLimit(playerId);
        int count = playerHands[playerId].Count;

        if (count > limit)
        {
            CurrentState.Value = GameState.PlayerTurn_DiscardPrompt;
            PromptDiscardClientRpc(playerId, count, limit);
            BeginDiscardTimer(playerId);
            return;
        }

        AdvanceToNextTurn();
    }

    private void PromptActionPhase(ulong playerId)
    {
        CurrentState.Value = GameState.PlayerTurn_Action;

        bool abilityOnly = firstActionKind.HasValue && firstActionKind.Value != TurnActionKind.Ability;
        bool allowSkip = firstActionKind.HasValue;
        NotifyActionPhaseClientRpc(playerId, abilityOnly, allowSkip);
        StartActionTimer(playerId);
    }
    //private bool HandHasPlayableAbility(ulong playerId)
    //{
    //    if (!playerHands.TryGetValue(playerId, out var hand)) return false;

    //    bool slotFull = GetPlayerActiveAbilityId(playerId) != null;
    //    bool hasTarget = false;
    //    foreach (ulong id in turnOrder)
    //    {
    //        if (id != playerId && !IsGuessImmune(id)) { hasTarget = true; break; }
    //    }

    //    foreach (NetworkCard card in hand)
    //    {
    //        if (card.Type != CardType.Ability) continue;
    //        if (!CardEffectRegistry.HasAbility(card.CardId)) continue;
    //        if (CardEffectRegistry.OccupiesSlot(card.CardId) && slotFull) continue;
    //        if (CardEffectRegistry.RequiresTarget(card.CardId) && !hasTarget) continue;
    //        return true;
    //    }
    //    return false;
    //}
    // ------------------------------------------------------------------ validation (server only authority)

    private string ValidateAbilityPlay(ulong senderId, NetworkCard card, ref ulong targetId)
    {
        string cardId = card.CardId;

        if (senderId != ActiveTurnPlayerId.Value) return "It's not your turn";
        if (CurrentState.Value != GameState.PlayerTurn_Action) return "You can't play an ability right now";
        if (GetRemainingActions(senderId) <= 0) return "You have no actions left";
        if (card.Type != CardType.Ability) return "That card can't be played";
        if (!playerHands.TryGetValue(senderId, out var hand) || !hand.Contains(card)) return "That card isn't in your hand";
        if (!CardEffectRegistry.HasAbility(cardId)) return $"{cardId} isn't implemented yet";

        if (CardEffectRegistry.OccupiesSlot(cardId) && GetPlayerActiveAbilityId(senderId) != null)
            return "Your Active Ability slot is full";

        if (CardEffectRegistry.RequiresTarget(cardId))
        {
            if (targetId == senderId || !playerHands.ContainsKey(targetId)) return "Pick an opponent to target";
            if (IsGuessImmune(targetId)) return GuessImmuneMessage(targetId);
        }
        else
        {
            targetId = NoTarget;
        }

        return null;
    }

    private string ValidateSteal(ulong thiefId, ulong victimId, int cardIndex)
    {
        if (thiefId != ActiveTurnPlayerId.Value) return "It's not your turn";
        if (CurrentState.Value != GameState.PlayerTurn_Action) return "You can't steal right now";
        if (GetRemainingActions(thiefId) <= 0) return "You have no actions left";
        if (firstActionKind.HasValue && firstActionKind.Value != TurnActionKind.Ability)
            return "Only one Steal or Guess per turn";
        if (victimId == thiefId || !playerHands.TryGetValue(victimId, out var victimHand)) return "Invalid target";
        if (IsGuessImmune(victimId)) return GuessImmuneMessage(victimId);
        if (victimHand.Count == 0) return "That player has no cards to steal";
        if (cardIndex < 0 || cardIndex >= victimHand.Count) return "Invalid card";
        return null;
    }

    private string ValidateGuess(ulong guesserId, ulong accusedId)
    {
        if (guesserId != ActiveTurnPlayerId.Value) return "It's not your turn";
        if (CurrentState.Value != GameState.PlayerTurn_Action) return "You can't guess right now";
        if (GetRemainingActions(guesserId) <= 0) return "You have no actions left";
        if (firstActionKind.HasValue && firstActionKind.Value != TurnActionKind.Ability)
            return "Only one Steal or Guess per turn";
        if (accusedId == guesserId || !playerHands.ContainsKey(accusedId)) return "Invalid target";
        if (IsGuessImmune(accusedId)) return GuessImmuneMessage(accusedId);
        if (CountCustodyCards(guesserId) < CustodyCardsRequired) return $"You need {CustodyCardsRequired} Custody cards to accuse someone";
        return null;
    }

    // ------------------------------------------------------------------ rejection + notices

    private ClientRpcParams SingleTarget(ulong clientId) => new ClientRpcParams
    {
        Send = new ClientRpcSendParams { TargetClientIds = new ulong[] { clientId } }
    };

    private void SendNotice(ulong playerId, string message)
    {
        ShowNoticeClientRpc(message, SingleTarget(playerId));
    }

    [ClientRpc]
    private void ShowNoticeClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        TurnActionUI.Instance?.ShowTemporaryNotification(message, 3f);
    }

    private void RejectCardRequest(ulong playerId, NetworkCard card, string message)
    {
        Debug.LogWarning($"[gm] rejected card request from #{playerId} ({card.CardId}): {message}");

        bool serverStillHasCard = playerHands.TryGetValue(playerId, out var hand) && hand.Contains(card);
        if (serverStillHasCard)
            RestoreCardClientRpc(card, $"<color=red>{message}</color>", SingleTarget(playerId));
        else
            SendNotice(playerId, $"<color=red>{message}</color>");

        if (playerId == ActiveTurnPlayerId.Value && CurrentState.Value == GameState.PlayerTurn_Action)
            PromptActionPhase(playerId);
    }

    [ClientRpc]
    private void RestoreCardClientRpc(NetworkCard card, string message, ClientRpcParams clientRpcParams = default)
    {
        PlayerHandController.Instance?.AddCard(card);
        TurnActionUI.Instance?.ShowTemporaryNotification(message, 3f);
    }

    private void RejectAction(ulong playerId, string message)
    {
        Debug.LogWarning($"[gm] rejected action from #{playerId}: {message}");
        SendNotice(playerId, $"<color=red>{message}</color>");

        if (playerId == ActiveTurnPlayerId.Value && CurrentState.Value == GameState.PlayerTurn_Action)
            PromptActionPhase(playerId);
    }


    public void AdvanceToNextTurn()
    {
        if (!IsServer || _matchEnded || CurrentState.Value == GameState.GameOver) return;

        ulong endingPlayerId = ActiveTurnPlayerId.Value;

        if (activePlayerAbilities.TryGetValue(endingPlayerId, out NetworkCard? equipped) && equipped.HasValue
            && abilityTurnDuration.TryGetValue(endingPlayerId, out int turnsLeft))
        {
            turnsLeft--;
            abilityTurnDuration[endingPlayerId] = turnsLeft;

            if (turnsLeft <= 0)
            {
                Debug.Log($"<color=orange>[ability expired] {GetPlayerName(endingPlayerId)} completed their turn, {equipped.Value.CardId} moves to Discard Pile!</color>");
                RemoveActiveAbility(endingPlayerId);
            }
        }

        int currentIndex = turnOrder.IndexOf(endingPlayerId);
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
                BroadcastReshuffleAlertClientRpc(CurrentRound.Value);
            }
        }

        StartPlayerTurn(turnOrder[nextIndex]);
    }

    [ClientRpc]
    private void BroadcastReshuffleAlertClientRpc(int round)
    {
        if (TurnActionUI.Instance != null)
        {
            TurnActionUI.Instance.ShowTemporaryNotification(
                $"<color=red>ROUND {round}: DISCARD PILE RESHUFFLED INTO MAIN DECK</color>",
                3.5f
            );
        }
    }

    private void ResolveRound10EndGame()
    {
        ulong winnerId = 0;
        foreach (var pair in playerHands)
        {
            if (pair.Value.Exists(c => c.Type == CardType.WhiskerBlower))
            {
                winnerId = pair.Key;
                break;
            }
        }

        if (winnerId == 0 && turnOrder.Count > 0)
        {
            winnerId = turnOrder[Random.Range(0, turnOrder.Count)];
        }

        string winnerName = GetPlayerName(winnerId);

        TriggerGameOver(winnerId, $"{winnerName} held the WhiskerBlower at Round 10");
    }

    private IEnumerator RetryUpdateIslandCount(ulong playerId, int newCount)
    {
        yield return new WaitForSeconds(0.3f);
        var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(playerId) : null;
        island?.UpdateCardCount(newCount);
    }


    [ClientRpc]
    public void UpdateOpponentHandCountClientRpc(ulong playerId, int newCount)
    {
        var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(playerId) : null;
        if (island != null)
        {
            island.UpdateCardCount(newCount);
        }
        else
        {
            StartCoroutine(RetryUpdateIslandCount(playerId, newCount));
        }
    }

    public int GetPlayerHandCount(ulong playerId) => playerHands.ContainsKey(playerId) ? playerHands[playerId].Count : 0;
    public string GetPlayerName(ulong playerId)
    {
        if (playerNamesMap.TryGetValue(playerId, out string pName) && !string.IsNullOrWhiteSpace(pName))
        {
            return pName;
        }
        return $"Detective_{Random.Range(100, 999)}";
    }


    public DetectiveCatType GetPlayerDetective(ulong playerId) => chosenDetectives.ContainsKey(playerId) ? chosenDetectives[playerId] : DetectiveCatType.HoarderMolly;

    [ClientRpc]
    private void AnnounceWinnerClientRpc(ulong winnerId, string reason)
    {
        _matchEnded = true; 
        string winnerName = GetPlayerName(winnerId);
        Debug.Log($"<color=green>GAME OVER | Winner: {winnerName} | Reason: {reason}</color>");

        if (GameOverUI.Instance != null)
        {
            GameOverUI.Instance.ShowWinner(winnerName, reason);
        }
    }










    //abilities
    public void EquipActiveAbility(ulong playerId, NetworkCard card, int turnDuration)
    {
        RemoveActiveAbility(playerId);

        activePlayerAbilities[playerId] = card;
        abilityTurnDuration[playerId] = turnDuration;
        SetPlayerActiveAbilityClientRpc(playerId, card.CardId.ToString());
    }

    public void RemoveActiveAbility(ulong playerId)
    {
        if (!activePlayerAbilities.TryGetValue(playerId, out NetworkCard? ability) || !ability.HasValue) return;

        NetworkCard removed = ability.Value;
        activePlayerAbilities[playerId] = null;
        abilityTurnDuration.Remove(playerId);
        trapImmunePlayers.Remove(playerId);
        pendingZoomiesPlayers.Remove(playerId);

        ClearPlayerActiveAbilityClientRpc(playerId);
        DeckManager.Instance.Discard(removed);

        Debug.Log($"[gm] {removed.CardId} left {GetPlayerName(playerId)}'s active ability slot");
    }

    public void DisarmPlayerActiveAbility(ulong targetId)
    {
        Debug.Log($"[gm] disarm {GetPlayerActiveAbilityId(targetId)} from player {targetId}");
        RemoveActiveAbility(targetId);
    }

    public void SetTrapImmunity(ulong playerId, bool immune)
    {
        if (immune) trapImmunePlayers.Add(playerId);
        else trapImmunePlayers.Remove(playerId);
    }

    public bool HasTrapImmunity(ulong playerId) =>
        trapImmunePlayers.Contains(playerId) &&
        abilityTurnDuration.TryGetValue(playerId, out int turnsLeft) && turnsLeft <= 1;

    public void SetPendingZoomies(ulong playerId, bool hasZoomies)
    {
        if (hasZoomies) pendingZoomiesPlayers.Add(playerId);
        else pendingZoomiesPlayers.Remove(playerId);
    }

    public int GetRemainingActions(ulong playerId)
    {
        return remainingActionsThisTurn.ContainsKey(playerId) ? remainingActionsThisTurn[playerId] : 1;
    }

    public void ConsumeAction(ulong playerId)
    {
        if (remainingActionsThisTurn.ContainsKey(playerId))
        {
            remainingActionsThisTurn[playerId] = Mathf.Max(0, remainingActionsThisTurn[playerId] - 1);
        }
    }

    private IEnumerator DelayStealReaction(ulong thiefId, ulong victimId, int cardIndex)
    {
        yield return new WaitForSeconds(2.2f);
        BeginStealReaction(thiefId, victimId, cardIndex, false);
    }


    //for action log
    [ClientRpc]
    public void LogActionClientRpc(string message)
    {
        ActionLogUI.Instance?.AddLog(message);
    }

    private void TriggerGameOver(ulong winnerId, string reason)
    {
        if (_matchEnded) return;
        _matchEnded = true;
        CurrentState.Value = GameState.GameOver;

        StopAllCoroutines();
        CancelDecisionTimer();
        AnnounceWinnerClientRpc(winnerId, reason);
    }
}