using System.Collections;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using EvasiveWhisker.Cards;


public partial class GameManager
{
    public const float ReactionWindowSeconds = 3f;
    public const float ReactionResultSeconds = 2.5f;
    private const float CounterplayResultWait = 2.6f;
    private static readonly string[] StealReactionTags = { "Steal" };

    private static string[] AbilityReactionTags(string cardId) =>
        cardId == "BombasticCatEyes" ? new[] { "Ability", "Reveal" } : new[] { "Ability" };
    private class ReactionReveal
    {
        public NetworkCard Card;
        public string AttackerWindowMsg, DefenderWindowMsg, SpectatorWindowMsg;
        public string AttackerResultMsg, DefenderResultMsg, SpectatorResultMsg;
    }
    private class ReactionRequest
    {
        public ReactionReveal Reveal;
        public ulong Attacker;
        public ulong Defender;
        public string[] ActionTags;
        public string Description;
        public System.Action OnProceed;
        public System.Action<bool> OnCancelled;
    }

    private class PendingReaction
    {
        public ReactionRequest Request;
        public bool CounterPlayed;
        public NetworkCard CounterCard;
        public bool Cancelled;
        public bool EndAttackerTurn;
    }

    private PendingReaction pendingReaction;
    private PendingReaction counteredReaction;

    private void StartReactionWindow(ReactionRequest request)
    {
        var pending = new PendingReaction { Request = request };
        pendingReaction = pending;
        StartCoroutine(ReactionWindowRoutine(pending));
    }

    private IEnumerator ReactionWindowRoutine(PendingReaction pending)
    {
        ReactionRequest req = pending.Request;
        if (req.Reveal != null) BroadcastReactionSpotlights(req, false, ReactionWindowSeconds);
        NetworkCard[] legalCards = GetLegalCounterCards(req.Defender, req.ActionTags);
        bool prompted = legalCards.Length > 0;
        if (prompted)
        {
            OpenReactionPromptClientRpc(req.Description, legalCards, ReactionWindowSeconds, SingleTarget(req.Defender));
        }

        float endTime = Time.time + ReactionWindowSeconds;
        while (Time.time < endTime && !pending.CounterPlayed)
        {
            yield return null;
        }

        if (pendingReaction == pending) pendingReaction = null;
        if (prompted) CloseReactionPromptClientRpc(SingleTarget(req.Defender));

        if (!pending.CounterPlayed)
        {
            if (req.Reveal != null)
            {
                BroadcastReactionSpotlights(req, true, ReactionResultSeconds);
                yield return new WaitForSeconds(ReactionResultSeconds);
            }
            req.OnProceed?.Invoke();
            yield break;
        }

        string defenderName = GetPlayerName(req.Defender);
        Debug.Log($"<color=red>[reaction] {defenderName} counters with {pending.CounterCard.CardId}</color>");
        BroadcastCounterplaySpotlightClientRpc(pending.CounterCard.CardId, $"{defenderName} played {pending.CounterCard.CardId}!", CounterplayResultWait);
        yield return new WaitForSeconds(CounterplayResultWait);

        counteredReaction = pending;
        bool applied = false;
        foreach (string tag in req.ActionTags)
        {
            if (CardEffectRegistry.TryCounter(pending.CounterCard.CardId, tag, req.Defender, req.Attacker, pending.CounterCard))
            {
                applied = true;
                break;
            }
        }
        counteredReaction = null;

        if (applied && pending.Cancelled)
        {
            req.OnCancelled?.Invoke(pending.EndAttackerTurn);
            yield break;
        }

        // countered but not cancelled (e.g. a counter with a side effect): the action still resolves
        if (req.Reveal != null)
        {
            BroadcastReactionSpotlights(req, true, ReactionResultSeconds);
            yield return new WaitForSeconds(ReactionResultSeconds);
        }
        req.OnProceed?.Invoke();
    }

