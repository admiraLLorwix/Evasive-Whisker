using Fusion;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// File: Assets/Scripts/Networking/ITurnActionResolver.cs
    /// Purpose: Boundary between "when is an action legal and whose turn is
    /// it" (TurnStateMachine, this step) and "what actually happens to the
    /// deck/hands/discard when that action executes" (the Action RPCs script,
    /// next step). TurnStateMachine calls these hooks after validating a
    /// request or a timeout — it never touches Deck, hand contents, or the
    /// discard pile directly.
    /// Runs on: Server/Host ONLY — every method here executes host-side.
    /// Network responsibility: None directly (no networked properties on the
    /// interface itself). Implementations are expected to update DeckCount /
    /// discard-top fields on MatchState and HandCount on PlayerState after
    /// mutating the real (host-only) game state, and to send the private
    /// hand-delivery RPC for draws (that RPC channel is the next step, item 5).
    /// </summary>
    public interface ITurnActionResolver
    {
        /// <summary>Player explicitly requested a draw during AwaitingDraw.</summary>
        void ResolveDraw(PlayerRef player);

        /// <summary>AwaitingDraw timed out — host draws on the player's behalf.</summary>
        void ResolveAutoDraw(PlayerRef player);

        /// <summary>
        /// Player explicitly requested a steal during AwaitingAction, picking
        /// a specific hand slot (0-based position) from the target's hand —
        /// not a specific card, since the requester can't see card contents.
        /// Slot bounds are validated here, not by the caller.
        /// </summary>
        void ResolveSteal(PlayerRef requester, PlayerRef target, int slotIndex);

        /// <summary>
        /// AwaitingAction timed out — host has already chosen the auto-steal
        /// target (per the architecture doc's "auto-selects a target and
        /// executes a steal"). Slot is also host-chosen at random, since
        /// there's no player input to pick one.
        /// </summary>
        void ResolveAutoSteal(PlayerRef requester, PlayerRef target);

        /// <summary>Player explicitly requested to play a card during AwaitingAction.</summary>
        void ResolvePlayCard(PlayerRef player, int cardId);
    }
}
