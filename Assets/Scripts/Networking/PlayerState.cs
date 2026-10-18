using Unity.Netcode;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Per-player networked state. Hand *contents* are never stored here — only the count
    /// is networked, so other clients can render "N cards in hand" without learning what
    /// those cards are. Actual hand contents live host-side in ActionResolver.
    ///
    /// Spawn one per player on the host with ownership, e.g.
    /// networkObject.SpawnAsPlayerObject(clientId). The owner is then available as
    /// OwnerClientId (replaces the old networked Owner field). Ownership does NOT give
    /// write access: only the host writes these variables.
    /// </summary>
    public class PlayerState : NetworkBehaviour
    {
        public readonly NetworkVariable<int> HandCount = new NetworkVariable<int>(0);
        public readonly NetworkVariable<int> Score = new NetworkVariable<int>(0);

        /// <summary>
        /// True once the player has disconnected. With reconnection dropped, a disconnect is an
        /// immediate, permanent forfeit, so this never returns to false. Replaces IsConnected.
        /// </summary>
        public readonly NetworkVariable<bool> HasForfeited = new NetworkVariable<bool>(false);

        /// <summary>Host-only: mark this player as forfeited.</summary>
        public void MarkForfeited()
        {
            if (!IsServer) return;
            HasForfeited.Value = true;
        }
    }
}
