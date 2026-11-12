using System.Collections;
using UnityEngine;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private const float HandRevealSeconds = 6f;
    private const float TrapGivePickSeconds = DecisionSeconds;//prev 25f
    private const float TrapGiveResultWait = 2.6f;
    private const float TurnSkipSeconds = 2.8f;

    public void BeginCatFlash(ulong thiefId, ulong victimId, NetworkCard trapCard, System.Action onFinished)
    {
        LogActionClientRpc($"{GetPlayerName(victimId)} triggered a Cat Flash Trap!");
        if (!IsServer) return;

        string thiefName = GetPlayerName(thiefId);
        NetworkCard[] thiefHand = playerHands[thiefId].ToArray();

        Debug.Log($"<color=red>[cat flash] {GetPlayerName(victimId)} sees {thiefName}'s hand ({thiefHand.Length} cards)</color>");

        string title = thiefHand.Length > 0 ? $"{thiefName}'s hand" : $"{thiefName} has no cards";
        ShowHandRevealClientRpc(title, thiefHand, SingleTarget(victimId));
        SendNotice(thiefId, $"<color=red>Cat Flash! {GetPlayerName(victimId)} can see your whole hand</color>");

        StartCoroutine(CatFlashRoutine(victimId, trapCard, onFinished));
    }

    private IEnumerator CatFlashRoutine(ulong victimId, NetworkCard trapCard, System.Action onFinished)
    {
        yield return new WaitForSeconds(HandRevealSeconds);

        CloseHandRevealClientRpc(SingleTarget(victimId));
        DeckManager.Instance.Discard(trapCard);
        onFinished?.Invoke();
    }

    [ClientRpc]
    private void ShowHandRevealClientRpc(string title, NetworkCard[] cards, ClientRpcParams clientRpcParams = default)
    {
        HandRevealUI.Instance?.Show(title, cards);
    }

    [ClientRpc]
    private void CloseHandRevealClientRpc(ClientRpcParams clientRpcParams = default)
    {
        HandRevealUI.Instance?.Hide();
    }








    private class PendingTrapGive
    {
        public ulong Thief;
        public ulong Recipient;
        public NetworkCard Card;
        public System.Action OnFinished;
        public bool Done;
    }

    private PendingTrapGive pendingTrapGive;

    public ulong GetRobinPawRecipient(ulong thiefId)
    {
        ulong best = thiefId;
        int bestCount = int.MaxValue;
        int start = turnOrder.IndexOf(thiefId);

        for (int step = 1; step < turnOrder.Count; step++)
        {
            ulong id = turnOrder[(start + step) % turnOrder.Count];
            if (IsGuessImmune(id)) continue;
            int count = playerHands.TryGetValue(id, out var hand) ? hand.Count : int.MaxValue;

            if (count < bestCount)
            {
                best = id;
                bestCount = count;
            }
        }

        return best;
    }

    public void BeginTrapGive(ulong thiefId, ulong recipientId, NetworkCard trapCard, System.Action onFinished)
    {
        if (!IsServer) return;

        if (recipientId == thiefId || !playerHands.TryGetValue(thiefId, out var thiefHand) || thiefHand.Count == 0)
        {
            Debug.Log($"[trap] {trapCard.CardId} fizzles: thief has no card to give");
            SendNotice(thiefId, $"<color=red>{trapCard.CardId}: you have no cards to give</color>");
            DeckManager.Instance.Discard(trapCard);
            onFinished?.Invoke();
            return;
        }

        var pending = new PendingTrapGive
        {
            Thief = thiefId,
            Recipient = recipientId,
            Card = trapCard,
            OnFinished = onFinished
        };
        pendingTrapGive = pending;

        Debug.Log($"<color=red>[trap] {trapCard.CardId}: {GetPlayerName(thiefId)} must give a card to {GetPlayerName(recipientId)}</color>");

        OpenTrapGivePickClientRpc(trapCard.CardId, GetPlayerName(recipientId), SingleTarget(thiefId));
        ShowDecisionTimerClientRpc(thiefId, GetPlayerName(thiefId), TrapGivePickSeconds);
        StartCoroutine(TrapGiveTimeoutRoutine(pending));
    }

    [Rpc(SendTo.Server)]
    public void SubmitTrapGivePickServerRpc(NetworkCard card, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingTrapGive pending = pendingTrapGive;

        if (pending == null || pending.Done || senderId != pending.Thief) return;

        if (!playerHands.TryGetValue(senderId, out var hand) || !hand.Contains(card))
        {
            SendNotice(senderId, "<color=red>That card isn't in your hand</color>");
            ReopenScatterPickClientRpc(SingleTarget(senderId));   // same "choose again" as Scatter
            return;
        }

        ExecuteTrapGive(pending, card);
    }

    private void ExecuteTrapGive(PendingTrapGive pending, NetworkCard given)
    {
        if (pending.Done) return;
        pending.Done = true;
        HideDecisionTimerClientRpc();

        playerHands[pending.Thief].Remove(given);
        playerHands[pending.Recipient].Add(given);

        UpdateOpponentHandCountClientRpc(pending.Thief, playerHands[pending.Thief].Count);
        UpdateOpponentHandCountClientRpc(pending.Recipient, playerHands[pending.Recipient].Count);

        Debug.Log($"<color=red>[trap] {GetPlayerName(pending.Thief)} gave {given.CardId} to {GetPlayerName(pending.Recipient)}</color>");

        ScatterResultClientRpc(true, given, false, default, 0, SingleTarget(pending.Thief));
        ScatterResultClientRpc(false, default, true, given, pending.Thief, SingleTarget(pending.Recipient));

        StartCoroutine(FinishTrapGiveRoutine(pending));
    }

    private IEnumerator FinishTrapGiveRoutine(PendingTrapGive pending)
    {
        yield return new WaitForSeconds(TrapGiveResultWait);

        DeckManager.Instance.Discard(pending.Card);
        if (pendingTrapGive == pending) pendingTrapGive = null;
        pending.OnFinished?.Invoke();
    }

    private IEnumerator TrapGiveTimeoutRoutine(PendingTrapGive pending)
    {
        yield return new WaitForSeconds(TrapGivePickSeconds);

        if (pending.Done) yield break;
        if (!playerHands.TryGetValue(pending.Thief, out var hand) || hand.Count == 0) yield break;

        var safeChoices = hand.FindAll(c => c.Type != CardType.WhiskerBlower);
        var pool = safeChoices.Count > 0 ? safeChoices : hand;

        Debug.LogWarning("[trap] thief timed out - giving a random card");
        ExecuteTrapGive(pending, pool[Random.Range(0, pool.Count)]);
    }

    [ClientRpc]
    private void OpenTrapGivePickClientRpc(string trapName, string recipientName, ClientRpcParams clientRpcParams = default)
    {
        PlayerHandController.Instance?.BeginTrapGivePick(trapName, recipientName);
    }

    private bool ShouldSkipTurnForCatNap(ulong playerId) =>
        activePlayerAbilities.TryGetValue(playerId, out NetworkCard? slot) && slot.HasValue &&
        slot.Value.CardId == "CatNap" &&
        abilityTurnDuration.TryGetValue(playerId, out int turnsLeft) && turnsLeft <= 1;

    private IEnumerator SkipTurnRoutine(ulong playerId)
    {
        CurrentState.Value = GameState.PlayerTurn_Resolving;
        ActiveTurnPlayerId.Value = playerId;
        hasTakenActionThisTurn = false;
        firstActionKind = null;
        remainingActionsThisTurn[playerId] = 0;

        string playerName = GetPlayerName(playerId);
        Debug.Log($"<color=cyan>[cat nap] {playerName}'s turn is skipped</color>");
        ShowTurnSkippedClientRpc("CatNap", playerId, $"{playerName} is napping - turn skipped!");

        yield return new WaitForSeconds(TurnSkipSeconds);
        AdvanceToNextTurn();
    }

    [ClientRpc]
    private void ShowTurnSkippedClientRpc(string cardId, ulong playerId, string message)
    {
        Color glow = new Color(0.4f, 0.6f, 1f, 1f);

        if (CardSpotlightController.Instance != null)
        {
            CardSpotlightController.Instance.PlaySpotlight(cardId, playerId, message, glow, 2.5f);
        }
        else
        {
            var spotlight = FindAnyObjectByType<CardSpotlightController>(FindObjectsInactive.Include);
            spotlight?.PlaySpotlight(cardId, playerId, message, glow, 2.5f);
        }
    }


    private void TriggerStolenTrapNow(ulong thiefId, ulong victimId, NetworkCard stolenCard, System.Action onDone)
    {
        StartCoroutine(TrapSequenceRoutine(thiefId, victimId, stolenCard, onDone));
    }

    private IEnumerator TrapSequenceRoutine(ulong thiefId, ulong victimId, NetworkCard stolenCard, System.Action onDone)
    {
        string thiefName = GetPlayerName(thiefId);
        string victimName = GetPlayerName(victimId);
        bool thiefImmune = IsImmuneToTraps(thiefId);

        if (!thiefImmune)
        {
            PlayHurtSequenceClientRpc(thiefId);
            yield return new WaitForSeconds(1.2f);
            StopVersusSpotlightClientRpc();
            yield return new WaitForSeconds(0.5f);
        }

        if (thiefImmune)
        {
            string reason = HasTrapImmunity(thiefId) ? "Nine Lives" : "Accusation Protection";
            BroadcastTrapSpotlightClientRpc(stolenCard.CardId.ToString(), thiefId, $"TRAP IMMUNITY: {stolenCard.CardId} ({reason})", thiefName, victimName);
        }
        else
        {
            BroadcastTrapSpotlightClientRpc(stolenCard.CardId.ToString(), thiefId, $"TRAP TRIGGERED: {stolenCard.CardId}", thiefName, victimName);
        }

        yield return new WaitForSeconds(3.5f);

        if (thiefImmune)
        {
            DeckManager.Instance.Discard(stolenCard);
            onDone?.Invoke();
            yield break;
        }

        bool finished = CardEffectRegistry.TriggerTrap(stolenCard.CardId, thiefId, victimId, stolenCard, onDone);
        if (finished) onDone?.Invoke();
    }

    [ClientRpc]
    private void PlayHurtSequenceClientRpc(ulong playerId)
    {
        CardSpotlightController.Instance?.PlayHurtAnimation(playerId);
    }
}