using Fusion;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// File: Assets/Scripts/Networking/PlayerState.cs
    /// Purpose: Per-player networked state. One instance per connected player,
    /// spawned as a child of (or alongside) their Photon player object.
    /// Runs on: Both — spawned with StateAuthority on the Host, InputAuthority
    /// on the owning client (so a client can cheaply check "is this me?").
    /// Network responsibility: Owns every piece of per-player information the
    /// architecture says must be PUBLIC. Notably absent: actual hand contents.
    /// HandCount is networked; the cards themselves are never put on this
    /// object or any other networked property — they travel only via targeted
    /// RPC to the owning client, per the hidden-information rule.
    /// </summary>
    public sealed class PlayerState : NetworkBehaviour
    {
        /// <summary>
        /// Number of cards currently in this player's hand. Public — everyone
        /// can see hand *size*, never hand *contents*.
        /// </summary>
        [Networked] public int HandCount { get; set; }

        /// <summary>
        /// Running score/points total. Public per the architecture doc.
        /// </summary>
        [Networked] public int Score { get; set; }

        /// <summary>
        /// True while this player has an active Photon connection. Set false
        /// the instant Fusion reports the player as disconnected — this is
        /// what starts the reconnect grace window (see ReconnectDeadline).
        /// </summary>
        [Networked] public NetworkBool IsConnected { get; set; }

        /// <summary>
        /// Host-authoritative countdown for the 60s (configurable) reconnect
        /// grace period. Only meaningful while IsConnected is false. The
        /// disconnection-handling script (upcoming) starts this the moment a
        /// disconnect is detected and checks it for expiry; on expiry it
        /// triggers the forfeit path (hand reshuffled into deck) or, if this
        /// player is the whisker blower holder, ends the match.
        /// </summary>
        [Networked] public TickTimer ReconnectDeadline { get; set; }

        /// <summary>
        /// The Photon PlayerRef this state belongs to. Set once at spawn and
        /// never changed — used to route targeted RPCs (private hand delivery)
        /// to the correct client and to match incoming action requests against
        /// their sender.
        /// </summary>
        [Networked] public PlayerRef Owner { get; set; }

        /// <summary>
        /// Convenience check run on any peer: is the local client the owner of
        /// this PlayerState? Used by UI/input scripts to decide whether to show
        /// "your hand" vs. an opponent's hand-count-only view. Not itself
        /// networked — derived locally from Owner + Runner.LocalPlayer.
        /// </summary>
        public bool IsLocalPlayer => Object != null && Object.Runner != null && Owner == Object.Runner.LocalPlayer;

        public override void Spawned()
        {
            // Defaults are set only by whoever holds state authority (the host),
            // to avoid every peer racing to "initialize" networked properties.
            if (Object.HasStateAuthority)
            {
                HandCount = 0;
                Score = 0;
                IsConnected = true;
                // ReconnectDeadline left at its default (expired/inactive) —
                // it's only ever started explicitly on disconnect.
            }
        }
    }
}
