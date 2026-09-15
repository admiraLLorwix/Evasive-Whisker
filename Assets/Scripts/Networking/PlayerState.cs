using Fusion;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Per-player networked state. Hand *contents* are never stored here — only the count
    /// is networked, so other clients can render "N cards in hand" without learning what
    /// those cards are. Actual hand contents live host-side in ActionResolver.
    /// </summary>
    public class PlayerState : NetworkBehaviour
    {
        [Networked] public PlayerRef Owner { get; set; }
        [Networked] public int HandCount { get; set; }
        [Networked] public int Score { get; set; }
        [Networked] public NetworkBool IsConnected { get; set; }

        // Tick-based deadline for this player's reconnect grace period. Set by the host
        // when a disconnect is detected; cleared on confirmed reconnect.
        [Networked] public TickTimer ReconnectDeadline { get; set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                IsConnected = true;
            }
        }

        /// <summary>Host-only: begin the reconnect grace period for this player.</summary>
        public void BeginReconnectGracePeriod(float seconds)
        {
            if (!Object.HasStateAuthority) return;
            IsConnected = false;
            ReconnectDeadline = TickTimer.CreateFromSeconds(Runner, seconds);
        }

        /// <summary>Host-only: mark the player reconnected and clear the grace timer.</summary>
        public void ConfirmReconnected()
        {
            if (!Object.HasStateAuthority) return;
            IsConnected = true;
            ReconnectDeadline = TickTimer.None;
        }

        /// <summary>
        /// Host-only: has the reconnect grace period elapsed without the player reconnecting?
        /// Verify TickTimer.Expired/ExpiredOrNotRunning semantics against your installed
        /// Fusion 2.1 API — this assumes Expired(Runner) is false for a TickTimer.None
        /// (never-started) timer.
        /// </summary>
        public bool HasReconnectDeadlineExpired()
        {
            if (IsConnected) return false;
            return ReconnectDeadline.Expired(Runner);
        }
    }
}
