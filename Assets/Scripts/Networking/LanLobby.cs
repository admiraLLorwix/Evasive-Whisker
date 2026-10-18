using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace EvasiveWhisker.Networking.Lan
{
    /// <summary>One lobby roster entry. Names and ready flags are public info, so they are safe to network.</summary>
    public struct LobbyPlayer : INetworkSerializable, IEquatable<LobbyPlayer>
    {
        public ulong ClientId;
        public FixedString64Bytes Name;
        public bool Ready;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref ClientId);
            serializer.SerializeValue(ref Name);
            serializer.SerializeValue(ref Ready);
        }

        public bool Equals(LobbyPlayer other) =>
            ClientId == other.ClientId && Name.Equals(other.Name) && Ready == other.Ready;
    }

    /// <summary>
    /// Place on an in-scene NetworkObject in the lobby scene.
    /// Host owns the roster; clients only send ready requests. The host never readies (host starts instead).
    /// </summary>
    public sealed class LanLobby : NetworkBehaviour
    {
        public static LanLobby Instance { get; private set; }

        [Tooltip("Scene loaded for everyone when the host starts the match. Must be in Build Settings.")]
        [SerializeField] private string gameSceneName = "Game";

        NetworkList<LobbyPlayer> _players;
        readonly NetworkVariable<bool> _locked = new NetworkVariable<bool>(false);

        /// <summary>Raised on every peer whenever the roster or lock state changes.</summary>
        public event Action LobbyChanged;

        /// <summary>Host only. Raised once the match is locked in; the scene load / match setup hooks in here.</summary>
        public event Action MatchStarting;

        public int PlayerCount => _players.Count;
        public bool IsLocked => _locked.Value;

        void Awake()
        {
            Instance = this;
            _players = new NetworkList<LobbyPlayer>();
        }

        public override void OnDestroy()
        {
            if (Instance == this) Instance = null;
            _players?.Dispose();
            base.OnDestroy();
        }

        public override void OnNetworkSpawn()
        {
            _players.OnListChanged += OnListChanged;
            _locked.OnValueChanged += OnLockedChanged;

            if (IsServer)
            {
                NetworkManager.OnClientConnectedCallback += HostOnClientConnected;
                NetworkManager.OnClientDisconnectCallback += HostOnClientDisconnected;
                foreach (ulong id in NetworkManager.ConnectedClientsIds) AddPlayer(id);
            }
            LobbyChanged?.Invoke();
        }

        public override void OnNetworkDespawn()
        {
            _players.OnListChanged -= OnListChanged;
            _locked.OnValueChanged -= OnLockedChanged;

            if (IsServer && NetworkManager != null)
            {
                NetworkManager.OnClientConnectedCallback -= HostOnClientConnected;
                NetworkManager.OnClientDisconnectCallback -= HostOnClientDisconnected;
            }
        }

        void OnListChanged(NetworkListEvent<LobbyPlayer> _) => LobbyChanged?.Invoke();
        void OnLockedChanged(bool previous, bool current) => LobbyChanged?.Invoke();

        public void GetPlayers(List<LobbyPlayer> buffer)
        {
            buffer.Clear();
            for (int i = 0; i < _players.Count; i++) buffer.Add(_players[i]);
        }

        // ------------------------------------------------------------ Client -> host

        /// <summary>UI entry point for non-host players.</summary>
        public void RequestReady(bool ready)
        {
            if (IsServer) return; // the host does not ready up
            SetReadyRpc(ready);
        }

        [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
        void SetReadyRpc(bool ready, RpcParams rpcParams = default)
        {
            if (_locked.Value) return;

            ulong sender = rpcParams.Receive.SenderClientId;
            if (sender == NetworkManager.ServerClientId) return;

            int index = IndexOf(sender);
            if (index < 0) return;

            LobbyPlayer p = _players[index];
            if (p.Ready == ready) return;
            p.Ready = ready;
            _players[index] = p;
        }

        // ------------------------------------------------------------ Host only

        /// <summary>Whether the host may start right now; reason is filled when it can't.</summary>
        public bool CanStart(out string reason)
        {
            reason = null;
            if (!IsServer) { reason = "Only the host can start the match"; return false; }
            if (_locked.Value) { reason = "Match already started"; return false; }

            int count = _players.Count;
            if (count < LanConstants.MinPlayers)
            {
                reason = "Need at least " + LanConstants.MinPlayers + " players";
                return false;
            }
            if (count > LanConstants.MaxPlayers)
            {
                reason = "Too many players";
                return false;
            }

            for (int i = 0; i < count; i++)
            {
                LobbyPlayer p = _players[i];
                if (p.ClientId != NetworkManager.ServerClientId && !p.Ready)
                {
                    reason = p.Name + " is not ready";
                    return false;
                }
            }
            return true;
        }

        public bool TryStartMatch(out string error)
        {
            if (!CanStart(out error)) return false;

            _locked.Value = true;
            LanSession.Instance.CloseLobby(); // stop advertising, refuse new joins

            // Snapshot the roster: this lobby object is destroyed by the scene load below.
            var participants = new List<ulong>(_players.Count);
            for (int i = 0; i < _players.Count; i++) participants.Add(_players[i].ClientId);
            LanSession.Instance.SetMatchParticipants(participants);

            MatchStarting?.Invoke();

            // The game scene's MatchSetup takes over once every client has finished loading.
            NetworkManager.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
            return true;
        }

        void HostOnClientConnected(ulong clientId) => AddPlayer(clientId);

        void HostOnClientDisconnected(ulong clientId)
        {
            // After lock, forfeit handling belongs to the turn layer, not the lobby.
            if (_locked.Value) return;

            int index = IndexOf(clientId);
            if (index >= 0) _players.RemoveAt(index);
            LanSession.Instance.ForgetPlayer(clientId);
        }

        void AddPlayer(ulong clientId)
        {
            if (_locked.Value || IndexOf(clientId) >= 0) return;

            string name = LanSession.Instance.GetPlayerName(clientId);
            _players.Add(new LobbyPlayer
            {
                ClientId = clientId,
                Name = new FixedString64Bytes(name),
                Ready = false
            });
        }

        int IndexOf(ulong clientId)
        {
            for (int i = 0; i < _players.Count; i++)
                if (_players[i].ClientId == clientId) return i;
            return -1;
        }
    }
}
