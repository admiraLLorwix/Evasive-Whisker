using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private readonly Dictionary<ulong, int> catLastUsedRound = new Dictionary<ulong, int>();

    private enum CatPickKind { Shimo, Bebang, ShadowSwap, WhamosDisarm }

    private class PendingCatPick
    {
        public ulong Player;
        public CatPickKind Kind;
        public NetworkCard[] Offered;
        public int[] HandIndices;
        public System.Action<int> OnPicked;
        public System.Action OnCancelled;
    }
    private PendingCatPick pendingCatPick;


    public bool IsCatAvailable(ulong playerId, DetectiveCatType cat, int round)
    {
        if (!chosenDetectives.TryGetValue(playerId, out DetectiveCatType mine) || mine != cat) return false;
        catLastUsedRound.TryGetValue(playerId, out int last);
        return DetectiveCatRules.IsAvailable(cat, round, last);
    }

    private void MarkCatUsed(ulong playerId)
    {
        catLastUsedRound[playerId] = CurrentRound.Value;
        SyncDetectiveUsageClientRpc(CurrentRound.Value, SingleTarget(playerId));
    }

    [ClientRpc]
    private void SyncDetectiveUsageClientRpc(int lastUsedRound, ClientRpcParams clientRpcParams = default)
    {
        DetectiveCatUI.Instance?.SetLastUsedRound(lastUsedRound);
    }

    [Rpc(SendTo.Server)]
    public void ActivateDetectiveServerRpc(RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;
        if (!chosenDetectives.TryGetValue(sender, out DetectiveCatType cat)) return;

        string catName = DetectiveCatRules.DisplayName(cat);
        CatTiming timing = DetectiveCatRules.Timing(cat);

        bool phaseOk =
            (timing == CatTiming.DrawPhase && ActiveTurnPlayerId.Value == sender && CurrentState.Value == GameState.PlayerTurn_Draw) ||
            (timing == CatTiming.ActionPhase && IsActionPhaseOf(sender));

        if (!phaseOk)
        {
            SendNotice(sender, $"<color=red>{catName}: {DetectiveCatRules.PhaseHint(cat)}</color>");
            return;
        }

        IDetectiveCatTrait trait = CardEffectRegistry.GetDetective(cat);
        if (trait == null)
        {
            SendNotice(sender, $"<color=yellow>{catName} isn't available yet</color>");
            return;
        }

        if (!trait.CanActivate(sender, CurrentRound.Value))
        {
            catLastUsedRound.TryGetValue(sender, out int last);
            SendNotice(sender, $"<color=red>{catName}: {DetectiveCatRules.UnavailableReason(cat, CurrentRound.Value, last)}</color>");
            return;
        }

        trait.Activate(sender);
    }

    public void BeginShimo(ulong playerId)
    {
        NetworkCard[] pile = DeckManager.Instance.GetDiscardPile();
        if (pile.Length == 0)
        {
            SendNotice(playerId, "<color=yellow>The discard pile is empty</color>");
            return;
        }

        OpenCatPick(playerId, CatPickKind.Shimo, pile, "Dumpster Diver Shimo: take a card from the discard pile");
    }
    public void BeginBebang(ulong playerId)
    {
        if (DeckManager.Instance.RemainingDrawCount < 2)
        {
            SendNotice(playerId, "<color=yellow>The draw deck needs at least 2 cards</color>");
            return;
        }

        NetworkCard[] top = DeckManager.Instance.PeekTop(2);
        OpenCatPick(playerId, CatPickKind.Bebang, top, "Inspector Bebang: keep one card (the other goes back on top of the deck)");
    }
    private void OpenCatPick(ulong playerId, CatPickKind kind, NetworkCard[] offered, string title)
    {
        CancelDecisionTimer();
        CurrentState.Value = GameState.PlayerTurn_Resolving;
        LockShuffle(playerId);

        var pending = new PendingCatPick { Player = playerId, Kind = kind, Offered = offered };
        pendingCatPick = pending;

        Debug.Log($"<color=cyan>[cat] {GetPlayerName(playerId)} activates {kind} ({offered.Length} cards offered)</color>");
        OpenCatPickClientRpc(title, offered, SingleTarget(playerId));
        StartDecisionTimer(playerId, () => AutoCatPick(pending));
    }

    [Rpc(SendTo.Server)]
    public void SubmitCatPickServerRpc(int index, RpcParams rpcParams = default)
    {
        PendingCatPick pending = pendingCatPick;
        if (pending == null || rpcParams.Receive.SenderClientId != pending.Player) return;
        if (index < 0 || index >= pending.Offered.Length) return;

        if (pending.OnPicked != null)
        {
            pendingCatPick = null;
            CancelDecisionTimer();
            pending.OnPicked(pending.HandIndices != null ? pending.HandIndices[index] : index);
            return;
        }
        ResolveCatPick(pending, index);
    }

    [Rpc(SendTo.Server)]
    public void CancelCatPickServerRpc(RpcParams rpcParams = default)
    {
        PendingCatPick pending = pendingCatPick;
        if (pending == null || rpcParams.Receive.SenderClientId != pending.Player) return;

        if (pending.OnCancelled != null)
        {
            pendingCatPick = null;
            CancelDecisionTimer();
            pending.OnCancelled();
            return;
        }

        pendingCatPick = null;
        ClearShuffleLocks();
        CurrentState.Value = GameState.PlayerTurn_Draw;
        UpdateTurnUIClientRpc(pending.Player, GetPlayerName(pending.Player), CurrentRound.Value); // re-opens the draw prompt
        StartDrawTimer(pending.Player);
    }

    private void AutoCatPick(PendingCatPick pending)
    {
        if (pendingCatPick != pending) return;

        if (pending.OnCancelled != null)
        {
            pendingCatPick = null;
            CloseHandRevealClientRpc(SingleTarget(pending.Player));
            pending.OnCancelled();
            return;
        }

        int index = pending.Kind == CatPickKind.Shimo ? Random.Range(0, pending.Offered.Length) : 0;
        Debug.Log($"<color=orange>[timer] {GetPlayerName(pending.Player)} ran out of time - cat pick #{index}</color>");
        CloseHandRevealClientRpc(SingleTarget(pending.Player));
        ResolveCatPick(pending, index);
    }

    private void ResolveCatPick(PendingCatPick pending, int index)
    {
        pendingCatPick = null;
        CancelDecisionTimer();

        ulong playerId = pending.Player;
        string playerName = GetPlayerName(playerId);
        NetworkCard chosen = pending.Offered[index];

        if (pending.Kind == CatPickKind.Shimo)
        {
            if (!DeckManager.Instance.TakeFromDiscard(chosen))
            {
                Debug.LogWarning("[cat] Shimo: the chosen card is no longer in the discard pile");
                ClearShuffleLocks();
                CheckHandLimitOrProceedToAction(playerId);
                return;
            }

            MarkCatUsed(playerId);
            RemoveFromDiscardPileClientRpc(chosen);
            PresentDrawnCard(playerId, chosen,
                $"You took {chosen.CardId} from the discard pile!",
                $"{playerName} took {chosen.CardId} from the discard pile!", true);
        }
        else
        {
            NetworkCard? kept = DeckManager.Instance.TakeTop(index);
            if (!kept.HasValue)
            {
                ClearShuffleLocks();
                CheckHandLimitOrProceedToAction(playerId);
                return;
            }

            MarkCatUsed(playerId);
            PresentDrawnCard(playerId, kept.Value,
                $"You kept {kept.Value.CardId}!", $"{playerName} drew a card!", false);
        }
    }

    [ClientRpc]
    private void OpenCatPickClientRpc(string title, NetworkCard[] cards, ClientRpcParams clientRpcParams = default)
    {
        if (HandRevealUI.Instance == null) return;

        HandRevealUI.Instance.ShowPicker(title, cards,
            index => Instance?.SubmitCatPickServerRpc(index),
            () => Instance?.CancelCatPickServerRpc());
    }

    [ClientRpc]
    private void RemoveFromDiscardPileClientRpc(NetworkCard card)
    {
        DiscardPile2D.Instance?.RemoveCardFromDiscard(card);
    }
}