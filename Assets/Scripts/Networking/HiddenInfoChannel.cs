using System;
using System.Collections.Generic;
using Unity.Netcode;
using EvasiveWhisker.Core;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Host-only RPC transport for hidden information delivery — the only place a player's
    /// own hand contents are ever sent over the network, and only ever to that one player.
    /// Two channels:
    ///  - <see cref="DeliverCard"/>: single-card delivery to one player (per-turn draw/steal flow).
    ///  - <see cref="BroadcastAllHands"/>: public reveal of every hand, used only for the
    ///    correct-custodian-guess match ending.
    /// This class holds no hand data itself — it only relays what ActionResolver already
    /// holds host-side, on demand.
    ///
    /// The old DeliverFullHandResync channel is gone: it only existed for reconnection,
    /// which was dropped (a disconnect is now an immediate forfeit).
    ///
    /// NOTE: <see cref="CardInstance"/> implements INetworkSerializable, which is what lets it (and
    /// CardInstance[]) be used as an RPC parameter. Any field added to it must be added to
    /// NetworkSerialize too, or it will silently not travel.
    /// </summary>
    public class HiddenInfoChannel : NetworkBehaviour
    {
        /// <summary>Client-side hook: this client was just handed a card (draw or blind steal).</summary>
        public event Action<CardInstance> CardReceived;

        /// <summary>Client-side hook: every player's hand, publicly revealed at match end.</summary>
        public event Action<IReadOnlyDictionary<ulong, CardInstance[]>> AllHandsRevealed;

        /// <summary>Host-only: deliver a single newly-drawn or newly-stolen card to its new owner.</summary>
        public void DeliverCard(ulong target, CardInstance card)
        {
            if (!IsServer) return;
            // SpecifiedInParams: sent only to `target`'s connection. No other client receives it.
            ReceiveCardRpc(card, RpcTarget.Single(target, RpcTargetUse.Temp));
        }

        /// <summary>
        /// Host-only: publicly reveal all hands (correct custodian guess). Flattened because
        /// RPCs can't carry jagged arrays: hand i is the next handSizes[i] entries of cards.
        /// </summary>
        public void BroadcastAllHands(ulong[] playerIds, int[] handSizes, CardInstance[] cards)
        {
            if (!IsServer) return;
            ReceiveAllHandsRevealRpc(playerIds, handSizes, cards);
        }

        [Rpc(SendTo.SpecifiedInParams)]
        private void ReceiveCardRpc(CardInstance card, RpcParams rpcParams = default)
        {
            // Only the host may hand out cards; ignore anything a modified client tries to send.
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            CardReceived?.Invoke(card);
        }

        [Rpc(SendTo.Everyone)]
        private void ReceiveAllHandsRevealRpc(ulong[] playerIds, int[] handSizes, CardInstance[] cards,
                                              RpcParams rpcParams = default)
        {
            if (rpcParams.Receive.SenderClientId != NetworkManager.ServerClientId) return;
            if (playerIds == null || handSizes == null || cards == null) return;
            if (playerIds.Length != handSizes.Length) return;

            var result = new Dictionary<ulong, CardInstance[]>();
            int offset = 0;
            for (int i = 0; i < playerIds.Length; i++)
            {
                int size = handSizes[i];
                if (size < 0 || offset + size > cards.Length) break; // malformed; stop rather than throw

                var hand = new CardInstance[size];
                Array.Copy(cards, offset, hand, 0, size);
                result[playerIds[i]] = hand;
                offset += size;
            }
            AllHandsRevealed?.Invoke(result);
        }
    }
}
