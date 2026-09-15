using System.Collections.Generic;
using Fusion;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Tracks who holds the Whisker Blower card. The holder's identity is intentionally
    /// NEVER a [Networked] property — it must not exist as shared network state. It lives
    /// only in host memory as a plain field, and is only ever pushed to clients via a
    /// targeted RPC (conditional card-ability reveal) or a single all-players broadcast RPC
    /// (correct custodian guess at match end) — the only two reveal paths for holder
    /// identity, per the project's three approved public-reveal triggers.
    ///
    /// This class is still a NetworkBehaviour (attached to a spawned NetworkObject) purely
    /// so it can define RPC methods — RPC support is independent of whether any property on
    /// the behaviour is marked [Networked].
    /// </summary>
    public class WhiskerBlowerTracker : NetworkBehaviour
    {
        // Host-only. Never [Networked]. default(PlayerRef) means "no holder assigned yet".
        private PlayerRef _holder;

        // Host-only bookkeeping: which players currently know the holder's identity
        // (e.g. via a card ability that grants a conditional reveal to one player).
        private readonly HashSet<PlayerRef> _playersWhoKnow = new HashSet<PlayerRef>();

        public bool HasAssignedHolder => _holder != default;

        /// <summary>Host-only: assign the Whisker Blower to a player (e.g. at match start).</summary>
        public void AssignHolder(PlayerRef player)
        {
            if (!Object.HasStateAuthority) return;
            _holder = player;
            _playersWhoKnow.Clear();
            _playersWhoKnow.Add(player); // the holder always knows their own role
        }

        /// <summary>Host-only: transfer holder status (e.g. via a future trade/steal ability).</summary>
        public void TransferHolder(PlayerRef newHolder)
        {
            if (!Object.HasStateAuthority) return;
            _holder = newHolder;
            _playersWhoKnow.Clear();
            _playersWhoKnow.Add(newHolder);
        }

        /// <summary>
        /// Host-only accessor. Never expose the return value of this to a client-facing
        /// code path — only call it from other host-only logic (ActionResolver, match-end
        /// custodian-guess resolution, etc.).
        /// </summary>
        public PlayerRef GetHolderHostOnly() => _holder;

        public bool DoesPlayerKnow(PlayerRef player) => _playersWhoKnow.Contains(player);

        /// <summary>
        /// Host-only: reveal the holder's identity to a single player only
        /// (conditional card-ability reveal trigger).
        /// </summary>
        public void RevealToPlayer(PlayerRef target)
        {
            if (!Object.HasStateAuthority) return;
            _playersWhoKnow.Add(target);
            RPC_ReceiveTargetedReveal(target, _holder);
        }

        /// <summary>
        /// Host-only: reveal the holder's identity to every player
        /// (correct custodian guess at match end trigger).
        /// </summary>
        public void RevealToAll()
        {
            if (!Object.HasStateAuthority) return;
            foreach (var p in Runner.ActivePlayers)
            {
                _playersWhoKnow.Add(p);
            }
            RPC_ReceiveBroadcastReveal(_holder);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
        private void RPC_ReceiveBroadcastReveal(PlayerRef holder)
        {
            // Client-side: fire a local event/UI hook here (e.g. "Player X was the
            // Whisker Blower!"). Out of scope for the networking layer itself.
        }

        // Targeted delivery uses RpcTargets.Proxies + the [RpcTarget] parameter attribute
        // so only the intended recipient's client actually invokes this method body.
        // Verify this pattern against your installed Fusion 2.1 API surface before shipping.
        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        private void RPC_ReceiveTargetedReveal([RpcTarget] PlayerRef target, PlayerRef holder)
        {
            // Client-side: only the targeted client's Fusion runtime invokes this.
        }
    }
}
