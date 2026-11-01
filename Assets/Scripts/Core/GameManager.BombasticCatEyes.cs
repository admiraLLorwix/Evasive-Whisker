using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private const float BombasticPickSeconds = 15f;
    private const float BombasticRevealSeconds = 2f;
    private const float BombasticRevealWait = 2.3f;

    private class PendingBombastic
    {
        public ulong Asker;
        public ulong Target;
        public NetworkCard Card;
        public System.Action OnFinished;
        public List<int> Picked = new List<int>();
        public int PicksTotal;
        public int Stage;
        public bool AwaitingPick;
    }

    private PendingBombastic pendingBombastic;

    public void BeginBombastic(ulong askerId, ulong targetId, NetworkCard card, System.Action onFinished)
    {
        if (!IsServer) return;


        if (!playerHands.TryGetValue(targetId, out var targetHand) || targetHand.Count == 0)
        {
            Debug.Log("[bombastic] target has no cards, fizzling");
            SendNotice(askerId, $"<color=yellow>{GetPlayerName(targetId)} has no cards to peek at</color>");
            DeckManager.Instance.Discard(card);
            onFinished?.Invoke();
            return;
        }

        var pending = new PendingBombastic
        {
            Asker = askerId,
            Target = targetId,
            Card = card,
            OnFinished = onFinished,
            PicksTotal = Mathf.Min(2, targetHand.Count)
        };
        pendingBombastic = pending;

        Debug.Log($"<color=magenta>[bombastic] {GetPlayerName(askerId)} peeks at {GetPlayerName(targetId)} ({pending.PicksTotal} pick(s))</color>");
        OpenNextBombasticPick(pending);
    }

    private void OpenNextBombasticPick(PendingBombastic pending)
    {
        pending.Stage++;
        pending.AwaitingPick = true;

        List<int> remaining = new List<int>();
        int handCount = playerHands[pending.Target].Count;
        for (int i = 0; i < handCount; i++)
        {
            if (!pending.Picked.Contains(i)) remaining.Add(i);
        }

        OpenPeekModalClientRpc(
            pending.Target,
            GetPlayerName(pending.Target),
            GetPlayerDetective(pending.Target),
            remaining.ToArray(),
            pending.Picked.Count + 1,
            pending.PicksTotal,
            SingleTarget(pending.Asker));
        StartCoroutine(BombasticPickTimeoutRoutine(pending, pending.Stage));
    }

    [Rpc(SendTo.Server)]
    public void SubmitBombasticPickServerRpc(int victimCardIndex, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingBombastic pending = pendingBombastic;

        if (pending == null || !pending.AwaitingPick || senderId != pending.Asker) return;
        if (!playerHands.TryGetValue(pending.Target, out var hand)) return;
        if (victimCardIndex < 0 || victimCardIndex >= hand.Count || pending.Picked.Contains(victimCardIndex)) return;
        RevealBombasticPick(pending, victimCardIndex);
    }

    private void RevealBombasticPick(PendingBombastic pending, int victimCardIndex)
    {
        pending.AwaitingPick = false;
        pending.Picked.Add(victimCardIndex);

        NetworkCard seen = playerHands[pending.Target][victimCardIndex];
        string cardId = seen.CardId;
        string askerName = GetPlayerName(pending.Asker);
        string targetName = GetPlayerName(pending.Target);

        Debug.Log($"<color=magenta>[bombastic] {askerName} peeked at {targetName}'s {cardId}</color>");
        ClosePeekModalClientRpc(SingleTarget(pending.Asker));
        ShowPeekRevealClientRpc(cardId, $"You peeked at {targetName}'s {cardId}", SingleTarget(pending.Asker));
        ShowPeekRevealClientRpc(cardId, $"{askerName} peeked at your {cardId}!", SingleTarget(pending.Target));

        StartCoroutine(BombasticAfterRevealRoutine(pending));
    }

    private IEnumerator BombasticAfterRevealRoutine(PendingBombastic pending)
    {
        yield return new WaitForSeconds(BombasticRevealWait);

        if (pending.Picked.Count < pending.PicksTotal)
        {
            OpenNextBombasticPick(pending);
            yield break;
        }
        DeckManager.Instance.Discard(pending.Card);
        if (pendingBombastic == pending) pendingBombastic = null;
        pending.OnFinished?.Invoke();
    }
    private IEnumerator BombasticPickTimeoutRoutine(PendingBombastic pending, int stage)
    {
        yield return new WaitForSeconds(BombasticPickSeconds);

        if (pending.Stage != stage || !pending.AwaitingPick) yield break;
        if (!playerHands.TryGetValue(pending.Target, out var hand)) yield break;

        List<int> remaining = new List<int>();
        for (int i = 0; i < hand.Count; i++)
        {
            if (!pending.Picked.Contains(i)) remaining.Add(i);
        }
        if (remaining.Count == 0) yield break;

        Debug.LogWarning("[bombastic] asker timed out - peeking at a random card");
        RevealBombasticPick(pending, remaining[Random.Range(0, remaining.Count)]);
    }
    [ClientRpc]
    private void OpenPeekModalClientRpc(ulong victimId, string victimName, DetectiveCatType victimCat,
                                        int[] remainingVictimIndices, int pickNumber, int pickTotal,
                                        ClientRpcParams clientRpcParams = default)
    {
        StealManager.Instance?.ShowPeekModal(victimId, victimName, victimCat, remainingVictimIndices, pickNumber, pickTotal);
    }

    [ClientRpc]
    private void ClosePeekModalClientRpc(ClientRpcParams clientRpcParams = default)
    {
        StealManager.Instance?.CloseStealModal();
    }

    [ClientRpc]
    private void ShowPeekRevealClientRpc(string cardId, string message, ClientRpcParams clientRpcParams = default)
    {
        Color glow = new Color(0.75f, 0.35f, 1f, 1f);

        if (CardSpotlightController.Instance != null)
        {
            CardSpotlightController.Instance.PlaySpotlight(cardId, message, glow, BombasticRevealSeconds);
        }
        else
        {
            var spotlight = FindAnyObjectByType<CardSpotlightController>(FindObjectsInactive.Include);
            spotlight?.PlaySpotlight(cardId, message, glow, BombasticRevealSeconds);
        }
    }
}