using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace EvasiveWhisker.Networking.Lan
{
    /// <summary>
    /// Owns starting/stopping the peer-hosted session over UnityTransport. The host is the server.
    ///
    /// Scene setup: put this on a persistent object next to the NetworkManager. In the NetworkManager
    /// inspector: Network Transport = UnityTransport, Player Prefab = none (players are not spawned objects).
    /// </summary>
    [RequireComponent(typeof(LanDiscoveryHost))]
    public sealed class LanSession : MonoBehaviour
    {
        public static LanSession Instance { get; private set; }

        LanDiscoveryHost _discovery;
        readonly Dictionary<ulong, string> _namesByClient = new Dictionary<ulong, string>();
        bool _callbacksHooked;
        bool _localWasConnected;

        public bool LobbyOpen { get; private set; }
        public string RoomName { get; private set; }
        public string LocalPlayerName { get; private set; }

        /// <summary>
        /// Host only. The lobby roster (client IDs, join order) captured when the match starts.
        /// The lobby object is destroyed by the scene load, so the game scene reads it from here.
        /// </summary>
        public IReadOnlyList<ulong> MatchParticipants => _matchParticipants;
        readonly List<ulong> _matchParticipants = new List<ulong>();

        public void SetMatchParticipants(IEnumerable<ulong> participants)
        {
            _matchParticipants.Clear();
            _matchParticipants.AddRange(participants);
        }

        /// <summary>Client only. Host rejected us (full, in progress, version mismatch) or was unreachable.</summary>
        public event Action<string> JoinFailed;

        /// <summary>Client only. We were connected and then lost the host (host quit, or our network dropped).
        /// Under the no-migration rule this means the match is over for this client.</summary>
        public event Action HostLost;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _discovery = GetComponent<LanDiscoveryHost>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            UnhookCallbacks();
        }

        // ---------------------------------------------------------------- Host

        public bool StartHost(string roomName, string hostName)
        {
            var nm = NetworkManager.Singleton;
            var utp = nm != null ? nm.NetworkConfig.NetworkTransport as UnityTransport : null;
            if (utp == null)
            {
                Debug.LogError("[LAN] NetworkManager with UnityTransport is required.");
                return false;
            }

            LocalPlayerName = CleanName(hostName, "Host");
            RoomName = CleanName(roomName, LocalPlayerName + "'s room");

            ConfigureTransport(utp);
            utp.SetConnectionData("0.0.0.0", LanConstants.GamePort, "0.0.0.0"); // listen on all interfaces

            nm.NetworkConfig.ConnectionApproval = true;
            nm.ConnectionApprovalCallback = HandleApproval;

            _namesByClient.Clear();
            _namesByClient[NetworkManager.ServerClientId] = LocalPlayerName;
            LobbyOpen = true;
            HookCallbacks(nm);

            if (!nm.StartHost())
            {
                LobbyOpen = false;
                Debug.LogError("[LAN] StartHost failed (is the port already in use?).");
                return false;
            }

            _discovery.Begin(BuildRoomInfo);
            return true;
        }

        /// <summary>Called by the lobby when the host starts the match: stop advertising, refuse new joins.</summary>
        public void CloseLobby()
        {
            LobbyOpen = false;
            _discovery.End();
        }

        LanRoomInfo BuildRoomInfo()
        {
            var nm = NetworkManager.Singleton;
            int count = nm != null ? nm.ConnectedClientsIds.Count : 0;
            return new LanRoomInfo
            {
                RoomName = RoomName,
                HostName = LocalPlayerName,
                PlayerCount = count,
                MaxPlayers = LanConstants.MaxPlayers,
                GamePort = LanConstants.GamePort,
                Open = LobbyOpen && count < LanConstants.MaxPlayers
            };
        }

        void HandleApproval(NetworkManager.ConnectionApprovalRequest request,
                            NetworkManager.ConnectionApprovalResponse response)
        {
            var nm = NetworkManager.Singleton;
            response.CreatePlayerObject = false; // roster lives in LanLobby / PlayerState, not spawned player objects
            response.Approved = false;

            if (request.ClientNetworkId == NetworkManager.ServerClientId)
            {
                response.Approved = true; // the host's own connection
                return;
            }

            if (!LobbyOpen) { response.Reason = "Match already in progress"; return; }
            if (nm.ConnectedClientsIds.Count >= LanConstants.MaxPlayers) { response.Reason = "Room is full"; return; }

            // Payload: "<protocolVersion>|<playerName>"
            string[] parts = Encoding.UTF8.GetString(request.Payload ?? Array.Empty<byte>()).Split(new[] { '|' }, 2);
            if (parts.Length != 2 || !int.TryParse(parts[0], out int version) || version != LanConstants.ProtocolVersion)
            {
                response.Reason = "Game version mismatch";
                return;
            }

            _namesByClient[request.ClientNetworkId] = MakeUniqueName(CleanName(parts[1], "Player"));
            response.Approved = true;
        }

        // ---------------------------------------------------------------- Client

        public bool JoinRoom(string hostAddress, ushort port, string playerName)
        {
            var nm = NetworkManager.Singleton;
            var utp = nm != null ? nm.NetworkConfig.NetworkTransport as UnityTransport : null;
            if (utp == null)
            {
                Debug.LogError("[LAN] NetworkManager with UnityTransport is required.");
                return false;
            }

            if (!IPAddress.TryParse(hostAddress, out _))
            {
                JoinFailed?.Invoke("Invalid IP address");
                return false;
            }

            LocalPlayerName = CleanName(playerName, "Player");
            ConfigureTransport(utp);
            utp.SetConnectionData(hostAddress, port);

            nm.NetworkConfig.ConnectionApproval = true;
            nm.NetworkConfig.ConnectionData =
                Encoding.UTF8.GetBytes(LanConstants.ProtocolVersion + "|" + LocalPlayerName);

            _localWasConnected = false;
            HookCallbacks(nm);

            if (!nm.StartClient())
            {
                JoinFailed?.Invoke("Could not start client");
                return false;
            }
            return true;
        }

        /// <summary>Manual-IP fallback using the default game port.</summary>
        public bool JoinByIp(string hostAddress, string playerName) =>
            JoinRoom(hostAddress, LanConstants.GamePort, playerName);

        // ---------------------------------------------------------------- Shared

        public void Leave()
        {
            LobbyOpen = false;
            _discovery.End();
            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) nm.Shutdown();
            _namesByClient.Clear();
            _matchParticipants.Clear();
            _localWasConnected = false;
        }

        /// <summary>Host only. Display name recorded at approval time.</summary>
        public string GetPlayerName(ulong clientId) =>
            _namesByClient.TryGetValue(clientId, out string n) ? n : "Player " + clientId;

        public void ForgetPlayer(ulong clientId) => _namesByClient.Remove(clientId);

        static void ConfigureTransport(UnityTransport utp)
        {
            // TODO: verify these member names against the installed Netcode / Unity Transport version.
            utp.ConnectTimeoutMS = LanConstants.ConnectTimeoutMs;
            utp.MaxConnectAttempts = LanConstants.MaxConnectAttempts;
            utp.DisconnectTimeoutMS = LanConstants.DisconnectTimeoutMs;
        }

        void HookCallbacks(NetworkManager nm)
        {
            if (_callbacksHooked) return;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            _callbacksHooked = true;
        }

        void UnhookCallbacks()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && _callbacksHooked)
            {
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            _callbacksHooked = false;
        }

        void OnClientConnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null && !nm.IsServer && clientId == nm.LocalClientId)
                _localWasConnected = true;
        }

        void OnClientDisconnected(ulong clientId)
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer) return; // host-side handling lives in LanLobby / the turn layer

            if (clientId != nm.LocalClientId) return;

            if (_localWasConnected)
            {
                _localWasConnected = false;
                HostLost?.Invoke();
            }
            else
            {
                string reason = nm.DisconnectReason;
                JoinFailed?.Invoke(string.IsNullOrEmpty(reason) ? "Could not reach the host" : reason);
            }
        }

        static string CleanName(string raw, string fallback)
        {
            string s = LanRoomInfo.Sanitize(raw, LanConstants.MaxNameLength);
            return string.IsNullOrWhiteSpace(s) ? fallback : s;
        }

        string MakeUniqueName(string name)
        {
            string candidate = name;
            int suffix = 2;
            while (IsNameTaken(candidate))
                candidate = name + " (" + suffix++ + ")";
            return candidate;
        }

        bool IsNameTaken(string name)
        {
            foreach (var existing in _namesByClient.Values)
                if (string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
