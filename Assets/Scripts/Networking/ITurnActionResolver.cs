namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Separates "when/whose turn is it, what phase, did we time out" (owned by
    /// TurnStateMachine) from "what actually happens to game state when an action
    /// resolves" (owned by ActionResolver). TurnStateMachine calls into this interface and
    /// never touches hand/deck/discard data directly.
    ///
    /// Players are identified by Netcode client ID (ulong).
    /// </summary>
    public interface ITurnActionResolver
    {
        /// <summary>Player explicitly requested to draw. Caller has already validated turn/phase.</summary>
        void ResolveDraw(ulong requester);

        /// <summary>Draw phase timed out — host performs the draw on the current player's behalf.</summary>
        void ResolveAutoDraw(ulong currentTurnPlayer);

        /// <summary>Player requested to steal a specific (blind) hand slot from a target.</summary>
        void ResolveSteal(ulong requester, ulong target, int slotIndex);

        /// <summary>Action phase timed out with no play — host performs a fully random auto-steal.</summary>
        void ResolveAutoSteal(ulong currentTurnPlayer);

        /// <summary>Player requested to play a specific card (by id) from their own hand.</summary>
        void ResolvePlayCard(ulong requester, int cardId);
    }
}
