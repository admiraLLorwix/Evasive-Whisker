using System.Collections;
using UnityEngine;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private const float CatInstinctAnswerSeconds = 20f;
    private const float CatInstinctResultSeconds = 4f;

    private class PendingCatInstinct
    {
        public ulong Asker;
        public ulong Target;
        public NetworkCard Card;
        public System.Action OnFinished;
        public bool Answered;
    }

    private PendingCatInstinct pendingCatInstinct;

    public void BeginCatInstinct(ulong askerId, ulong targetId, NetworkCard card, System.Action onFinished)
    {
        if (!IsServer) return;

        if (!playerHands.ContainsKey(targetId))
        {
            Debug.LogWarning($"[cat instinct] target #{targetId} is gone");
            DeckManager.Instance.Discard(card);
            onFinished?.Invoke();
            return;
        }

        var pending = new PendingCatInstinct
        {
            Asker = askerId,
            Target = targetId,
            Card = card,
            OnFinished = onFinished,
            Answered = false
        };
        pendingCatInstinct = pending;

        string askerName = GetPlayerName(askerId);
        string targetName = GetPlayerName(targetId);
        Debug.Log($"<color=cyan>[cat instinct] {askerName} asks {targetName} if they hold the WhiskerBlower</color>");

        OpenCatInstinctPromptClientRpc(askerName, CatInstinctAnswerSeconds, SingleTarget(targetId));
        SendNotice(askerId, $"<color=blue>Waiting for {targetName} to answer...</color>");

        StartCoroutine(CatInstinctTimeoutRoutine(pending));
    }

    [Rpc(SendTo.Server)]
    public void SubmitCatInstinctAnswerServerRpc(bool saysYes, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingCatInstinct pending = pendingCatInstinct;

        if (pending == null || pending.Answered || senderId != pending.Target) return;

        ResolveCatInstinct(pending, saysYes);
    }

    private void ResolveCatInstinct(PendingCatInstinct pending, bool saysYes)
    {
        pending.Answered = true;

        bool actuallyHolds = TargetHoldsWhiskerBlower(pending.Target);
        bool lying = saysYes != actuallyHolds;

        Debug.Log($"<color=cyan>[cat instinct] {GetPlayerName(pending.Target)} said {(saysYes ? "YES" : "NO")} " +
                  $"(holds={actuallyHolds}) -> {(lying ? "LYING" : "truthful")}</color>");

        CloseCatInstinctPromptClientRpc(SingleTarget(pending.Target));
        ShowCatInstinctResultClientRpc(GetPlayerName(pending.Target), saysYes, lying, SingleTarget(pending.Asker));

        StartCoroutine(FinishCatInstinctRoutine(pending));
    }

    private IEnumerator FinishCatInstinctRoutine(PendingCatInstinct pending)
    {
        yield return new WaitForSeconds(CatInstinctResultSeconds);

        CloseCatInstinctResultClientRpc(SingleTarget(pending.Asker));
        DeckManager.Instance.Discard(pending.Card);

        if (pendingCatInstinct == pending) pendingCatInstinct = null;
        pending.OnFinished?.Invoke();
    }

    private IEnumerator CatInstinctTimeoutRoutine(PendingCatInstinct pending)
    {
        yield return new WaitForSeconds(CatInstinctAnswerSeconds);

        if (pending.Answered) yield break;

        Debug.LogWarning("[cat instinct] target timed out - answering truthfully");
        ResolveCatInstinct(pending, TargetHoldsWhiskerBlower(pending.Target));
    }

    private bool TargetHoldsWhiskerBlower(ulong playerId) =>
        playerHands.TryGetValue(playerId, out var hand) && hand.Exists(c => c.Type == CardType.WhiskerBlower);




    [ClientRpc]
    private void OpenCatInstinctPromptClientRpc(string askerName, float seconds, ClientRpcParams clientRpcParams = default)
    {
        CatInstinctUI.Instance?.ShowQuestion(askerName, seconds);
    }

    [ClientRpc]
    private void CloseCatInstinctPromptClientRpc(ClientRpcParams clientRpcParams = default)
    {
        CatInstinctUI.Instance?.HideQuestion();
    }

    [ClientRpc]
    private void ShowCatInstinctResultClientRpc(string targetName, bool saysYes, bool lying, ClientRpcParams clientRpcParams = default)
    {
        CatInstinctUI.Instance?.ShowResult(targetName, saysYes, lying);
    }

    [ClientRpc]
    private void CloseCatInstinctResultClientRpc(ClientRpcParams clientRpcParams = default)
    {
        CatInstinctUI.Instance?.HideResult();
    }
}