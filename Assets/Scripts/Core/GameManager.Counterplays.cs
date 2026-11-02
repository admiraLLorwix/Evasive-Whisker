using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private const float CounterStealPickSeconds = DecisionSeconds; //prev 15f

    private class QueuedCounter
    {
        public ulong Defender;
        public ulong Attacker;
        public int Picks = 2;
    }

    private QueuedCounter queuedEyeForAnEye;
    private QueuedCounter queuedCopyCat;

    public void QueueEyeForAnEye(ulong defenderId, ulong attackerId) =>
        queuedEyeForAnEye = new QueuedCounter { Defender = defenderId, Attacker = attackerId };

    public void QueueCopyCat(ulong defenderId, ulong attackerId) =>
        queuedCopyCat = new QueuedCounter
        {
            Defender = defenderId,
            Attacker = attackerId,
            Picks = pendingBombastic != null ? pendingBombastic.PicksTotal : 2
        };

    private bool TryRunQueuedCounterFollowUp(TurnActionKind kind, System.Action resume)
    {
        if (kind == TurnActionKind.Steal && queuedEyeForAnEye != null)
        {
            QueuedCounter q = queuedEyeForAnEye;
            queuedEyeForAnEye = null;
            BeginCounterSteal(q, resume);
            return true;
        }

        if (kind == TurnActionKind.Ability && queuedCopyCat != null)
        {
            QueuedCounter q = queuedCopyCat;
            queuedCopyCat = null;
            BeginReflectedBombastic(q, resume);
            return true;
        }

        return false;
    }
    private void TriggerStolenTrapNow(ulong thiefId, ulong victimId, NetworkCard stolenCard, System.Action onDone)

    {
        string thiefName = GetPlayerName(thiefId);
        string victimName = GetPlayerName(victimId);

        bool thiefImmune = IsImmuneToTraps(thiefId);

        if (thiefImmune)
        {
            string reason = HasTrapImmunity(thiefId) ? "Nine Lives" : "Accusation Protection";
            Debug.Log($"<color=green>[immunity] {thiefName} was immune to traps ({reason}), {stolenCard.CardId} will be discarded</color>");
            BroadcastTrapSpotlightClientRpc(stolenCard.CardId.ToString(), $"TRAP IMMUNITY: {stolenCard.CardId} ({reason})", thiefName, victimName);
        }
        else
        {
            Debug.Log($"<color=red>[trap] Thief {thiefName} triggered {stolenCard.CardId} from {victimName}</color>");
            BroadcastTrapSpotlightClientRpc(stolenCard.CardId.ToString(), $"TRAP TRIGGERED: {stolenCard.CardId}", thiefName, victimName);
        }

        StartCoroutine(ResolveTrapAfterSpotlightRoutine(thiefId, victimId, stolenCard, thiefImmune, onDone));
    }
    private class PendingCounterSteal
    {
        public ulong Defender;
        public ulong Thief;
        public System.Action OnFinished;
        public bool Done;
    }
    private PendingCounterSteal pendingCounterSteal;
    private void BeginCounterSteal(QueuedCounter q, System.Action onFinished)
    {
        string defenderName = GetPlayerName(q.Defender);
        string thiefName = GetPlayerName(q.Attacker);

        if (IsGuessImmune(q.Attacker))
        {
            SendNotice(q.Defender, $"<color=yellow>{GuessImmuneMessage(q.Attacker)} - Eye For An Eye fizzles</color>");
            onFinished?.Invoke();
            return;
        }

        if (!playerHands.TryGetValue(q.Attacker, out var thiefHand) || thiefHand.Count == 0)
        {
            SendNotice(q.Defender, $"<color=yellow>{thiefName} has no cards to steal back</color>");
            onFinished?.Invoke();
            return;
        }

        var pending = new PendingCounterSteal { Defender = q.Defender, Thief = q.Attacker, OnFinished = onFinished };
        pendingCounterSteal = pending;
        LockShuffle(q.Attacker);
        LockShuffle(q.Defender);
        Debug.Log($"<color=red>[eye for an eye] {defenderName} steals back from {thiefName}</color>");
        SendNotice(q.Attacker, $"<color=red>Eye For An Eye! {defenderName} is stealing a card back</color>");
        OpenCounterStealModalClientRpc(q.Attacker, thiefName, GetPlayerDetective(q.Attacker), thiefHand.Count, SingleTarget(q.Defender));
        ShowDecisionTimerClientRpc(q.Defender, defenderName, CounterStealPickSeconds);
        StartCoroutine(CounterStealTimeoutRoutine(pending));
    }

    [Rpc(SendTo.Server)]
    public void SubmitCounterStealPickServerRpc(int cardIndex, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingCounterSteal pending = pendingCounterSteal;

        if (pending == null || pending.Done || senderId != pending.Defender) return;
        if (!playerHands.TryGetValue(pending.Thief, out var hand) || cardIndex < 0 || cardIndex >= hand.Count) return;

        ExecuteCounterSteal(pending, cardIndex);
    }

    private void ExecuteCounterSteal(PendingCounterSteal pending, int cardIndex)
    {
        if (pending.Done) return;
        pending.Done = true;
        HideDecisionTimerClientRpc();

        List<NetworkCard> thiefHand = playerHands[pending.Thief];
        NetworkCard card = thiefHand[cardIndex];
        thiefHand.RemoveAt(cardIndex);

        RemoveStolenCardClientRpc(card, SingleTarget(pending.Thief));
        UpdateOpponentHandCountClientRpc(pending.Thief, thiefHand.Count);

        StartCoroutine(CounterStealResultRoutine(pending, card));
    }
    private IEnumerator CounterStealResultRoutine(PendingCounterSteal pending, NetworkCard card)
    {
        string defenderName = GetPlayerName(pending.Defender);
        string thiefName = GetPlayerName(pending.Thief);

        var view = new ReactionRequest
        {
            Attacker = pending.Defender,
            Defender = pending.Thief,
            Reveal = new ReactionReveal
            {
                Card = card,
                AttackerResultMsg = $"You stole {card.CardId} back from {thiefName}!",
                DefenderResultMsg = $"{defenderName} stole {card.CardId} back from you!",
                SpectatorResultMsg = $"{defenderName} stole a card back from {thiefName}!"
            }
        };
        BroadcastReactionSpotlights(view, true, ReactionResultSeconds);
        yield return new WaitForSeconds(ReactionResultSeconds);

        if (card.Type != CardType.Trap)
        {
            List<NetworkCard> defenderHand = playerHands[pending.Defender];
            defenderHand.Add(card);
            UpdateOpponentHandCountClientRpc(pending.Defender, defenderHand.Count);
            ReceiveDrawnCardClientRpc(card, SingleTarget(pending.Defender));
            FinishCounterSteal(pending);
            yield break;
        }

        TriggerStolenTrap(pending.Defender, pending.Thief, card, () => FinishCounterSteal(pending));
    }

    private void FinishCounterSteal(PendingCounterSteal pending)
    {
        if (pendingCounterSteal == pending) pendingCounterSteal = null;
        pending.OnFinished?.Invoke();
    }

    private IEnumerator CounterStealTimeoutRoutine(PendingCounterSteal pending)
    {
        yield return new WaitForSeconds(CounterStealPickSeconds);

        if (pending.Done) yield break;
        if (!playerHands.TryGetValue(pending.Thief, out var hand) || hand.Count == 0) yield break;

        Debug.LogWarning("[eye for an eye] defender timed out - stealing a random card");
        ClosePeekModalClientRpc(SingleTarget(pending.Defender));
        ExecuteCounterSteal(pending, Random.Range(0, hand.Count));
    }

    [ClientRpc]
    private void OpenCounterStealModalClientRpc(ulong thiefId, string thiefName, DetectiveCatType thiefCat, int cardCount, ClientRpcParams clientRpcParams = default)
    {
        StealManager.Instance?.ShowCounterStealModal(thiefId, thiefName, thiefCat, cardCount);
    }
    private void BeginReflectedBombastic(QueuedCounter q, System.Action onFinished)
    {
        LockShuffle(q.Attacker);
        SendNotice(q.Attacker, $"<color=red>Copy Cat! {GetPlayerName(q.Defender)} reflects the peek back at you</color>");
        BeginBombastic(q.Defender, q.Attacker, default, onFinished, reflected: true, maxPicks: q.Picks);
    }
}