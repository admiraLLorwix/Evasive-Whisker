using System.Collections.Generic;
using UnityEngine;
using Fusion;
using EvasiveWhisker.Core;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Host-only RPC transport for hidden information delivery — the only place a player's
    /// own hand contents are ever sent over the network, and only ever to that one player.
    /// Two channels:
    ///  - <see cref="DeliverCard"/>: single-card delivery for the normal per-turn draw/steal flow.
    ///  - <see cref="DeliverFullHandResync"/>: one-shot full-hand push, used on reconnect to
    ///    rebuild a client's local hand state after it may have lost everything.
    /// This class holds no hand data itself — it only relays what ActionResolver already
    /// holds host-side, on demand.
    /// </summary>
    public class HiddenInfoChannel : NetworkBehaviour
    {
        /// <summary>Host-only: deliver a single newly-drawn or newly-stolen card to its new owner.</summary>
        public void DeliverCard(PlayerRef target, Card card)
        {
            if (!Object.HasStateAuthority) return;
            RPC_ReceiveCard(target, card);
        }

        /// <summary>
        /// Host-only: deliver a player's complete hand in one shot. Used on reconnect to
        /// correct any local hand state the client lost while disconnected. This is a full
        /// source-of-truth push, not an incremental update — the client should replace its
        /// local hand wholesale rather than merge.
        ///
        /// <paramref name="hand"/> must have Count &lt;= GameConstants.MaxHandSize; the
        /// underlying array is sent zero-padded to MaxHandSize with <c>count</c> indicating
        /// how many entries are valid.
        /// </summary>
        public void DeliverFullHandResync(PlayerRef target, IReadOnlyList<Card> hand)
        {
            if (!Object.HasStateAuthority) return;

            if (hand.Count > GameConstants.MaxHandSize)
            {
                Debug.LogError(
                    $"HiddenInfoChannel: hand size {hand.Count} exceeds GameConstants.MaxHandSize " +
                    $"({GameConstants.MaxHandSize}) for player {target}. Resync aborted — raise " +
                    "GameConstants.MaxHandSize or investigate why the hand grew this large.");
                return;
            }

            var padded = new Card[GameConstants.MaxHandSize];
            for (int i = 0; i < hand.Count; i++)
            {
                padded[i] = hand[i];
            }

            RPC_ReceiveHandResync(target, padded, hand.Count);
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        private void RPC_ReceiveCard([RpcTarget] PlayerRef target, Card card)
        {
            // Client-side: add this card to local hand-view state. Hook point for whatever
            // client-side hand UI/model exists — out of scope for the networking layer.
        }

        [Rpc(RpcSources.StateAuthority, RpcTargets.Proxies)]
        private void RPC_ReceiveHandResync([RpcTarget] PlayerRef target, Card[] hand, int count)
        {
            // Client-side: replace local hand-view state wholesale with hand[0..count).
            // Ignore the zero-padded tail beyond `count`.
        }
    }
}
