using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public partial class GameManager
{
    public const int MaxShuffleCharges = 3;
    private const float ShuffleCooldownSeconds = 1.4f;

    private readonly Dictionary<ulong, int> shuffleCharges = new Dictionary<ulong, int>();
    private readonly HashSet<ulong> shuffleLocked = new HashSet<ulong>();
    private readonly Dictionary<ulong, float> lastShuffleTime = new Dictionary<ulong, float>();
    private ulong? pendingStealVictim = null;

    private void InitShuffleCharges()
    {
        shuffleCharges.Clear();
        shuffleLocked.Clear();
        lastShuffleTime.Clear();
        pendingStealVictim = null;

        foreach (ulong id in turnOrder)
        {
            shuffleCharges[id] = MaxShuffleCharges;
            PushShuffleState(id);
        }
    }

    private void ResetShuffleCharges(ulong playerId)
    {
        shuffleCharges[playerId] = MaxShuffleCharges;
        PushShuffleState(playerId);
    }

    private void LockShuffle(ulong playerId)
    {
        if (shuffleLocked.Add(playerId)) PushShuffleState(playerId);
    }

    private void LockShuffleForEveryone()
    {
        foreach (ulong id in turnOrder) LockShuffle(id);
    }

    private void ClearShuffleLocks()
    {
        pendingStealVictim = null;
        if (shuffleLocked.Count == 0) return;

        var released = new List<ulong>(shuffleLocked);
        shuffleLocked.Clear();
        foreach (ulong id in released) PushShuffleState(id);
    }

    public bool TryBeginStealTargeting(ulong thiefId, ulong victimId)
    {
        string error = null;
        if (thiefId != ActiveTurnPlayerId.Value) error = "It's not your turn";
        else if (CurrentState.Value != GameState.PlayerTurn_Action) error = "You can't steal right now";
        else if (victimId == thiefId || !playerHands.ContainsKey(victimId)) error = "Invalid target";
        else if (IsGuessImmune(victimId)) error = GuessImmuneMessage(victimId);

        if (error != null)
        {
            RejectAction(thiefId, error);
            return false;
        }

        ReleaseStealTargetLock(thiefId);
        pendingStealVictim = victimId;
        LockShuffle(victimId);
        StartDecisionTimer(thiefId, () => AutoStealCard(thiefId, victimId));
        return true;
    }
    public void ReleaseStealTargetLock(ulong thiefId)
    {
        if (thiefId != ActiveTurnPlayerId.Value) return;
        if (!pendingStealVictim.HasValue) return;

        ulong victim = pendingStealVictim.Value;
        pendingStealVictim = null;
        if (shuffleLocked.Remove(victim)) PushShuffleState(victim);
    }

    private void PushShuffleState(ulong playerId)
    {
        int charges = shuffleCharges.TryGetValue(playerId, out int c) ? c : 0;
        SyncShuffleStateClientRpc(charges, shuffleLocked.Contains(playerId), SingleTarget(playerId));
    }
    private string ValidateShuffle(ulong playerId)
    {
        if (!playerHands.TryGetValue(playerId, out var hand)) return "You can't shuffle right now";

        switch (CurrentState.Value)
        {
            case GameState.PlayerTurn_Draw:
            case GameState.PlayerTurn_Action:
            case GameState.PlayerTurn_DiscardPrompt:
            case GameState.PlayerTurn_Resolving:
                break;
            default:
                return "You can't shuffle right now";
        }

        if (shuffleLocked.Contains(playerId)) return "You're being targeted - can't shuffle right now";
        if (hand.Count < 2) return "You need at least 2 cards to shuffle";
        if (!shuffleCharges.TryGetValue(playerId, out int charges) || charges <= 0) return "No shuffles left this turn cycle";
        if (lastShuffleTime.TryGetValue(playerId, out float last) && Time.time - last < ShuffleCooldownSeconds)
            return "Still shuffling...";

        return null;
    }

    [Rpc(SendTo.Server)]
    public void RequestShuffleHandServerRpc(RpcParams rpcParams = default)
    {
        ulong playerId = rpcParams.Receive.SenderClientId;
        Debug.Log($"[shuffle][server] request from #{playerId}, state={CurrentState.Value}");

        string error = ValidateShuffle(playerId);
        if (error != null)
        {
            Debug.Log($"[shuffle] rejected #{playerId}: {error}");
            ShuffleRejectedClientRpc($"<color=red>{error}</color>", SingleTarget(playerId));
            return;
        }

        List<NetworkCard> hand = playerHands[playerId];
        ShuffleHandInPlace(hand);
        shuffleCharges[playerId]--;
        lastShuffleTime[playerId] = Time.time;

        Debug.Log($"[shuffle][server] new order for #{playerId}: {string.Join(", ", hand.ConvertAll(c => c.CardId))}");

        ReceiveShuffledHandClientRpc(hand.ToArray(), SingleTarget(playerId));
        PushShuffleState(playerId);
        PlayHandShuffleAnimationClientRpc(playerId);

        Debug.Log($"<color=cyan>[shuffle] {GetPlayerName(playerId)} shuffled ({shuffleCharges[playerId]} left)</color>");
    }
    private void ShuffleHandInPlace(List<NetworkCard> hand)
    {
        NetworkCard[] original = hand.ToArray();

        for (int attempt = 0; attempt < 8; attempt++)
        {
            for (int i = hand.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                NetworkCard tmp = hand[i];
                hand[i] = hand[j];
                hand[j] = tmp;
            }

            bool changed = false;
            for (int i = 0; i < original.Length; i++)
            {
                if (!original[i].Equals(hand[i])) { changed = true; break; }
            }
            if (changed) return;
        }
    }
    [ClientRpc]
    private void ReceiveShuffledHandClientRpc(NetworkCard[] newOrder, ClientRpcParams clientRpcParams = default)
    {
        Debug.Log($"[shuffle][client] received new order ({newOrder.Length} cards), handController={(PlayerHandController.Instance != null)}");
        PlayerHandController.Instance?.ApplyShuffledOrder(newOrder);
    }

    [ClientRpc]
    private void SyncShuffleStateClientRpc(int charges, bool locked, ClientRpcParams clientRpcParams = default)
    {
        ShuffleHandUI.Instance?.SetState(charges, locked);
    }

    [ClientRpc]
    private void ShuffleRejectedClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        ShuffleHandUI.Instance?.ShowBlocked(message);
    }

    [ClientRpc]
    private void PlayHandShuffleAnimationClientRpc(ulong playerId)
    {
        if (playerId == NetworkManager.Singleton.LocalClientId) return;

        PlayerIslandUI island = BoardLayoutManager.Instance != null
            ? BoardLayoutManager.Instance.GetOpponentIsland(playerId)
            : null;
        if (island != null) island.PlayShuffleAnimation();
    }
}