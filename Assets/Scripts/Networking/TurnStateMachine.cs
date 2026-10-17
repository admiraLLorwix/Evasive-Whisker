using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Turn/phase FSM: AwaitingDraw -> AwaitingAction -> advance to next player -> AwaitingDraw.
    /// Owns *when* things happen (whose turn, what phase, timeouts) and delegates *what
    /// happens to game state* to an ITurnActionResolver. Never mutates hands/deck/discard
    /// directly — see ActionResolver for that.
    ///
    /// Players are identified by Netcode client ID (ulong). The host owns the turn order;
    /// a forfeited player is removed from it via RemovePlayer().
    /// </summary>
    public class TurnStateMachine : NetworkBehaviour
    {
        [SerializeField] private float drawPhaseSeconds = 20f;
        [SerializeField] private float actionPhaseSeconds = 30f;

        public MatchState MatchState;
        public WhiskerBlowerTracker WhiskerBlowerTracker; // unchanged from the Fusion version (currently unused here)

        private ITurnActionResolver _resolver;
        private readonly List<ulong> _turnOrder = new List<ulong>(); // host-only
        private int _currentTurnIndex;
        private bool _started;

        public int PlayersInTurnOrder => _turnOrder.Count;

        /// <summary>Host-only. Works whether called before or after this object has spawned.</summary>
        public void Initialize(ITurnActionResolver resolver, IReadOnlyList<ulong> turnOrder)
        {
            _resolver = resolver;
            _turnOrder.Clear();
            _turnOrder.AddRange(turnOrder);
            _currentTurnIndex = 0;
            _started = false;

            if (IsSpawned && IsServer) StartFirstTurn();
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer && _resolver != null) StartFirstTurn();
        }

        private void StartFirstTurn()
        {
            if (_started || _turnOrder.Count == 0) return;
            _started = true;
            StartTurnFor(_turnOrder[_currentTurnIndex]);
        }

        // Replaces FixedUpdateNetwork: only the host runs the phase clock.
        private void Update()
        {
            if (!IsServer || !IsSpawned || !_started || _turnOrder.Count == 0) return;
            if (!MatchState.PhaseTimerExpired) return;

            switch (MatchState.CurrentPhase.Value)
            {
                case TurnPhase.AwaitingDraw:
                    _resolver.ResolveAutoDraw(MatchState.CurrentTurnPlayer.Value);
                    EnterActionPhase();
                    break;

                case TurnPhase.AwaitingAction:
                    _resolver.ResolveAutoSteal(MatchState.CurrentTurnPlayer.Value);
                    AdvanceTurn();
                    break;
            }
        }

        // ---- RPC entry points (client request -> host validates -> resolver mutates state) ----
        // The requester is ALWAYS the connection the RPC arrived on (SenderClientId). Never take
        // "who am I" from a client-supplied parameter — a modified client could claim to be
        // whoever's turn it is.

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestDrawRpc(RpcParams rpcParams = default)
        {
            ulong requester = rpcParams.Receive.SenderClientId;
            if (!IsValidRequest(requester, TurnPhase.AwaitingDraw)) return;
            _resolver.ResolveDraw(requester);
            EnterActionPhase();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestStealRpc(ulong target, int slotIndex, RpcParams rpcParams = default)
        {
            ulong requester = rpcParams.Receive.SenderClientId;
            if (!IsValidRequest(requester, TurnPhase.AwaitingAction)) return;
            _resolver.ResolveSteal(requester, target, slotIndex);
            AdvanceTurn();
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        public void RequestPlayCardRpc(int cardId, RpcParams rpcParams = default)
        {
            ulong requester = rpcParams.Receive.SenderClientId;
            if (!IsValidRequest(requester, TurnPhase.AwaitingAction)) return;
            _resolver.ResolvePlayCard(requester, cardId);
            AdvanceTurn();
        }

        // ---- Phase transitions ----

        private void StartTurnFor(ulong player)
        {
            MatchState.CurrentTurnPlayer.Value = player;
            MatchState.CurrentPhase.Value = TurnPhase.AwaitingDraw;
            MatchState.StartPhaseTimer(drawPhaseSeconds);
        }

        private void EnterActionPhase()
        {
            MatchState.CurrentPhase.Value = TurnPhase.AwaitingAction;
            MatchState.StartPhaseTimer(actionPhaseSeconds);
        }

        public void AdvanceTurn()
        {
            if (!IsServer) return;

            if (_turnOrder.Count == 0)
            {
                EndTurnLoop();
                return;
            }

            // Forfeited players are removed from _turnOrder by RemovePlayer(), so simply
            // stepping to the next entry always lands on someone still in the match.
            _currentTurnIndex = (_currentTurnIndex + 1) % _turnOrder.Count;
            StartTurnFor(_turnOrder[_currentTurnIndex]);
        }

        /// <summary>
        /// Host-only. Called by the forfeit path when a player disconnects. Removes them from the
        /// rotation. If it was their turn, their turn is passed over entirely (no auto-draw /
        /// auto-steal on their behalf) and the next player's turn starts immediately.
        /// </summary>
        public void RemovePlayer(ulong clientId)
        {
            if (!IsServer) return;

            int index = _turnOrder.IndexOf(clientId);
            if (index < 0) return;

            bool wasTheirTurn = _started && index == _currentTurnIndex;
            _turnOrder.RemoveAt(index);

            if (_turnOrder.Count == 0)
            {
                _currentTurnIndex = 0;
                if (_started) EndTurnLoop();
                return;
            }

            // Removing someone earlier in the order shifts everyone after them down by one.
            if (index < _currentTurnIndex) _currentTurnIndex--;

            if (wasTheirTurn)
            {
                // The next player now sits at the same index (wrap if we removed the last entry).
                _currentTurnIndex %= _turnOrder.Count;
                StartTurnFor(_turnOrder[_currentTurnIndex]);
            }
        }

        /// <summary>
        /// Host-only. Stops the turn loop when the match ends early: the phase clock stops and
        /// every further draw/steal/play request is rejected.
        /// </summary>
        public void HaltTurns()
        {
            if (!IsServer) return;
            _started = false;
            EndTurnLoop();
        }

        private void EndTurnLoop()
        {
            MatchState.CurrentTurnPlayer.Value = MatchState.NoPlayer;
            MatchState.StopPhaseTimer();
        }

        // ---- Validation ----

        private bool IsValidRequest(ulong requester, TurnPhase expectedPhase)
        {
            if (!IsServer || !_started) return false;
            if (requester != MatchState.CurrentTurnPlayer.Value) return false;
            if (MatchState.CurrentPhase.Value != expectedPhase) return false;
            return true;
        }
    }
}
