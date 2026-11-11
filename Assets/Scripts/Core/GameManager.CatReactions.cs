using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private const float CatSpotlightSeconds = 2.0f;

    private static bool HasTag(ReactionRequest req, string tag)
    {
        foreach (string t in req.ActionTags) if (t == tag) return true;
        return false;
    }

    private List<ulong> GetRedirectTargets(ReactionRequest req)
    {
        var list = new List<ulong>();
        foreach (ulong id in turnOrder)
        {
            if (id == req.Attacker || id == req.Defender || IsGuessImmune(id)) continue;
            if (!playerHands.TryGetValue(id, out var hand) || hand.Count == 0) continue;
            list.Add(id);
        }
        return list;
    }

    private bool TryGetLegalCatReaction(ReactionRequest req, out DetectiveCatType cat)
    {
        cat = default;
        if (!chosenDetectives.TryGetValue(req.Defender, out cat)) return false;
        if (DetectiveCatRules.Timing(cat) != CatTiming.Reactive) return false;
        if (!IsCatAvailable(req.Defender, cat, CurrentRound.Value)) return false;

        switch (cat)
        {
            case DetectiveCatType.AgentPutol:
                return HasTag(req, "Reveal");

            case DetectiveCatType.SleightOfPawShadow:
                return HasTag(req, "Steal") && req.OnSwapTarget != null && req.TargetIndex >= 0
                    && playerHands.TryGetValue(req.Defender, out var hand) && hand.Count >= 2;

            case DetectiveCatType.DecoyMokang:
                return HasTag(req, "Steal") && !req.Redirected && req.OnRedirect != null
                    && GetRedirectTargets(req).Count > 0;

            default:
                return false;
        }
    }

    [Rpc(SendTo.Server)]
    public void SubmitCatReactionServerRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingReaction pending = pendingReaction;

        if (pending == null || pending.CounterPlayed || pending.CatPlayed || senderId != pending.Request.Defender) return;

        if (!TryGetLegalCatReaction(pending.Request, out DetectiveCatType cat))
        {
            SendNotice(senderId, "<color=red>Your detective cat can't react to this</color>");
            return;
        }

        pending.CatKind = cat;
        pending.CatPlayed = true;
    }

    private IEnumerator HandleCatReaction(PendingReaction pending)
    {
        ReactionRequest req = pending.Request;
        ulong defender = req.Defender;
        string defenderName = GetPlayerName(defender);
        DetectiveCatType cat = pending.CatKind;
        string catName = DetectiveCatRules.DisplayName(cat);
        LogActionClientRpc($"{defenderName} reacted with {catName}!");
        Debug.Log($"<color=red>[reaction] {defenderName} reacts with {catName}</color>");

        switch (cat)
        {
            case DetectiveCatType.AgentPutol:
                MarkCatUsed(defender);
                BroadcastCatSpotlightClientRpc(cat, defender, $"{defenderName} uses {catName} - the reveal is blocked!", CounterplayResultWait);
                yield return new WaitForSeconds(CounterplayResultWait);
                req.OnCancelled?.Invoke(false);
                yield break;

            case DetectiveCatType.SleightOfPawShadow:
                yield return StartCoroutine(ShadowReaction(pending));
                yield break;

            case DetectiveCatType.DecoyMokang:
                yield return StartCoroutine(MokangReaction(pending));
                yield break;
        }

        req.OnProceed?.Invoke();
    }

    private IEnumerator ProceedAfterCatReaction(ReactionRequest req)
    {
        if (req.Reveal != null)
        {
            BroadcastReactionSpotlights(req, true, ReactionResultSeconds);
            yield return new WaitForSeconds(ReactionResultSeconds);
        }
        req.OnProceed?.Invoke();
    }

    private IEnumerator ShadowReaction(PendingReaction pending)
    {
        ReactionRequest req = pending.Request;
        ulong defender = req.Defender;
        string defenderName = GetPlayerName(defender);

        BroadcastCatSpotlightClientRpc(DetectiveCatType.SleightOfPawShadow, defender, $"{defenderName} uses Sleight of Paw Shadow!", CatSpotlightSeconds);
        yield return new WaitForSeconds(CatSpotlightSeconds);

        List<NetworkCard> hand = playerHands[defender];
        var offered = new List<NetworkCard>();
        var handIndices = new List<int>();
        for (int i = 0; i < hand.Count; i++)
        {
            if (i == req.TargetIndex) continue;
            offered.Add(hand[i]);
            handIndices.Add(i);
        }

        bool finished = false;
        int chosenHandIndex = -1;

        var pick = new PendingCatPick
        {
            Player = defender,
            Kind = CatPickKind.ShadowSwap,
            Offered = offered.ToArray(),
            HandIndices = handIndices.ToArray(),
            OnPicked = idx => { chosenHandIndex = idx; finished = true; },
            OnCancelled = () => { finished = true; }
        };
        pendingCatPick = pick;

        OpenCatPickClientRpc("Sleight of Paw Shadow: choose a card to give up instead", pick.Offered, SingleTarget(defender));
        StartDecisionTimer(defender, () => AutoCatPick(pick));

        while (!finished) yield return null;

        if (chosenHandIndex >= 0 && chosenHandIndex < playerHands[defender].Count)
        {
            MarkCatUsed(defender);
            req.OnSwapTarget(chosenHandIndex);
        }

        yield return StartCoroutine(ProceedAfterCatReaction(req));
    }

    private IEnumerator MokangReaction(PendingReaction pending)
    {
        ReactionRequest req = pending.Request;
        ulong defender = req.Defender;
        string defenderName = GetPlayerName(defender);

        BroadcastCatSpotlightClientRpc(DetectiveCatType.DecoyMokang, defender, $"{defenderName} uses Decoy Mokang!", CatSpotlightSeconds);
        yield return new WaitForSeconds(CatSpotlightSeconds);

        List<ulong> targets = GetRedirectTargets(req);
        ulong chosen = 0;
        bool hasChoice = false;
        bool finished = false;

        if (targets.Count > 0)
        {
            var pick = new PendingIslandPick
            {
                Player = defender,
                Eligible = targets,
                OnChosen = id => { chosen = id; hasChoice = true; finished = true; },
                OnCancelled = () => { finished = true; }
            };
            pendingIslandPick = pick;

            BeginIslandPickClientRpc(targets.ToArray(), "Decoy Mokang: choose who takes the hit", false, SingleTarget(defender));
            StartDecisionTimer(defender, () => TimeoutIslandPick(pick));

            while (!finished) yield return null;
            EndIslandPickClientRpc(SingleTarget(defender));
        }

        if (hasChoice)
        {
            MarkCatUsed(defender);
            req.OnRedirect(chosen);
            yield break;
        }

        yield return StartCoroutine(ProceedAfterCatReaction(req));
    }

    private class StealContext
    {
        public ulong Thief;
        public ulong Victim;
        public int Index;
        public NetworkCard Card;
    }

    private ReactionReveal BuildStealReveal(string thiefName, string victimName, NetworkCard card) => new ReactionReveal
    {
        Card = card,
        AttackerWindowMsg = $"Waiting for {victimName} to react...",
        DefenderWindowMsg = $"{thiefName} is stealing {card.CardId} from you!",
        SpectatorWindowMsg = $"{thiefName} is stealing a card from {victimName}!",
        AttackerResultMsg = $"You stole {card.CardId} from {victimName}!",
        DefenderResultMsg = $"{thiefName} stole {card.CardId} from you!",
        SpectatorResultMsg = $"{thiefName} stole a card from {victimName}!"
    };

    private void BeginStealReaction(ulong thiefId, ulong victimId, int cardIndex, bool redirected)
    {
        var ctx = new StealContext
        {
            Thief = thiefId,
            Victim = victimId,
            Index = cardIndex,
            Card = playerHands[victimId][cardIndex]
        };

        string thiefName = GetPlayerName(thiefId);
        string victimName = GetPlayerName(victimId);

        if (!redirected) PlayVersusSpotlightClientRpc(thiefId, victimId, "is attempting to steal from");

        ReactionRequest request = null;
        request = new ReactionRequest
        {
            Attacker = thiefId,
            Defender = victimId,
            ActionTags = StealReactionTags,
            Description = redirected ? $"{thiefName}'s steal was redirected to you" : $"{thiefName} is stealing from you",
            Reveal = BuildStealReveal(thiefName, victimName, ctx.Card),
            Redirected = redirected,
            TargetIndex = cardIndex,

            OnSwapTarget = newIndex =>
            {
                ctx.Index = newIndex;
                ctx.Card = playerHands[ctx.Victim][newIndex];
                request.Reveal = BuildStealReveal(thiefName, victimName, ctx.Card);
            },

            OnRedirect = newVictim => RedirectSteal(ctx, newVictim),

            OnProceed = () => ProceedSteal(ctx.Thief, ctx.Victim, ctx.Index, ctx.Card),
            OnCancelled = endTurn => CancelSteal(ctx.Thief, endTurn)
        };

        StartReactionWindow(request);
    }

    private void RedirectSteal(StealContext ctx, ulong newVictim)
    {
        List<NetworkCard> newHand = playerHands[newVictim];
        if (newHand.Count == 0)
        {
            CancelSteal(ctx.Thief, false);
            return;
        }
        int index = ctx.Index % newHand.Count;

        Debug.Log($"<color=red>[mokang] {GetPlayerName(ctx.Thief)}'s steal redirected from {GetPlayerName(ctx.Victim)} to {GetPlayerName(newVictim)} (card #{index})</color>");

        UnlockShuffle(ctx.Victim);
        LockShuffle(newVictim);
        BeginStealReaction(ctx.Thief, newVictim, index, true);
    }

    private class PendingIslandPick
    {
        public ulong Player;
        public List<ulong> Eligible;
        public System.Action<ulong> OnChosen;
        public System.Action OnCancelled;
        public bool CancelOnStepReport;
    }

    private PendingIslandPick pendingIslandPick;

    [Rpc(SendTo.Server)]
    public void SubmitIslandPickServerRpc(ulong targetId, RpcParams rpcParams = default)
    {
        PendingIslandPick pick = pendingIslandPick;
        if (pick == null || rpcParams.Receive.SenderClientId != pick.Player || !pick.Eligible.Contains(targetId)) return;

        pendingIslandPick = null;
        CancelDecisionTimer();
        pick.OnChosen?.Invoke(targetId);
    }

    private void TimeoutIslandPick(PendingIslandPick pick)
    {
        if (pendingIslandPick != pick) return;
        pendingIslandPick = null;
        Debug.Log("<color=orange>[timer] island pick timed out - cat reaction dropped</color>");
        pick.OnCancelled?.Invoke();
    }

    [ClientRpc]
    private void BeginIslandPickClientRpc(ulong[] eligible, string prompt, bool allowCancel, ClientRpcParams clientRpcParams = default)
    {
        StealManager.Instance?.BeginIslandPick(eligible, prompt, allowCancel);
    }

    [ClientRpc]
    private void EndIslandPickClientRpc(ClientRpcParams clientRpcParams = default)
    {
        StealManager.Instance?.EndIslandPick();
    }
    [ClientRpc]
    private void BroadcastCatSpotlightClientRpc(DetectiveCatType cat, ulong playerId, string message, float duration)
    {
        Color glow = new Color(1f, 0.6f, 0.2f, 1f);
        CardSpotlightController spotlight = CardSpotlightController.Instance != null
            ? CardSpotlightController.Instance
            : FindAnyObjectByType<CardSpotlightController>(FindObjectsInactive.Include);

        spotlight?.PlayCatSpotlight(cat, playerId, message, glow, duration);
    }
}