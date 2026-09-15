using Fusion;

namespace EvasiveWhisker.Networking
{
    public enum TurnPhase
    {
        AwaitingDraw,
        AwaitingAction
    }

    /// <summary>
    /// Match-wide networked state. Deliberately does NOT hold Whisker Blower holder
    /// identity — see WhiskerBlowerTracker, which keeps that host-only and non-networked.
    /// </summary>
    public class MatchState : NetworkBehaviour
    {
        [Networked] public PlayerRef CurrentTurnPlayer { get; set; }
        [Networked] public TurnPhase CurrentPhase { get; set; }
        [Networked] public TickTimer PhaseTimer { get; set; }

        // Host-migration pause support: when migration begins, the host freezes the
        // effective countdown by recording remaining seconds; IsPausedForMigration gates
        // TurnStateMachine's timeout checks until ResumeAfterMigration() is called.
        [Networked] public NetworkBool IsPausedForMigration { get; set; }
        [Networked] public float RemainingPhaseSecondsAtPause { get; set; }

        [Networked] public int DeckCount { get; set; }

        // Public info about only the top of the discard pile — never the full pile order.
        [Networked] public int DiscardTopCardId { get; set; }
        [Networked] public int DiscardTopDefinitionId { get; set; }

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                CurrentPhase = TurnPhase.AwaitingDraw;
                IsPausedForMigration = false;
            }
        }

        /// <summary>Host-only: pause the phase timer ahead of a host migration handoff.</summary>
        public void PauseForMigration()
        {
            if (!Object.HasStateAuthority) return;
            RemainingPhaseSecondsAtPause = PhaseTimer.RemainingTime(Runner) ?? 0f;
            IsPausedForMigration = true;
        }

        /// <summary>Host-only: resume the phase timer after migration recovery completes.</summary>
        public void ResumeAfterMigration()
        {
            if (!Object.HasStateAuthority) return;
            PhaseTimer = TickTimer.CreateFromSeconds(Runner, RemainingPhaseSecondsAtPause);
            IsPausedForMigration = false;
        }
    }
}
