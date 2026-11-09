using UnityEngine;
using EvasiveWhisker.Cards;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Unity.Netcode;

public partial class GameManager
{
    private bool _matchEnded;
    private bool _processingRemovals;
    private readonly Queue<ulong> _pendingRemovals = new Queue<ulong>();

    public bool IsMatchOver() => _matchEnded || (GameOverUI.Instance != null && GameOverUI.Instance.IsShowing());

    public void HandleStateChangedServer(GameState oldState, GameState newState)
    {
        if (newState != GameState.PlayerTurn_Resolving)
            TryProcessPendingRemovals();
    }

    public void HandleServerClientDisconnect(ulong clientId)
    {
        if (!IsServer) return;
        if (_matchEnded) return;

        if (IsWhiskerBlowerHolder(clientId))
        {
            _matchEnded = true;
            WhiskerBlowerLeftClientRpc(clientId);
            return;
        }

        _pendingRemovals.Enqueue(clientId);
        TryProcessPendingRemovals();
    }

    public void HandleClientConnectionLost(ulong clientId)
    {
        if (NetworkManager.Singleton == null) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;
        if (_matchEnded) return;
        if (!IsSpawned) return;

        _matchEnded = true;
        GameOverUI.Instance?.ShowMatchEnded("Lost connection to the host");
    }

    public async Task HostLeaveMatchAsync()
    {
        if (!IsServer) return;
        if (_matchEnded) return;

        _matchEnded = true;
        HostLeavingClientRpc();
        await Task.Delay(500);
        NetworkManager.Singleton.Shutdown();
        await Task.Delay(200);
    }

    public void LeaveMatchClient()
    {
        if (IsServer) return;
        if (_matchEnded) return;

        _matchEnded = true;
        NetworkManager.Singleton.Shutdown();
    }

    [ClientRpc]
    private void HostLeavingClientRpc()
    {
        _matchEnded = true;
        GameOverUI.Instance?.ShowMatchEnded("The host left the match");
    }

    [ClientRpc]
    private void WhiskerBlowerLeftClientRpc(ulong formerHolderId)
    {
        _matchEnded = true;
        string name = GetPlayerName(formerHolderId);
        GameOverUI.Instance?.ShowMatchEnded($"{name} (the WhiskerBlower holder) left the match");
    }

    [ClientRpc]
    private void GreyOutPlayerIslandClientRpc(ulong clientId)
    {
        var island = BoardLayoutManager.Instance != null
            ? BoardLayoutManager.Instance.GetOpponentIsland(clientId)
            : null;
        if (island == null) return;
        island.UpdateCardCount(0);
    }

    private bool IsWhiskerBlowerHolder(ulong clientId)
    {
        if (!playerHands.TryGetValue(clientId, out var hand)) return false;
        return hand.Exists(c => c.Type == CardType.WhiskerBlower);
    }

    private void TryProcessPendingRemovals()
    {
        if (_processingRemovals) return;
        if (!IsServer || _matchEnded) return;
        if (CurrentState.Value == GameState.PlayerTurn_Resolving) return;
        if (_pendingRemovals.Count == 0) return;

        _processingRemovals = true;
        try
        {
            ulong oldActive = ActiveTurnPlayerId.Value;
            bool removedActive = false;
            bool draftInterrupted = false;

            while (_pendingRemovals.Count > 0)
            {
                ulong id = _pendingRemovals.Dequeue();

                if (id == oldActive) removedActive = true;

                if (CurrentState.Value == GameState.DraftingDetectiveCats &&
                    ActiveDraftIndex.Value < draftOrder.Count &&
                    draftOrder[ActiveDraftIndex.Value] == id)
                {
                    draftInterrupted = true;
                }

                CleanUpAndRemove(id);
            }

            if (draftInterrupted && !_matchEnded)
            {
                PromptNextDraftPlayer();
            }
            else if (removedActive && !_matchEnded && CurrentState.Value != GameState.DraftingDetectiveCats)
            {
                if (turnOrder.Count == 0) { _matchEnded = true; return; }
                AdvanceToNextTurn();
            }

            if (turnOrder.Count == 1 && !_matchEnded && CurrentState.Value != GameState.DraftingDetectiveCats)
            {
                _matchEnded = true;
                TriggerGameOver(turnOrder[0], "Last player standing");
            }
        }
        finally
        {
            _processingRemovals = false;
        }
    }

    private void CleanUpAndRemove(ulong clientId)
    {
        Debug.Log($"[gm] removing player #{clientId} from match");

        GreyOutPlayerIslandClientRpc(clientId);

        if (playerHands.TryGetValue(clientId, out var hand))
        {
            foreach (var card in hand) DeckManager.Instance.Discard(card);
            hand.Clear();
            UpdateOpponentHandCountClientRpc(clientId, 0);
        }

        if (activePlayerAbilities.TryGetValue(clientId, out NetworkCard? equipped) && equipped.HasValue)
            DeckManager.Instance.Discard(equipped.Value);

        activePlayerAbilities.Remove(clientId);
        abilityTurnDuration.Remove(clientId);
        trapImmunePlayers.Remove(clientId);
        pendingZoomiesPlayers.Remove(clientId);
        remainingActionsThisTurn.Remove(clientId);

        turnOrder.Remove(clientId);
        draftOrder.Remove(clientId);
    }
}