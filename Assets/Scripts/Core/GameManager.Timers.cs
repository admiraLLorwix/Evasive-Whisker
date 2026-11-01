using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    public const float DecisionSeconds = 10f;
    public enum DecisionStep { ChooseAction = 1, PickAbility = 2, StealOpponent = 3, AccuseTarget = 4, AbilityTarget = 5 }

    private int decisionTimerToken;
    private Coroutine decisionTimerRoutine;

    private void StartDecisionTimer(ulong playerId, System.Action onTimeout, float seconds = DecisionSeconds)
    {
        CancelDecisionTimer(false);
        int token = ++decisionTimerToken;
        decisionTimerRoutine = StartCoroutine(DecisionTimerRoutine(token, seconds, onTimeout));
        ShowDecisionTimerClientRpc(playerId, GetPlayerName(playerId), seconds);
    }

    private void CancelDecisionTimer(bool hideDisplay = true)
    {
        decisionTimerToken++;
        if (decisionTimerRoutine != null)
        {
            StopCoroutine(decisionTimerRoutine);
            decisionTimerRoutine = null;
        }
        if (hideDisplay) HideDecisionTimerClientRpc();
    }

    private IEnumerator DecisionTimerRoutine(int token, float seconds, System.Action onTimeout)
    {
        yield return new WaitForSeconds(seconds);
        if (token != decisionTimerToken) yield break;

        decisionTimerRoutine = null;
        onTimeout?.Invoke();
    }

    private bool IsActionPhaseOf(ulong playerId) =>
        ActiveTurnPlayerId.Value == playerId && CurrentState.Value == GameState.PlayerTurn_Action;

    private List<ulong> GetEligibleOpponents(ulong playerId, bool requireCards)
    {
        var list = new List<ulong>();
        foreach (ulong id in turnOrder)
        {
            if (id == playerId || IsGuessImmune(id)) continue;
            if (requireCards && (!playerHands.TryGetValue(id, out var hand) || hand.Count == 0)) continue;
            list.Add(id);
        }
        return list;
    }

    private List<NetworkCard> GetPlayableAbilityCards(ulong playerId)
    {
        var result = new List<NetworkCard>();
        if (!playerHands.TryGetValue(playerId, out var hand)) return result;

        bool slotFull = GetPlayerActiveAbilityId(playerId) != null;
        bool hasTarget = GetEligibleOpponents(playerId, false).Count > 0;

        foreach (NetworkCard card in hand)
        {
            if (card.Type != CardType.Ability) continue;
            if (!CardEffectRegistry.HasAbility(card.CardId)) continue;
            if (CardEffectRegistry.OccupiesSlot(card.CardId) && slotFull) continue;
            if (CardEffectRegistry.RequiresTarget(card.CardId) && !hasTarget) continue;
            result.Add(card);
        }
        return result;
    }

    private bool HandHasPlayableAbility(ulong playerId) => GetPlayableAbilityCards(playerId).Count > 0;
    private void StartDraftTimer(ulong chooserId) => StartDecisionTimer(chooserId, () => AutoPickCat(chooserId));
    private void StartDrawTimer(ulong playerId) => StartDecisionTimer(playerId, () => AutoDraw(playerId));
    private void StartActionTimer(ulong playerId) => StartDecisionTimer(playerId, () => AutoChooseAction(playerId));
    private void BeginDiscardTimer(ulong playerId) => StartDecisionTimer(playerId, () => AutoDiscard(playerId));

    [Rpc(SendTo.Server)]
    public void ReportDecisionStepServerRpc(int step, string cardId, RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!IsActionPhaseOf(sender)) return;

        switch ((DecisionStep)step)
        {
            case DecisionStep.ChooseAction: StartActionTimer(sender); break;
            case DecisionStep.PickAbility: StartDecisionTimer(sender, () => AutoPlayAbility(sender)); break;
            case DecisionStep.StealOpponent: StartDecisionTimer(sender, () => AutoStealOpponent(sender)); break;
            case DecisionStep.AccuseTarget: StartDecisionTimer(sender, () => AutoAccuseStep(sender)); break;
            case DecisionStep.AbilityTarget: StartDecisionTimer(sender, () => AutoAbilityTarget(sender, cardId)); break;
        }
    }

    public void OnStealModalCancelled(ulong thiefId)
    {
        ReleaseStealTargetLock(thiefId);
        if (IsActionPhaseOf(thiefId)) StartActionTimer(thiefId);
    }
    private void AutoPickCat(ulong chooserId)
    {
        if (CurrentState.Value != GameState.DraftingDetectiveCats || availableCatPool.Count == 0) return;
        if (ActiveDraftIndex.Value >= draftOrder.Count || draftOrder[ActiveDraftIndex.Value] != chooserId) return;

        DetectiveCatType cat = availableCatPool[Random.Range(0, availableCatPool.Count)];
        Debug.Log($"<color=orange>[timer] {GetPlayerName(chooserId)} ran out of time - given {cat}</color>");
        ApplyChosenCat(chooserId, cat);
    }

    private void AutoDraw(ulong playerId)
    {
        if (ActiveTurnPlayerId.Value != playerId || CurrentState.Value != GameState.PlayerTurn_Draw) return;
        Debug.Log($"<color=orange>[timer] {GetPlayerName(playerId)} ran out of time - auto draw</color>");
        PerformDraw(playerId);
    }

    private void AutoChooseAction(ulong playerId)
    {
        if (!IsActionPhaseOf(playerId)) return;
        Debug.Log($"<color=orange>[timer] {GetPlayerName(playerId)} ran out of time choosing an action</color>");

        if (firstActionKind.HasValue)
        {
            ForceCloseTurnUIClientRpc(SingleTarget(playerId));
            EndTurnSequence(playerId);
            return;
        }

        if (TryAutoAccuse(playerId)) return;

        var options = new List<int>();
        if (GetPlayableAbilityCards(playerId).Count > 0) options.Add(0);
        if (GetEligibleOpponents(playerId, true).Count > 0) options.Add(1);

        if (options.Count == 0)
        {
            Debug.Log("[timer] nothing legal to do - ending the turn");
            ForceCloseTurnUIClientRpc(SingleTarget(playerId));
            EndTurnAfterCancel(playerId, TurnActionKind.Steal);
            return;
        }

        if (options[Random.Range(0, options.Count)] == 0) AutoPlayAbility(playerId);
        else AutoStealOpponent(playerId);
    }

    private bool TryAutoAccuse(ulong playerId)
    {
        if (CountCustodyCards(playerId) < CustodyCardsRequired) return false;

        List<ulong> targets = GetEligibleOpponents(playerId, false);
        if (targets.Count == 0) return false;

        ulong accused = targets[Random.Range(0, targets.Count)];
        Debug.Log($"<color=orange>[timer] {GetPlayerName(playerId)} auto-accuses {GetPlayerName(accused)}</color>");
        ForceCloseTurnUIClientRpc(SingleTarget(playerId));
        ProcessCustodyGuess(playerId, accused);
        return true;
    }

    private void AutoAccuseStep(ulong playerId)
    {
        if (!IsActionPhaseOf(playerId)) return;
        if (!TryAutoAccuse(playerId)) AutoChooseAction(playerId);
    }

    private void AutoPlayAbility(ulong playerId)
    {
        if (!IsActionPhaseOf(playerId)) return;

        List<NetworkCard> cards = GetPlayableAbilityCards(playerId);
        if (cards.Count == 0)
        {
            AutoChooseAction(playerId);
            return;
        }

        NetworkCard card = cards[Random.Range(0, cards.Count)];
        ulong targetId = NoTarget;

        if (CardEffectRegistry.RequiresTarget(card.CardId))
        {
            List<ulong> targets = GetEligibleOpponents(playerId, false);
            if (targets.Count == 0)
            {
                AutoChooseAction(playerId);
                return;
            }
            targetId = targets[Random.Range(0, targets.Count)];
        }

        Debug.Log($"<color=orange>[timer] {GetPlayerName(playerId)} auto-plays {card.CardId}</color>");
        AutoSubmitAbility(playerId, card, targetId);
    }

    private void AutoAbilityTarget(ulong playerId, string cardId)
    {
        if (!IsActionPhaseOf(playerId)) return;

        NetworkCard card = default;
        bool found = false;
        if (playerHands.TryGetValue(playerId, out var hand))
        {
            foreach (NetworkCard c in hand)
            {
                if (c.CardId == cardId && c.Type == CardType.Ability) { card = c; found = true; break; }
            }
        }

        List<ulong> targets = GetEligibleOpponents(playerId, false);
        if (!found || targets.Count == 0)
        {
            ForceCloseTurnUIClientRpc(SingleTarget(playerId));
            PromptActionPhase(playerId);
            return;
        }

        ulong targetId = targets[Random.Range(0, targets.Count)];
        Debug.Log($"<color=orange>[timer] {GetPlayerName(playerId)} auto-targets {GetPlayerName(targetId)} with {cardId}</color>");
        AutoSubmitAbility(playerId, card, targetId);
    }

    private void AutoSubmitAbility(ulong playerId, NetworkCard card, ulong targetId)
    {
        ForceCloseTurnUIClientRpc(SingleTarget(playerId));
        ProcessPlayAbility(playerId, card, targetId);

        if (CurrentState.Value == GameState.PlayerTurn_Resolving)
            RemoveCardsFromHandClientRpc(new[] { card }, SingleTarget(playerId));
    }

    private void AutoStealOpponent(ulong playerId)
    {
        if (!IsActionPhaseOf(playerId)) return;

        List<ulong> victims = GetEligibleOpponents(playerId, true);
        if (victims.Count == 0)
        {
            AutoChooseAction(playerId);
            return;
        }

        ulong victim = victims[Random.Range(0, victims.Count)];
        Debug.Log($"<color=orange>[timer] {GetPlayerName(playerId)} auto-picks {GetPlayerName(victim)} to steal from</color>");

        ForceCloseTurnUIClientRpc(SingleTarget(playerId));

        if (StealManager.Instance != null && TryBeginStealTargeting(playerId, victim))
            StealManager.Instance.ServerOpenStealModal(playerId, victim);
    }

    private void AutoStealCard(ulong thiefId, ulong victimId)
    {
        if (!IsActionPhaseOf(thiefId)) return;

        CloseThiefStealUIClientRpc(SingleTarget(thiefId));

        if (!playerHands.TryGetValue(victimId, out var victimHand) || victimHand.Count == 0)
        {
            ReleaseStealTargetLock(thiefId);
            PromptActionPhase(thiefId);
            return;
        }

        int index = Random.Range(0, victimHand.Count);
        Debug.Log($"<color=orange>[timer] {GetPlayerName(thiefId)} auto-steals card #{index} from {GetPlayerName(victimId)}</color>");
        ExecuteSteal(thiefId, victimId, index);
    }

    private void AutoDiscard(ulong playerId)
    {
        if (ActiveTurnPlayerId.Value != playerId || CurrentState.Value != GameState.PlayerTurn_DiscardPrompt) return;
        if (!playerHands.TryGetValue(playerId, out var hand)) return;

        List<NetworkCard> candidates = hand.FindAll(c => c.Type != CardType.WhiskerBlower);
        if (candidates.Count == 0) return;

        NetworkCard card = candidates[Random.Range(0, candidates.Count)];
        Debug.Log($"<color=orange>[timer] {GetPlayerName(playerId)} ran out of time - auto-discarding {card.CardId}</color>");

        ForceCloseTurnUIClientRpc(SingleTarget(playerId));
        RemoveCardsFromHandClientRpc(new[] { card }, SingleTarget(playerId));
        ProcessDiscard(playerId, card);
    }
    [ClientRpc]
    private void ForceCloseTurnUIClientRpc(ClientRpcParams clientRpcParams = default)
    {
        TurnActionUI.Instance?.HideAllActions();
        PlayerHandController.Instance?.SetAbilityCardsPlayable(false);
        PlayerHandController.Instance?.HideDiscardConfirm();
        TargetingManager.Instance?.ForceStop();
        StealManager.Instance?.CancelSelection();
    }

    [ClientRpc]
    private void ShowDecisionTimerClientRpc(ulong playerId, string playerName, float seconds)
    {
        bool isMe = NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == playerId;
        DecisionTimerUI.Instance?.Show(isMe, playerName, seconds);
    }

    [ClientRpc]
    private void HideDecisionTimerClientRpc()
    {
        DecisionTimerUI.Instance?.Hide();
    }
}