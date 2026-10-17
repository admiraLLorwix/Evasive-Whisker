using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using EvasiveWhisker.Networking.Lan;

namespace EvasiveWhisker.Networking
{
    /// <summary>
    /// Host-only. Applies the disconnect rules when a client drops mid-match:
    ///
    ///  - Host leaves          -> nothing to do here (the server is gone); clients get LanSession.HostLost.
    ///  - Holder leaves        -> match ends for everyone; holder identity revealed, hands not.
    ///  - Anyone else leaves   -> hand reshuffled into the deck, marked forfeited, removed from the
    ///                            turn order; play continues.
    ///  - Fewer than 3 remain  -> match ends; nothing revealed.
    ///
    /// Plain C# class: create it on the host when the match starts, Dispose() when it ends.
    /// </summary>
    public sealed class DisconnectCoordinator : IDisposable
    {
        private readonly NetworkManager _networkManager;
        private readonly ActionResolver _resolver;
        private readonly TurnStateMachine _turns;
        private readonly MatchState _matchState;
        private readonly WhiskerBlowerTracker _tracker;
        private readonly IReadOnlyDictionary<ulong, PlayerState> _playerStates;

        private bool _matchEnded;

        public DisconnectCoordinator(
            NetworkManager networkManager,
            ActionResolver resolver,
            TurnStateMachine turns,
            MatchState matchState,
            WhiskerBlowerTracker tracker,
            IReadOnlyDictionary<ulong, PlayerState> playerStates)
        {
            _networkManager = networkManager;
            _resolver = resolver;
            _turns = turns;
            _matchState = matchState;
            _tracker = tracker;
            _playerStates = playerStates;

            _networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public void Dispose()
        {
            if (_networkManager != null)
                _networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!_networkManager.IsServer || _networkManager.ShutdownInProgress) return;
            if (_matchEnded) return;
            if (clientId == NetworkManager.ServerClientId) return; // host is the server; see summary above

            if (!_playerStates.TryGetValue(clientId, out PlayerState state)) return; // not a match participant
            if (state.HasForfeited.Value) return;

            state.MarkForfeited();

            // Holder leaving ends the match: the hidden role can't be played out without them.
            if (_tracker.HasAssignedHolder && _tracker.GetHolderHostOnly() == clientId)
            {
                EndMatch(MatchEndReason.HolderDisconnected);
                return;
            }

            // Everyone else: forfeit and keep playing.
            _resolver.ForfeitHandOnDisconnect(clientId);
            _turns.RemovePlayer(clientId);

            // TODO: LanConstants.MinPlayers duplicates the lobby minimum; reconcile with GameConstants.
            if (_turns.PlayersInTurnOrder < LanConstants.MinPlayers)
            {
                EndMatch(MatchEndReason.TooFewPlayers);
            }
        }

        private void EndMatch(MatchEndReason reason)
        {
            _matchEnded = true;
            _turns.HaltTurns();

            // Holder disconnect reveals the holder's identity only — never hands.
            // Too-few-players ends with no reveal at all.
            if (reason == MatchEndReason.HolderDisconnected)
            {
                _tracker.RevealToAll();
            }

            _matchState.EndReason.Value = reason;
            Debug.Log($"[Match] Ended early: {reason}");
        }
    }
}
