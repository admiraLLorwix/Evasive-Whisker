using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using EvasiveWhisker.Cards;
using System.Linq;

public partial class GameManager
{
    private Dictionary<ulong, ulong> botWhiskerBlowerSuspect = new Dictionary<ulong, ulong>();
    private Dictionary<ulong, int> botMemoryRound = new Dictionary<ulong, int>();

    public bool IsBot(ulong clientId) => clientId >= 1000 && clientId < 2000;

    private void InitializeBotMemory(ulong botId)
    {
        if (!botWhiskerBlowerSuspect.ContainsKey(botId))
            botWhiskerBlowerSuspect[botId] = NoTarget;
        if (!botMemoryRound.ContainsKey(botId))
            botMemoryRound[botId] = 0;
    }

    private void RecordBotMemory(ulong botId, ulong suspectId)
    {
        botWhiskerBlowerSuspect[botId] = suspectId;
        botMemoryRound[botId] = CurrentRound.Value;
    }

    private bool TryGetBotMemory(ulong botId, out ulong suspectId)
    {
        suspectId = NoTarget;
        if (!botWhiskerBlowerSuspect.TryGetValue(botId, out ulong target)) return false;
        if (target == NoTarget) return false;

        int memoryRound = botMemoryRound[botId];
        if (CurrentRound.Value - memoryRound > 2)
        {
            botWhiskerBlowerSuspect[botId] = NoTarget;
            return false;
        }

        suspectId = target;
        return true;
    }

    private IEnumerator BotDraftRoutine(ulong botId)
    {
        yield return new WaitForSeconds(1f);
        if (CurrentState.Value != GameState.DraftingDetectiveCats) yield break;

        DetectiveCatType randomCat = availableCatPool[Random.Range(0, availableCatPool.Count)];
        ApplyChosenCat(botId, randomCat);
    }

    private IEnumerator BotTurnRoutine(ulong botId)
    {
        yield return new WaitForSeconds(1f);
        if (CurrentState.Value == GameState.PlayerTurn_Draw)
        {
            AutoDraw(botId);
            yield return new WaitForSeconds(1.5f);
        }
        if (CurrentState.Value == GameState.PlayerTurn_Action)
        {
            BotChooseAction(botId);
        }
    }

    private void BotChooseAction(ulong botId)
    {
        if (!playerHands.TryGetValue(botId, out var hand)) return;

        if (CountCustodyCards(botId) >= CustodyCardsRequired)
        {
            ulong target = GetEligibleOpponents(botId, false).OrderBy(x => Random.value).FirstOrDefault();
            if (target != NoTarget)
            {
                Debug.Log($"<color=orange>[bot] {GetPlayerName(botId)} accuses {GetPlayerName(target)}</color>");
                ProcessCustodyGuess(botId, target);
                return;
            }
        }

        if (TryGetBotMemory(botId, out ulong suspectedTarget) && GetEligibleOpponents(botId, true).Contains(suspectedTarget))
        {
            Debug.Log($"<color=orange>[bot] {GetPlayerName(botId)} remembers {GetPlayerName(suspectedTarget)} has the Whiskerblower and steals.</color>");
            BeginStealSequence(botId, suspectedTarget);
            return;
        }

        List<NetworkCard> playableCards = GetPlayableAbilityCards(botId);
        if (playableCards.Count > 0)
        {
            NetworkCard cardToPlay = playableCards[0];

            var zoomies = playableCards.FirstOrDefault(c => c.CardId == "Zoomies");
            if (zoomies.CardId != null) cardToPlay = zoomies;

            ulong targetId = NoTarget;
            if (CardEffectRegistry.RequiresTarget(cardToPlay.CardId))
            {
                targetId = GetEligibleOpponents(botId, false).OrderBy(x => Random.value).FirstOrDefault();
                if (targetId == NoTarget) return;
            }

            Debug.Log($"<color=orange>[bot] {GetPlayerName(botId)} plays {cardToPlay.CardId}</color>");
            ProcessPlayAbility(botId, cardToPlay, targetId);
            return;
        }

        var stealTarget = GetEligibleOpponents(botId, true).OrderBy(x => Random.value).FirstOrDefault();
        if (stealTarget != NoTarget)
        {
            Debug.Log($"<color=orange>[bot] {GetPlayerName(botId)} steals from {GetPlayerName(stealTarget)}</color>");
            BeginStealSequence(botId, stealTarget);
        }
        else
        {
            EndTurnSequence(botId);
        }
    }

    private IEnumerator BotReactionRoutine(PendingReaction pending)
    {
        yield return new WaitForSeconds(1.2f);
        if (pending == null || pending.CounterPlayed || pending.CatPlayed) yield break;

        ulong botId = pending.Request.Defender;
        if (!playerHands.TryGetValue(botId, out var hand)) yield break;

        var legalCards = GetLegalCounterCards(botId, pending.Request.ActionTags);
        if (legalCards.Length > 0)
        {
            NetworkCard cardToPlay = legalCards[0];
            hand.Remove(cardToPlay);

            pending.CounterCard = cardToPlay;
            pending.CounterPlayed = true;

            RemoveCardsFromHandClientRpc(new[] { cardToPlay }, SingleTarget(botId));
            UpdateOpponentHandCountClientRpc(botId, hand.Count);
            Debug.Log($"<color=orange>[bot] {GetPlayerName(botId)} auto-countered with {cardToPlay.CardId}</color>");
        }
    }
}