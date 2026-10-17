using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace EvasiveWhisker.Networking.Lan
{
    /// <summary>
    /// Throwaway test UI (IMGUI, no Canvas needed) for exercising the LAN flow:
    /// host / discover / join by IP / roster / ready / start / leave.
    /// Put it in the same scene as LanSession, LanLobby and a LanDiscoveryClient.
    /// Replace with real UI later; nothing depends on this class.
    /// </summary>
    public sealed class LanTestPanel : MonoBehaviour
    {
        [SerializeField] private LanDiscoveryClient discoveryClient;

        private string _playerName = "Player";
        private string _roomName = "Test room";
        private string _manualIp = "127.0.0.1";
        private string _status = "";
        private string _localIps = "";
        private bool _pendingReset;
        private readonly List<LobbyPlayer> _roster = new List<LobbyPlayer>();

        private void Awake()
        {
            if (discoveryClient == null) discoveryClient = FindFirstObjectByType<LanDiscoveryClient>();
        }

        private void Start()
        {
            if (LanSession.Instance == null)
            {
                Debug.LogError("[LanTestPanel] No LanSession in the scene.");
                return;
            }
            LanSession.Instance.JoinFailed += OnJoinFailed;
            LanSession.Instance.HostLost += OnHostLost;
            StartSearching();
        }

        private void OnDestroy()
        {
            var session = LanSession.Instance;
            if (session == null) return;
            session.JoinFailed -= OnJoinFailed;
            session.HostLost -= OnHostLost;
        }

        private void Update()
        {
            // Reset outside the disconnect callback so we never shut down from inside it.
            if (!_pendingReset) return;
            _pendingReset = false;
            LanSession.Instance.Leave();
            StartSearching();
        }

        private void OnJoinFailed(string reason)
        {
            _status = "Could not join: " + reason;
            _pendingReset = true;
        }

        private void OnHostLost()
        {
            _status = "The host left, or the connection was lost.";
            _pendingReset = true;
        }

        private void StartSearching()
        {
            if (discoveryClient != null && !discoveryClient.IsListening) discoveryClient.StartListening();
        }

        private void StopSearching()
        {
            if (discoveryClient != null) discoveryClient.StopListening();
        }

        // ---------------------------------------------------------------- GUI

        private void OnGUI()
        {
            float scale = Mathf.Max(1f, Screen.height / 600f); // readable on phones
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));

            float width = Mathf.Min(460f, Screen.width / scale - 20f);
            GUILayout.BeginArea(new Rect(10f, 10f, width, Screen.height / scale - 20f));

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsListening) DrawSession(nm);
            else DrawMenu();

            if (!string.IsNullOrEmpty(_status)) GUILayout.Label(_status);
            GUILayout.EndArea();
        }

        private void DrawMenu()
        {
            GUILayout.Label("EVASIVE WHISKER: LAN TEST");

            GUILayout.BeginHorizontal();
            GUILayout.Label("Name", GUILayout.Width(50f));
            _playerName = GUILayout.TextField(_playerName, LanConstants.MaxNameLength);
            GUILayout.EndHorizontal();

            GUILayout.Space(8f);
            GUILayout.Label("Host a game");
            _roomName = GUILayout.TextField(_roomName, LanConstants.MaxNameLength);
            if (GUILayout.Button("Host"))
            {
                if (LanSession.Instance.StartHost(_roomName, _playerName))
                {
                    _status = "";
                    _localIps = string.Join(", ", LanNetUtil.GetLocalIPv4Addresses());
                    StopSearching();
                }
                else _status = "Could not start host (see console).";
            }

            GUILayout.Space(8f);
            GUILayout.Label("Games found on this network");

            DiscoveredRoom? joinTarget = null;
            int shown = 0;
            if (discoveryClient != null)
            {
                foreach (DiscoveredRoom room in discoveryClient.Rooms)
                {
                    shown++;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{room.Info.RoomName} ({room.Info.HostName})  " +
                                    $"{room.Info.PlayerCount}/{room.Info.MaxPlayers}  {room.HostAddress}");
                    GUI.enabled = room.Info.Open;
                    if (GUILayout.Button("Join", GUILayout.Width(60f))) joinTarget = room;
                    GUI.enabled = true;
                    GUILayout.EndHorizontal();
                }
            }
            if (shown == 0) GUILayout.Label("  (none yet, waiting for broadcasts)");

            // Act after the loop: joining stops discovery, which clears the list being iterated.
            if (joinTarget.HasValue) Join(joinTarget.Value.HostAddress, joinTarget.Value.Info.GamePort);

            GUILayout.Space(8f);
            GUILayout.Label("Or join by IP");
            GUILayout.BeginHorizontal();
            _manualIp = GUILayout.TextField(_manualIp);
            if (GUILayout.Button("Join by IP", GUILayout.Width(90f))) Join(_manualIp.Trim(), LanConstants.GamePort);
            GUILayout.EndHorizontal();
        }

        private void Join(string address, ushort port)
        {
            _status = "Connecting...";
            StopSearching();
            if (!LanSession.Instance.JoinRoom(address, port, _playerName))
            {
                StartSearching();
            }
        }

        private void DrawSession(NetworkManager nm)
        {
            if (!nm.IsServer && !nm.IsConnectedClient)
            {
                GUILayout.Label("Connecting...");
                DrawLeaveButton();
                return;
            }

            string role = nm.IsHost ? "HOST" : "CLIENT";
            GUILayout.Label($"{role}   (client id {nm.LocalClientId})");
            if (nm.IsServer) GUILayout.Label($"Your IP: {_localIps}   Game port: {LanConstants.GamePort}");

            LanLobby lobby = LanLobby.Instance;
            if (lobby == null || !lobby.IsSpawned)
            {
                GUILayout.Label("Waiting for lobby...");
                DrawLeaveButton();
                return;
            }

            lobby.GetPlayers(_roster);
            GUILayout.Label($"Players ({_roster.Count}/{LanConstants.MaxPlayers})");

            bool localReady = false;
            foreach (LobbyPlayer p in _roster)
            {
                bool isHost = p.ClientId == NetworkManager.ServerClientId;
                bool isMe = p.ClientId == nm.LocalClientId;
                string tag = isHost ? "HOST" : (p.Ready ? "READY" : "not ready");
                GUILayout.Label($"   {p.Name}   [{tag}]{(isMe ? "   <- you" : "")}");
                if (isMe) localReady = p.Ready;
            }

            GUILayout.Space(6f);
            if (!nm.IsServer)
            {
                bool wantReady = GUILayout.Toggle(localReady, "  I'm ready");
                if (wantReady != localReady) lobby.RequestReady(wantReady);
            }
            else
            {
                bool canStart = lobby.CanStart(out string reason);
                GUI.enabled = canStart;
                if (GUILayout.Button("Start match") && !lobby.TryStartMatch(out string error))
                    _status = error;
                GUI.enabled = true;
                if (!canStart && !string.IsNullOrEmpty(reason)) GUILayout.Label("   " + reason);
            }

            GUILayout.Space(6f);
            DrawLeaveButton();
        }

        private void DrawLeaveButton()
        {
            if (!GUILayout.Button("Leave")) return;
            LanSession.Instance.Leave();
            _status = "";
            StartSearching();
        }
    }
}
