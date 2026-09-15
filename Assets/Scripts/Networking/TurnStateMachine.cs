using UnityEngine;
using Fusion;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Turn/phase FSM: AwaitingDraw -> AwaitingAction -> advance to next player -> AwaitingDraw.
    /// Owns *when* things happen (whose turn, what phase, timeouts) and delegates *what
    /// happens to game state* to an ITurnActionResolver. Never mutates hands/deck/discard
    /// directly — see ActionResolver for that.
    /// </summary>
    public class TurnStateMachine : NetworkBehaviour
    {
        [SerializeField] private float drawPhaseSeconds = 20f;
        [SerializeField] private float actionPhaseSeconds = 30f;

        public MatchState MatchState;
        public WhiskerBlowerTracker WhiskerBlowerTracker;

        private ITurnActionResolver _resolver;
        private PlayerRef[] _turnOrder;
        private int _currentTurnIndex;

        public void Initialize(ITurnActionResolver resolver, PlayerRef[] turnOrder)
        {
            _resolver = resolver;
            _turnOrder = turnOrder;
            _currentTurnIndex = 0;
        }

        public override void Spawned()
        {
            if (Object.HasStateAuthority && _turnOrder != null && _turnOrder.Length > 0)
            {
                StartTurnFor(_turnOrder[_currentTurnIndex]);
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (MatchState.IsPausedForMigration) return;
            if (!MatchState.PhaseTimer.Expired(Runner)) return;

            switch (MatchState.CurrentPhase)
            {
                case TurnPhase.AwaitingDraw:
                    _resolver.ResolveAutoDraw(MatchState.CurrentTurnPlayer);
                    EnterActionPhase();
                    break;

                case TurnPhase.AwaitingAction:
                    _resolver.ResolveAutoSteal(MatchState.CurrentTurnPlayer);
                    AdvanceTurn();
                    break;
            }
        }

        // ---- RPC entry points (client request -> host validates -> resolver mutates state) ----

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestDraw(PlayerRef requester, RpcInfo info = default)
        {
            if (!IsValidRequest(requester, TurnPhase.AwaitingDraw)) return;
            _resolver.ResolveDraw(requester);
            EnterActionPhase();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestSteal(PlayerRef requester, PlayerRef target, int slotIndex, RpcInfo info = default)
        {
            if (!IsValidRequest(requester, TurnPhase.AwaitingAction)) return;
            _resolver.ResolveSteal(requester, target, slotIndex);
            AdvanceTurn();
        }

        [Rpc(RpcSources.InputAuthority, RpcTargets.StateAuthority)]
        public void RPC_RequestPlayCard(PlayerRef requester, int cardId, RpcInfo info = default)
        {
            if (!IsValidRequest(requester, TurnPhase.AwaitingAction)) return;
            _resolver.ResolvePlayCard(requester, cardId);
            AdvanceTurn();
        }

        // ---- Phase transitions ----

        private void StartTurnFor(PlayerRef player)
        {
            MatchState.CurrentTurnPlayer = player;
            MatchState.CurrentPhase = TurnPhase.AwaitingDraw;
            MatchState.PhaseTimer = TickTimer.CreateFromSeconds(Runner, drawPhaseSeconds);
        }

        private void EnterActionPhase()
        {
            MatchState.CurrentPhase = TurnPhase.AwaitingAction;
            MatchState.PhaseTimer = TickTimer.CreateFromSeconds(Runner, actionPhaseSeconds);
        }

        public void AdvanceTurn()
        {
            if (!Object.HasStateAuthority) return;

            // TODO (Item 6, turn-rotation skip logic): this should skip over any player
            // whose PlayerState.IsConnected is false — a disconnected player's turn is
            // passed over entirely, not resolved via auto-draw/auto-steal on their behalf.
            // Needs access to the PlayerState lookup (not currently held by this class) to
            // implement the skip; wiring that in is part of Item 6.
            _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Length;

            StartTurnFor(_turnOrder[_currentTurnIndex]);
        }

        // ---- Host migration pause/resume hooks (Item 7) ----

        public void PauseForMigration()
        {
            if (!Object.HasStateAuthority) return;
            MatchState.PauseForMigration();
        }

        public void ResumeAfterMigration()
        {
            if (!Object.HasStateAuthority) return;
            MatchState.ResumeAfterMigration();
        }

        // ---- Validation ----

        private bool IsValidRequest(PlayerRef requester, TurnPhase expectedPhase)
        {
            if (!Object.HasStateAuthority) return false;
            if (MatchState.IsPausedForMigration) return false;
            if (requester != MatchState.CurrentTurnPlayer) return false;
            if (MatchState.CurrentPhase != expectedPhase) return false;
            return true;
        }
    }
}
