using System;
using System.Collections.Generic;
using System.Linq;
using Fusion;
using UnityEngine;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// File: Assets/Scripts/Networking/TurnStateMachine.cs
    /// Purpose: Owns the two-phase per-turn state machine (AwaitingDraw ⇄
    /// AwaitingAction), the independent host-authoritative phase timer, whose
    /// turn it is, and the pause/resume hooks used during host migration
    /// recovery. Validates every incoming action request against current
    /// phase + current turn player before doing anything with it.
    /// Runs on: Both. RPC entry points execute their body only when
    /// Object.HasStateAuthority is true (i.e. only on the host) — clients
    /// call the RPCs but never run the validation/resolution logic locally.
    /// Network responsibility: Reads and writes CurrentTurnPlayer, CurrentPhase,
    /// PhaseTimer, IsMigrationRecoveryPaused, and PausedPhaseSecondsRemaining
    /// on MatchState — this is the only script that's allowed to mutate those.
    /// Does NOT touch Deck, hand contents, or discard pile directly; that's
    /// delegated to an ITurnActionResolver (implemented in the next step).
    /// </summary>
    [RequireComponent(typeof(MatchState))]
    public sealed class TurnStateMachine : NetworkBehaviour
    {
        [Header("Phase durations (host-authoritative; clients never decide expiry)")]
        [SerializeField] private float _drawPhaseDurationSeconds = 15f;
        [SerializeField] private float _actionPhaseDurationSeconds = 30f;

        private MatchState _matchState;

        /// <summary>
        /// The resolver that actually mutates deck/hands/discard. Assigned at
        /// Spawned() via GetComponent — expected to live on the same
        /// NetworkObject once the Action RPCs script (next step) exists.
        /// Left nullable so this script compiles and can be wired/tested in
        /// isolation before that script exists.
        /// </summary>
        private ITurnActionResolver _resolver;

        /// <summary>
        /// Host-side only. Not networked — turn order isn't hidden information,
        /// but it doesn't need to be a synced property either, since only the
        /// host ever acts on it and clients just observe CurrentTurnPlayer.
        /// Rebuilding this list after host migration is the new host's
        /// responsibility (host-migration script, a later step) — it can
        /// derive a stable order from PlayerState roster/seat data.
        /// </summary>
        private readonly List<PlayerRef> _turnOrder = new List<PlayerRef>();

        /// <summary>Host-side only RNG for auto-steal target selection — same rule as Deck's shuffle RNG: never client-side.</summary>
        private readonly System.Random _rng = new System.Random();

        public override void Spawned()
        {
            _matchState = GetComponent<MatchState>();
            _resolver = GetComponent<ITurnActionResolver>(); // may be null until the next step's script is attached
        }

        // ---------------------------------------------------------------
        // Match-start / setup entry points (called by whatever owns match
        // start — lobby transition script, out of scope here)
        // ---------------------------------------------------------------

        /// <summary>Host-only. Establishes turn order and kicks off the first turn.</summary>
        public void InitializeTurnOrder(IEnumerable<PlayerRef> players)
        {
            if (!Object.HasStateAuthority) return;
            _turnOrder.Clear();
            _turnOrder.AddRange(players);
            if (_turnOrder.Count == 0)
            {
                Debug.LogError("TurnStateMachine.InitializeTurnOrder called with no players.");
                return;
            }
            BeginTurn(_turnOrder[0]);
        }

        private void BeginTurn(PlayerRef player)
        {
            _matchState.CurrentTurnPlayer = player;
            TransitionTo(TurnPhase.AwaitingDraw, _drawPhaseDurationSeconds);
        }

        private void TransitionTo(TurnPhase phase, float durationSeconds)
        {
            _matchState.CurrentPhase = phase;
            _matchState.PhaseTimer = TickTimer.CreateFromSeconds(Runner, durationSeconds);
        }

        // ---------------------------------------------------------------
        // Client → Host action requests. Each validates phase + turn-player
        // before doing anything. Invalid/out-of-turn requests are dropped
        // silently (logged) — never trusted, per the architecture's core rule.
        // ---------------------------------------------------------------

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestDraw(RpcInfo info = default)
        {
            var sender = info.Source;
            if (!IsLegalRequest(sender, TurnPhase.AwaitingDraw)) return;
            ExecuteDraw(sender, isAuto: false);
        }

        /// <summary>
        /// slotIndex is a hand SLOT POSITION (0-based) in the target's hand,
        /// not a card id — the requester picks blind, by position, since they
        /// can't see card contents. The client UI is expected to show the
        /// target's face-down hand as HandCount slots and let the player tap
        /// one of those positions.
        /// </summary>
        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestSteal(PlayerRef target, int slotIndex, RpcInfo info = default)
        {
            var sender = info.Source;
            if (!IsLegalRequest(sender, TurnPhase.AwaitingAction)) return;
            if (target == sender)
            {
                Debug.LogWarning($"Player {sender} tried to steal from themselves — ignored.");
                return;
            }
            ExecuteSteal(sender, target, isAuto: false, slotIndex: slotIndex);
        }

        [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
        public void RPC_RequestPlayCard(int cardId, RpcInfo info = default)
        {
            var sender = info.Source;
            if (!IsLegalRequest(sender, TurnPhase.AwaitingAction)) return;
            ExecutePlayCard(sender, cardId);
        }

        private bool IsLegalRequest(PlayerRef sender, TurnPhase requiredPhase)
        {
            if (!Object.HasStateAuthority) return false; // safety net; RPC target already restricts this to the host
            if (_matchState.IsMigrationRecoveryPaused)
            {
                Debug.LogWarning($"Rejected action from {sender}: match is paused for host migration recovery.");
                return false;
            }
            if (sender != _matchState.CurrentTurnPlayer)
            {
                Debug.LogWarning($"Rejected action from {sender}: not their turn (current turn player is {_matchState.CurrentTurnPlayer}).");
                return false;
            }
            if (_matchState.CurrentPhase != requiredPhase)
            {
                Debug.LogWarning($"Rejected action from {sender}: wrong phase (expected {requiredPhase}, was {_matchState.CurrentPhase}).");
                return false;
            }
            return true;
        }

        // ---------------------------------------------------------------
        // Timeout handling — host-driven, runs every network tick.
        // ---------------------------------------------------------------

        public override void FixedUpdateNetwork()
        {
            if (!Object.HasStateAuthority) return;
            if (_matchState.IsMigrationRecoveryPaused) return; // frozen — see PauseForMigration
            if (!_matchState.PhaseTimer.Expired(Runner)) return;

            switch (_matchState.CurrentPhase)
            {
                case TurnPhase.AwaitingDraw:
                    ExecuteDraw(_matchState.CurrentTurnPlayer, isAuto: true);
                    break;
                case TurnPhase.AwaitingAction:
                    var autoTarget = ChooseAutoStealTarget(_matchState.CurrentTurnPlayer);
                    if (autoTarget != PlayerRef.None)
                        ExecuteSteal(_matchState.CurrentTurnPlayer, autoTarget, isAuto: true);
                    else
                        Debug.LogWarning("AwaitingAction timed out but no valid auto-steal target was found (no other players with cards?). Turn is stuck — this edge case needs a rule.");
                    break;
            }
        }

        // ---------------------------------------------------------------
        // Execution — delegates the actual game-state mutation to the
        // resolver, then advances the state machine.
        // ---------------------------------------------------------------

        private void ExecuteDraw(PlayerRef player, bool isAuto)
        {
            if (_resolver == null) { Debug.LogWarning("No ITurnActionResolver attached yet — draw not resolved."); return; }
            if (isAuto) _resolver.ResolveAutoDraw(player); else _resolver.ResolveDraw(player);
            TransitionTo(TurnPhase.AwaitingAction, _actionPhaseDurationSeconds);
        }

        private void ExecuteSteal(PlayerRef requester, PlayerRef target, bool isAuto, int slotIndex = -1)
        {
            if (_resolver == null) { Debug.LogWarning("No ITurnActionResolver attached yet — steal not resolved."); return; }
            if (isAuto) _resolver.ResolveAutoSteal(requester, target); else _resolver.ResolveSteal(requester, target, slotIndex);
            AdvanceTurn();
        }

        private void ExecutePlayCard(PlayerRef player, int cardId)
        {
            if (_resolver == null) { Debug.LogWarning("No ITurnActionResolver attached yet — play not resolved."); return; }
            _resolver.ResolvePlayCard(player, cardId);
            AdvanceTurn();
        }

        private void AdvanceTurn()
        {
            if (_turnOrder.Count == 0) return;
            int currentIndex = _turnOrder.IndexOf(_matchState.CurrentTurnPlayer);
            int nextIndex = (currentIndex + 1) % _turnOrder.Count;
            // TODO(item 6 — disconnection handling): skip players who are
            // currently disconnected rather than always taking the literal
            // next seat. Left as a straight rotation for now since PlayerState
            // connection status isn't wired to this script yet.
            BeginTurn(_turnOrder[nextIndex]);
        }

        /// <summary>Host-side only RNG pick among other players who currently have cards. Excludes the requester.</summary>
        private PlayerRef ChooseAutoStealTarget(PlayerRef requester)
        {
            var candidates = _turnOrder.Where(p => p != requester).ToList();
            // TODO(item 6): also filter out disconnected players once
            // PlayerState connection status is available here.
            if (candidates.Count == 0) return PlayerRef.None;
            return candidates[_rng.Next(candidates.Count)];
        }

        // ---------------------------------------------------------------
        // Host-migration pause/resume hooks (called by the host-migration
        // script, a later step). Implements the "timer pauses during
        // recovery" decision.
        // ---------------------------------------------------------------

        public void PauseForMigration()
        {
            if (!Object.HasStateAuthority) return;
            float remaining = _matchState.PhaseTimer.RemainingTime(Runner) ?? 0f;
            _matchState.PausedPhaseSecondsRemaining = Mathf.Max(0f, remaining);
            _matchState.IsMigrationRecoveryPaused = true;
        }

        public void ResumeAfterMigration()
        {
            if (!Object.HasStateAuthority) return;
            _matchState.PhaseTimer = TickTimer.CreateFromSeconds(Runner, _matchState.PausedPhaseSecondsRemaining);
            _matchState.IsMigrationRecoveryPaused = false;
        }
    }
}
