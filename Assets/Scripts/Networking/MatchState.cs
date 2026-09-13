using Fusion;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// The two-phase per-turn state machine, as locked in the architecture doc.
    /// Lives here (rather than in Core) because it's inseparable from how it's
    /// synced — every peer needs to see phase changes as networked state.
    /// </summary>
    public enum TurnPhase
    {
        AwaitingDraw = 0,
        AwaitingAction = 1
    }

    /// <summary>
    /// File: Assets/Scripts/Networking/MatchState.cs
    /// Purpose: Single source of truth for match-wide networked state — the
    /// things every player needs to see regardless of whose turn it is.
    /// One instance exists per match, owned by the host.
    /// Runs on: Both — StateAuthority on Host (only the host ever writes these
    /// properties); clients only read.
    /// Network responsibility: Everything this architecture marks PUBLIC that
    /// isn't tied to a single player (that's PlayerState's job): whose turn it
    /// is, which phase, the phase timer, deck count, and the discard pile's
    /// public top card. Whisker blower holder identity is explicitly NOT
    /// public — see the note near the bottom of this class.
    /// </summary>
    public sealed class MatchState : NetworkBehaviour
    {
        [Networked] public PlayerRef CurrentTurnPlayer { get; set; }
        [Networked] public TurnPhase CurrentPhase { get; set; }

        /// <summary>
        /// Host-authoritative phase timer. Independent per phase — the
        /// turn-state-machine script (next up) restarts this with a fresh
        /// duration on every AwaitingDraw⇄AwaitingAction transition. Client
        /// UIs read this for display only; expiry is decided by the host.
        /// </summary>
        [Networked] public TickTimer PhaseTimer { get; set; }

        /// <summary>
        /// Set true by the host-migration recovery script while a new host is
        /// rebuilding state. While true, the turn-state-machine script must
        /// not treat PhaseTimer expiry as a real timeout (per the "timer pauses
        /// during recovery" decision). PausedRemainingSeconds captures how much
        /// time was left so the timer can be resumed accurately rather than
        /// reset to full duration — Fusion's TickTimer has no native pause, so
        /// the pattern is: on pause, stop trusting PhaseTimer and remember the
        /// remainder; on resume, start a fresh TickTimer for that remainder.
        /// Full pause/resume wiring belongs to the turn-state-machine and
        /// host-migration scripts — this class just holds the flag and value.
        /// </summary>
        [Networked] public NetworkBool IsMigrationRecoveryPaused { get; set; }
        [Networked] public float PausedPhaseSecondsRemaining { get; set; }

        /// <summary>
        /// Public deck size. The actual Deck object (Core.Deck) lives host-side
        /// only and is never networked — this int is republished by the host
        /// after every draw/reshuffle so clients can show "N cards left."
        /// </summary>
        [Networked] public int DeckCount { get; set; }

        /// <summary>
        /// Identity of the top discard card — the one card in the discard pile
        /// that's public per the architecture ("top card is public; pile
        /// contents are hidden"). -1 means the discard pile is empty. Everything
        /// else in the pile stays host-side only, same as the deck.
        /// </summary>
        [Networked] public int DiscardTopCardId { get; set; } = -1;
        [Networked] public int DiscardTopDefinitionId { get; set; }

        // NOTE: WhiskerBlowerHolder identity deliberately does NOT live here.
        // It's hidden information (revealed only conditionally, via specific
        // card abilities, or at match end if the custodian's guess is correct)
        // — exactly the same category as hand contents. Networking it as a
        // [Networked] property on this class would broadcast it to every
        // client unconditionally, which breaks the "impostor" mechanic.
        // See WhiskerBlowerTracker.cs (host-only, non-networked) for where
        // this identity actually lives, and how it gets selectively revealed.

        public override void Spawned()
        {
            if (Object.HasStateAuthority)
            {
                CurrentPhase = TurnPhase.AwaitingDraw;
                IsMigrationRecoveryPaused = false;
                PausedPhaseSecondsRemaining = 0f;
                DiscardTopCardId = -1;
                DiscardTopDefinitionId = 0;
                // CurrentTurnPlayer, DeckCount, WhiskerBlowerHolder are set by
                // match-start setup logic, not here — this just establishes
                // safe defaults before that setup runs.
            }
        }
    }
}
