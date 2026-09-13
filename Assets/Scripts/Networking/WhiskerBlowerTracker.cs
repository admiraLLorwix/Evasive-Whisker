using System.Collections.Generic;
using Fusion;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// File: Assets/Scripts/Networking/WhiskerBlowerTracker.cs
    /// Purpose: Host-authoritative record of who holds the whisker blower and
    /// who currently knows it. This is the "impostor identity" for the game —
    /// hidden by default, revealed only through specific rules.
    /// Runs on: Server/Host ONLY for the holder/knowledge data itself.
    /// The RPC-sending methods below run host-side and dispatch to clients;
    /// clients never read HolderPlayer directly (there is nothing for them to
    /// read — it's a plain host-side field, not a networked property).
    /// Network responsibility: Deliberately holds NO [Networked] properties.
    /// Same rule as hand contents: identity this hidden must never be
    /// networked as shared state, even encrypted/obscured — it must simply
    /// not exist on any client until an explicit reveal RPC is sent. This
    /// class is attached to the same NetworkObject as MatchState (or a
    /// sibling host-only component) purely so it has a Runner reference to
    /// send RPCs from; it does not sync itself.
    /// </summary>
    public sealed class WhiskerBlowerTracker : NetworkBehaviour
    {
        /// <summary>
        /// Host-side only. Never read by client code — clients have no access
        /// to this field's true value except via an explicit reveal RPC.
        /// </summary>
        private PlayerRef _holderPlayer;

        /// <summary>
        /// Host-side only. Tracks which players currently know the holder's
        /// identity, because a card-ability reveal is scoped to whoever
        /// triggered/received it — not a global "now everyone knows" flip.
        /// Reset whenever the whisker blower changes hands (a new steal moves
        /// it, which should also reset who "currently" knows, per your framing
        /// of this as an ongoing impostor mechanic rather than a one-time leak).
        /// </summary>
        private readonly HashSet<PlayerRef> _knownTo = new HashSet<PlayerRef>();

        public PlayerRef HolderPlayer => _holderPlayer;

        /// <summary>
        /// Call when the whisker blower changes hands (initial deal, or moved
        /// via a steal). Host-only. Clears prior knowledge — a new holder
        /// means old "I saw who had it" information about the previous holder
        /// is no longer the current answer.
        /// </summary>
        public void SetHolder(PlayerRef newHolder)
        {
            if (!Object.HasStateAuthority) return;
            _holderPlayer = newHolder;
            _knownTo.Clear();
        }

        public bool IsKnownTo(PlayerRef viewer) => _knownTo.Contains(viewer);

        /// <summary>
        /// Reveals the current holder to a single player only, via targeted
        /// RPC. This is the hook the (future) card-ability resolution script
        /// calls when a reveal-condition card is played/used. Host-only.
        /// </summary>
        public void RevealToPlayer(PlayerRef viewer)
        {
            if (!Object.HasStateAuthority) return;
            _knownTo.Add(viewer);
            RpcRevealHolder(viewer, _holderPlayer);
        }

        /// <summary>
        /// Reveals the current holder to every player at once. Host-only.
        /// This is the ONLY path that produces a full public reveal: called
        /// when the custodian's guess at match end is correct. It is NOT
        /// called on the whisker-blower-holder-disconnect match-ending path —
        /// that ending has no guess, so no automatic reveal happens there.
        /// (Flagged assumption — confirm if disconnect-triggered endings
        /// should also reveal.)
        /// </summary>
        public void RevealToAll()
        {
            if (!Object.HasStateAuthority) return;
            foreach (var p in Runner.ActivePlayers) _knownTo.Add(p);
            RpcRevealHolderPublic(_holderPlayer);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        private void RpcRevealHolder([RpcTarget] PlayerRef viewer, PlayerRef holder)
        {
            // Client-side: hand off to whatever UI/role system displays
            // "you now know X is the whisker blower holder." Left as a stub —
            // wiring to UI belongs to a later, presentation-layer script.
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RpcRevealHolderPublic(PlayerRef holder)
        {
            // Client-side: broadcast reveal — e.g. "The whisker blower was
            // held by <holder>!" at match end. Stub for the same reason above.
        }
    }
}
