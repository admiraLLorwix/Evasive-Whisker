using System;
using System.Collections.Generic;
using Unity.Netcode;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Tracks who holds the Whisker Blower card. The holder's identity is intentionally
    /// NEVER a NetworkVariable — it must not exist as shared network state. It lives
    /// only in host memory as a plain field, and is only ever pushed to clients via a
    /// targeted RPC (conditional card-ability reveal) or a single all-players broadcast RPC
    /// (correct custodian guess at match end, or the holder disconnecting) — the only reveal
    /// paths for holder identity.
    ///
    /// This class is still a NetworkBehaviour (on a spawned NetworkObject) purely so it can
    /// define RPC methods.
    /// </summary>
    public class WhiskerBlowerTracker : NetworkBehaviour
    {
        // Host-only. Never a NetworkVariable. NoPlayer means "no holder assigned yet"
        // (ulong default 0 can't be used as the sentinel: client ID 0 is the host).
        private ulong _holder = MatchState.NoPlayer;

        // Host-only bookkeeping: which players currently know the holder's identity
        // (e.g. via a card ability that grants a conditional reveal to one player).
        private readonly HashSet<ulong> _playersWhoKnow = new HashSet<ulong>();

        /// <summary>
        /// Client-side hook: raised on a client when the host reveals the holder to it
        /// (targeted or broadcast). Argument is the holder's client ID.
        /// </summary>
        public event Action<ulong> HolderRevealed;

        public bool HasAssignedHolder => _holder != MatchState.NoPlayer;

        /// <summary>Host-only: assign the Whisker Blower to a player (e.g. at match start).</summary>
        public void AssignHolder(ulong player)
        {
            if (!IsServer) return;
            _holder = player;
            _playersWhoKnow.Clear();
            _playersWhoKnow.Add(player); // the holder always knows their own role
        }

        /// <summary>Host-only: transfer holder status (e.g. via a future trade/steal ability).</summary>
        public void TransferHolder(ulong newHolder)
        {
            if (!IsServer) return;
            _holder = newHolder;
            _playersWhoKnow.Clear();
            _playersWhoKnow.Add(newHolder);
        }

        /// <summary>
        /// Host-only accessor. Never expose the return value of this to a client-facing
        /// code path — only call it from other host-only logic (ActionResolver, match-end
        /// custodian-guess resolution, DisconnectCoordinator, etc.).
        /// </summary>
        public ulong GetHolderHostOnly() => _holder;

        public bool DoesPlayerKnow(ulong player) => _playersWhoKnow.Contains(player);

        /// <summary>
        /// Host-only: reveal the holder's identity to a single player only
        /// (conditional card-ability reveal trigger). SpecifiedInParams means the message is
        /// sent to that one client's connection only — nobody else receives it.
        /// </summary>
        public void RevealToPlayer(ulong target)
        {
            if (!IsServer || !HasAssignedHolder) return;
            _playersWhoKnow.Add(target);
            ReceiveTargetedRevealRpc(_holder, RpcTarget.Single(target, RpcTargetUse.Temp));
        }

        /// <summary>
        /// Host-only: reveal the holder's identity to every player
        /// (correct custodian guess, or holder disconnect).
        /// </summary>
        public void RevealToAll()
        {
            if (!IsServer || !HasAssignedHolder) return;
            foreach (ulong id in NetworkManager.ConnectedClientsIds)
            {
                _playersWhoKnow.Add(id);
            }
            ReceiveBroadcastRevealRpc(_holder);
        }

        [Rpc(SendTo.Everyone)]
        private void ReceiveBroadcastRevealRpc(ulong holder, RpcParams rpcParams = default)
        {
            // Only the host may announce a holder. Without this check a modified client could
            // invoke this RPC itself and make everyone "learn" a fake holder.
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            HolderRevealed?.Invoke(holder);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ReceiveTargetedRevealRpc(ulong holder, RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            HolderRevealed?.Invoke(holder);
        }
    }
}
