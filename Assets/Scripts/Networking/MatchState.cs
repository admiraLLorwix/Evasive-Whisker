using Unity.Netcode;
using UnityEngine;

namespace EvasiveWhisker.Networking
{
    public enum TurnPhase : byte
    {
        AwaitingDraw,
        AwaitingAction
    }

    /// <summary>
    /// Why the host ended the match early. The host quitting is not listed here: with no host
    /// left to announce anything, clients learn that through LanSession.HostLost instead.
    /// </summary>
    public enum MatchEndReason : byte
    {
        None,
        HolderDisconnected,
        TooFewPlayers
    }

    /// <summary>
    /// Match-wide networked state. Deliberately does NOT hold Whisker Blower holder
    /// identity — see WhiskerBlowerTracker, which keeps that host-only and non-networked.
    ///
    /// Only the host (server) writes these variables; clients read them.
    /// </summary>
    public class MatchState : NetworkBehaviour
    {
        /// <summary>Sentinel for "nobody's turn" (before the match starts or after everyone is gone).</summary>
        public const ulong NoPlayer = ulong.MaxValue;

        /// <summary>Netcode client ID of the player whose turn it is.</summary>
        public readonly NetworkVariable<ulong> CurrentTurnPlayer = new NetworkVariable<ulong>(NoPlayer);
        public readonly NetworkVariable<TurnPhase> CurrentPhase = new NetworkVariable<TurnPhase>(TurnPhase.AwaitingDraw);

        /// <summary>
        /// Phase deadline in NetworkManager.ServerTime seconds; 0 means "no timer running".
        /// Replaces Fusion's TickTimer. Clients can read it to draw a countdown.
        /// </summary>
        public readonly NetworkVariable<double> PhaseEndServerTime = new NetworkVariable<double>(0d);

        /// <summary>Anything other than None means the match was ended early by the host.</summary>
        public readonly NetworkVariable<MatchEndReason> EndReason = new NetworkVariable<MatchEndReason>(MatchEndReason.None);

        public readonly NetworkVariable<int> DeckCount = new NetworkVariable<int>(0);

        // Public info about only the top of the discard pile — never the full pile order.
        public readonly NetworkVariable<int> DiscardTopCardId = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> DiscardTopDefinitionId = new NetworkVariable<int>(0);

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentPhase.Value = TurnPhase.AwaitingDraw;
            }
        }

        // ---- Phase timer helpers ----

        /// <summary>Host-only: (re)start the phase countdown.</summary>
        public void StartPhaseTimer(float seconds)
        {
            if (!IsServer) return;
            PhaseEndServerTime.Value = NetworkManager.ServerTime.Time + seconds;
        }

        /// <summary>Host-only: stop the countdown (a stopped timer never reports as expired).</summary>
        public void StopPhaseTimer()
        {
            if (!IsServer) return;
            PhaseEndServerTime.Value = 0d;
        }

        /// <summary>True only while a timer is running and its deadline has passed.</summary>
        public bool PhaseTimerExpired =>
            PhaseEndServerTime.Value > 0d && NetworkManager.ServerTime.Time >= PhaseEndServerTime.Value;

        /// <summary>Seconds left in the phase, for UI. 0 when no timer is running.</summary>
        public float PhaseSecondsRemaining =>
            PhaseEndServerTime.Value > 0d
                ? Mathf.Max(0f, (float)(PhaseEndServerTime.Value - NetworkManager.ServerTime.Time))
                : 0f;
    }
}
