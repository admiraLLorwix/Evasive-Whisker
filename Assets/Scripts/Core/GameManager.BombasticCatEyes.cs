using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private const float BombasticPickSeconds = DecisionSeconds;
    private const float BombasticRevealSeconds = 2f;

    private class PendingBombastic
    {
        public ulong Asker;
        public ulong Target;
        public NetworkCard Card;
        public System.Action OnFinished;
        public List<int> Picked = new List<int>();
        public int PicksTotal;
        public bool AwaitingPick;
        public bool Reflected;
        public bool FromCat;
    }

    private PendingBombastic pendingBombastic;

    public void BeginBombastic(ulong askerId, ulong targetId, NetworkCard card, System.Action onFinished, bool reflected = false, int maxPicks = 2, bool fromCat = false)
    {
        if (!IsServer) return;

        if (!playerHands.TryGetValue(targetId, out var targetHand) || targetHand.Count == 0)
        {
            SendNotice(askerId, $"<color=red>{GetPlayerName(targetId)} has no cards to peek at</color>");
            if (!reflected && !fromCat) DeckManager.Instance.Discard(card);
            onFinished?.Invoke();
            return;
        }
        var pending = new PendingBombastic
        {
            Asker = askerId,
            Target = targetId,
            Card = card,
            OnFinished = onFinished,
            Reflected = reflected,
            FromCat = fromCat,
            PicksTotal = Mathf.Min(maxPicks, targetHand.Count)
        };
        pendingBombastic = pending;
        OpenNextBombasticPick(pending);
    }

    private void OpenNextBombasticPick(PendingBombastic pending)
    {
        pending.AwaitingPick = true;
        List<int> remaining = new List<int>();
        for (int i = 0; i < playerHands[pending.Target].Count; i++)
            if (!pending.Picked.Contains(i)) remaining.Add(i);

        int picksNeeded = pending.PicksTotal;

        if (picksNeeded == 1)
        {
            OpenSinglePeekModalClientRpc(pending.Target, GetPlayerName(pending.Target), GetPlayerDetective(pending.Target), remaining.ToArray(), SingleTarget(pending.Asker));
        }
        else
        {
            OpenMultiPeekModalClientRpc(pending.Target, GetPlayerName(pending.Target), GetPlayerDetective(pending.Target), remaining.ToArray(), SingleTarget(pending.Asker));
        }

        StartCoroutine(BombasticPickTimeoutRoutine(pending));
    }

    [ClientRpc]
    private void OpenSinglePeekModalClientRpc(ulong victimId, string victimName, DetectiveCatType victimCat, int[] remainingVictimIndices, ClientRpcParams clientRpcParams = default)
    {
        StealManager.Instance?.ShowPeekModal(victimId, victimName, victimCat, remainingVictimIndices, 1, 1);
    }

    [ClientRpc]
    private void OpenMultiPeekModalClientRpc(ulong victimId, string victimName, DetectiveCatType victimCat, int[] remainingVictimIndices, ClientRpcParams clientRpcParams = default)
    {
        StealManager.Instance?.ShowMultiPeekModal(victimId, victimName, victimCat, remainingVictimIndices,
            (pickedIndices) => SubmitBombasticMultiPickServerRpc(pickedIndices));
    }


    [Rpc(SendTo.Server)]
    public void SubmitBombasticPickServerRpc(int victimCardIndex, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingBombastic pending = pendingBombastic;

        if (pending == null || !pending.AwaitingPick || senderId != pending.Asker) return;
        if (!playerHands.TryGetValue(pending.Target, out var hand)) return;
        if (victimCardIndex < 0 || victimCardIndex >= hand.Count) return;

        pending.AwaitingPick = false;
        ResolveBombasticMultiPick(pending, new int[] { victimCardIndex });
    }



    [Rpc(SendTo.Server)]
    public void SubmitBombasticMultiPickServerRpc(int[] victimCardIndices, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingBombastic pending = pendingBombastic;
        if (pending == null || !pending.AwaitingPick || senderId != pending.Asker) return;
        if (victimCardIndices == null || victimCardIndices.Length != 2) return;
        pending.AwaitingPick = false;
        ResolveBombasticMultiPick(pending, victimCardIndices);
    }

    private void ResolveBombasticMultiPick(PendingBombastic pending, int[] pickedIndices)
    {
        string askerName = GetPlayerName(pending.Asker);
        string targetName = GetPlayerName(pending.Target);
        List<NetworkCard> seenCards = new List<NetworkCard>();
        foreach (int idx in pickedIndices)
        {
            if (idx >= 0 && idx < playerHands[pending.Target].Count)
            {
                pending.Picked.Add(idx);
                seenCards.Add(playerHands[pending.Target][idx]);
            }
        }
        if (seenCards.Count == 0) { CancelBombastic(pending, false); return; }
        ShowMultiPeekRevealClientRpc(seenCards.ToArray(), askerName, targetName, SingleTarget(pending.Asker));
        ShowMultiPeekRevealClientRpc(seenCards.ToArray(), askerName, targetName, SingleTarget(pending.Target));
        if (!pending.Reflected && !pending.FromCat) DeckManager.Instance.Discard(pending.Card);
        if (pendingBombastic == pending) pendingBombastic = null;
        StartCoroutine(DelayAndFinishBombastic(pending.OnFinished));
    }

    [ClientRpc]
    private void ShowMultiPeekRevealClientRpc(NetworkCard[] cards, string askerName, string targetName, ClientRpcParams clientRpcParams = default)
    {
        HandRevealUI.Instance?.ShowTimed("Peeked Cards", cards, 3f);
    }

    private IEnumerator DelayAndFinishBombastic(System.Action onFinished)
    {
        yield return new WaitForSeconds(3.2f);
        onFinished?.Invoke();
    }



    private void CancelBombastic(PendingBombastic pending, bool endAttackerTurn)
    {
        if (pendingBombastic == pending) pendingBombastic = null;

        if (pending.FromCat)
        {
            if (endAttackerTurn) EndTurnAfterCancel(pending.Asker, TurnActionKind.Ability);
            else pending.OnFinished?.Invoke();
            return;
        }

        CancelAbility(pending.Asker, pending.Card, endAttackerTurn);
    }
    private IEnumerator BombasticPickTimeoutRoutine(PendingBombastic pending)
    {
        yield return new WaitForSeconds(BombasticPickSeconds);

        if (!pending.AwaitingPick) yield break;
        if (!playerHands.TryGetValue(pending.Target, out var hand)) yield break;

        List<int> remaining = new List<int>();
        for (int i = 0; i < hand.Count; i++)
            if (!pending.Picked.Contains(i)) remaining.Add(i);

        if (remaining.Count == 0) yield break;

        int[] autoPicks = new int[Mathf.Min(pending.PicksTotal, remaining.Count)];
        for (int i = 0; i < autoPicks.Length; i++) autoPicks[i] = remaining[i];

        Debug.LogWarning("[bombastic] asker timed out - peeking at random cards");
        pending.AwaitingPick = false;
        ResolveBombasticMultiPick(pending, autoPicks);
    }
    [ClientRpc]
    private void ClosePeekModalClientRpc(ClientRpcParams clientRpcParams = default)
    {
        StealManager.Instance?.CloseStealModal();
    }
}