    private void BroadcastReactionSpotlights(ReactionRequest req, bool result, float seconds)
    {
        ReactionReveal r = req.Reveal;
        string cardId = r.Card.CardId;

        ShowStealSpotlightClientRpc(result ? cardId : "", result ? r.AttackerResultMsg : r.AttackerWindowMsg,
            !result, seconds, SingleTarget(req.Attacker));

        ShowStealSpotlightClientRpc(cardId, result ? r.DefenderResultMsg : r.DefenderWindowMsg,
            false, seconds, SingleTarget(req.Defender));

        var spectatorIds = new List<ulong>();
        foreach (var client in NetworkManager.Singleton.ConnectedClientsList)
        {
            if (client.ClientId != req.Attacker && client.ClientId != req.Defender) spectatorIds.Add(client.ClientId);
        }
        if (spectatorIds.Count > 0)
        {
            ShowStealSpotlightClientRpc("", result ? r.SpectatorResultMsg : r.SpectatorWindowMsg, true, seconds,
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = spectatorIds.ToArray() } });
        }
    }

    private NetworkCard[] GetLegalCounterCards(ulong defenderId, string[] actionTags)
    {
        var legal = new List<NetworkCard>();
        if (!playerHands.TryGetValue(defenderId, out var hand)) return legal.ToArray();

        foreach (NetworkCard card in hand)
        {
            if (card.Type != CardType.Counterplay) continue;
            if (legal.Exists(c => c.CardId == card.CardId)) continue;
            if (CanCounterAny(card.CardId, actionTags)) legal.Add(card);
        }
        return legal.ToArray();
    }

    private bool CanCounterAny(string counterCardId, string[] actionTags)
    {
        foreach (string tag in actionTags)
        {
            if (CardEffectRegistry.CanCounter(counterCardId, tag)) return true;
        }
        return false;
    }

    [Rpc(SendTo.Server)]
    public void SubmitReactionServerRpc(NetworkCard card, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingReaction pending = pendingReaction;

        if (pending == null || pending.CounterPlayed || senderId != pending.Request.Defender) return;

        if (card.Type != CardType.Counterplay || !CanCounterAny(card.CardId, pending.Request.ActionTags))
        {
            SendNotice(senderId, "<color=red>That card can't counter this</color>");
            return;
        }

        if (!playerHands.TryGetValue(senderId, out var hand) || !hand.Remove(card)) return;

        pending.CounterCard = card;
        pending.CounterPlayed = true;

        RemoveCardsFromHandClientRpc(new[] { card }, SingleTarget(senderId));
        UpdateOpponentHandCountClientRpc(senderId, hand.Count);
    }

    public void CancelPendingReaction(bool endAttackerTurn)
    {
        if (counteredReaction == null) return;
        counteredReaction.Cancelled = true;
        counteredReaction.EndAttackerTurn = endAttackerTurn;
    }

    private void EndTurnAfterCancel(ulong attackerId, TurnActionKind kind)
    {
        ClearShuffleLocks();
        CloseThiefStealUIClientRpc(SingleTarget(attackerId));

        if (!firstActionKind.HasValue) firstActionKind = kind;
        hasTakenActionThisTurn = true;
        remainingActionsThisTurn[attackerId] = 0;

        EndTurnSequence(attackerId);
    }


    [ClientRpc]
    private void OpenReactionPromptClientRpc(string description, NetworkCard[] legalCards, float seconds, ClientRpcParams clientRpcParams = default)
    {
        PlayerHandController.Instance?.BeginReaction(description, legalCards, seconds);
    }

    [ClientRpc]
    private void CloseReactionPromptClientRpc(ClientRpcParams clientRpcParams = default)
    {
        PlayerHandController.Instance?.EndReaction();
    }

    [ClientRpc]
    private void BroadcastCounterplaySpotlightClientRpc(string cardId, string message, float duration)
    {
        Color glow = new Color(1f, 0.3f, 0.3f, 1f);

        if (CardSpotlightController.Instance != null)
        {
            CardSpotlightController.Instance.PlaySpotlight(cardId, message, glow, duration);
        }
        else
        {
            var spotlight = FindAnyObjectByType<CardSpotlightController>(FindObjectsInactive.Include);
            spotlight?.PlaySpotlight(cardId, message, glow, duration);
        }
    }
}