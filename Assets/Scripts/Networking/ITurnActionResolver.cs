using Fusion;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Separates "when/whose turn is it, what phase, did we time out" (owned by
    /// TurnStateMachine) from "what actually happens to game state when an action
    /// resolves" (owned by ActionResolver). TurnStateMachine calls into this interface and
    /// never touches hand/deck/discard data directly.
    /// </summary>
    public interface ITurnActionResolver
    {
        /// <summary>Player explicitly requested to draw. Caller has already validated turn/phase.</summary>
        void ResolveDraw(PlayerRef requester);

        /// <summary>Draw phase timed out — host performs the draw on the current player's behalf.</summary>
        void ResolveAutoDraw(PlayerRef currentTurnPlayer);

        /// <summary>Player requested to steal a specific (blind) hand slot from a target.</summary>
        void ResolveSteal(PlayerRef requester, PlayerRef target, int slotIndex);

        /// <summary>Action phase timed out with no play — host performs a fully random auto-steal.</summary>
        void ResolveAutoSteal(PlayerRef currentTurnPlayer);

        /// <summary>Player requested to play a specific card (by id) from their own hand.</summary>
        void ResolvePlayCard(PlayerRef requester, int cardId);
    }
}
