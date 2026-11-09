using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private static readonly string[] NidoReactionTags = { "Ability", "Reveal" };


    public void BeginNido(ulong playerId)
    {
        List<ulong> targets = GetEligibleOpponents(playerId, true);
        if (targets.Count == 0)
        {
            SendNotice(playerId, "<color=red>No opponent with cards to inspect</color>");
            return;
        }

        ForceCloseTurnUIClientRpc(SingleTarget(playerId));

        PendingIslandPick pick = null;
        pick = new PendingIslandPick
        {
            Player = playerId,
            Eligible = targets,
            CancelOnStepReport = true,
            OnChosen = id => StartCoroutine(NidoRoutine(playerId, id)),
            OnCancelled = () => { }
        };
        pendingIslandPick = pick;

        BeginIslandPickClientRpc(targets.ToArray(), "Detective Nido: choose a player to inspect", true, SingleTarget(playerId));
        StartDecisionTimer(playerId, () => TimeoutNidoPick(pick));
    }

    private void TimeoutNidoPick(PendingIslandPick pick)
    {
        if (pendingIslandPick != pick) return;
        pendingIslandPick = null;

        EndIslandPickClientRpc(SingleTarget(pick.Player));
        ForceCloseTurnUIClientRpc(SingleTarget(pick.Player));
        if (IsActionPhaseOf(pick.Player)) PromptActionPhase(pick.Player);
    }

    private IEnumerator NidoRoutine(ulong askerId, ulong targetId)
    {
        CurrentState.Value = GameState.PlayerTurn_Resolving;
        LockShuffle(targetId);
        MarkCatUsed(askerId);

        string askerName = GetPlayerName(askerId);
        string targetName = GetPlayerName(targetId);
        BroadcastCatSpotlightClientRpc(DetectiveCatType.DetectiveNido, $"{askerName} uses Detective Nido on {targetName}!", CatSpotlightSeconds);
        yield return new WaitForSeconds(CatSpotlightSeconds);

        BeginBombastic(askerId, targetId, default, () => AfterAction(askerId, TurnActionKind.Ability),
            reflected: false, maxPicks: 1, fromCat: true);
    }

    //whamos
    private void TriggerStolenTrap(ulong thiefId, ulong victimId, NetworkCard stolenCard, System.Action onDone)
    {
        if (!IsImmuneToTraps(thiefId) && IsCatAvailable(thiefId, DetectiveCatType.BombSquadWhamos, CurrentRound.Value))
        {
            var pick = new PendingCatPick
            {
                Player = thiefId,
                Kind = CatPickKind.WhamosDisarm,
                Offered = new[] { stolenCard },
                OnPicked = _ => StartCoroutine(WhamosDisarmRoutine(thiefId, stolenCard, onDone)),
                OnCancelled = () => TriggerStolenTrapNow(thiefId, victimId, stolenCard, onDone)
            };
            pendingCatPick = pick;

            OpenCatPickClientRpc($"BombSquad Whamos: click the {stolenCard.CardId} to disarm it",
                pick.Offered, SingleTarget(thiefId));
            StartDecisionTimer(thiefId, () => AutoCatPick(pick));
            return;
        }

        TriggerStolenTrapNow(thiefId, victimId, stolenCard, onDone);
    }

    private IEnumerator WhamosDisarmRoutine(ulong thiefId, NetworkCard trap, System.Action onDone)
    {
        MarkCatUsed(thiefId);
        DeckManager.Instance.Discard(trap);

        string thiefName = GetPlayerName(thiefId);
        Debug.Log($"<color=cyan>[whamos] {thiefName} disarmed {trap.CardId}</color>");
        BroadcastCatSpotlightClientRpc(DetectiveCatType.BombSquadWhamos, $"{thiefName} uses Bomb Squad Whamos - {trap.CardId} is disarmed!", ReactionResultSeconds);
        yield return new WaitForSeconds(ReactionResultSeconds);

        onDone?.Invoke();
    }
}