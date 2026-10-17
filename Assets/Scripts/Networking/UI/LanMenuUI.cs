using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

namespace EvasiveWhisker.Networking.Lan.UI
{
    /// <summary>
    /// Canvas-based replacement for LanTestPanel. Same flow (host / discover / join by IP /
    /// roster / ready / start / leave), driven by real uGUI widgets and two row prefabs
    /// instead of IMGUI. Nothing in LanSession/LanLobby/LanDiscoveryClient needed to change —
    /// this only replaces how the result is drawn.
    /// </summary>
    public sealed class LanMenuUI : MonoBehaviour
    {
        [Header("Scene refs")]
        [SerializeField] private LanDiscoveryClient discoveryClient;

        [Header("Panels")]
        [SerializeField] private GameObject menuPanel;   // host/join screen
        [SerializeField] private GameObject lobbyPanel;  // roster screen

        [Header("Menu panel widgets")]
        [SerializeField] private TMP_InputField nameField;
        [SerializeField] private TMP_InputField roomNameField;
        [SerializeField] private Button hostButton;
        [SerializeField] private TMP_InputField ipField;
        [SerializeField] private Button joinByIpButton;
        [SerializeField] private Transform roomListContent;   // parent with a Layout Group
        [SerializeField] private RoomListItemUI roomListItemPrefab;
        [SerializeField] private TMP_Text noRoomsLabel;       // shown when the list is empty

        [Header("Lobby panel widgets")]
        [SerializeField] private TMP_Text yourIpText;
        [SerializeField] private Transform playerListContent; // parent with a Layout Group
        [SerializeField] private PlayerListItemUI playerListItemPrefab;
        [SerializeField] private Toggle readyToggle;          // client only
        [SerializeField] private Button startButton;          // host only
        [SerializeField] private TMP_Text startBlockedReasonText;
        [SerializeField] private Button leaveButton;

        [Header("Shared")]
        [SerializeField] private TMP_Text statusText;

        private readonly List<RoomListItemUI> _roomRows = new List<RoomListItemUI>();
        private readonly List<PlayerListItemUI> _playerRows = new List<PlayerListItemUI>();
        private readonly List<LobbyPlayer> _rosterBuffer = new List<LobbyPlayer>();

        private bool _subscribedToLobby;
        private bool _settingReadyFromCode;

        private void Awake()
        {
            if (discoveryClient == null) discoveryClient = FindAnyObjectByType<LanDiscoveryClient>();

            if (!string.IsNullOrEmpty(SystemInfo.deviceName) && nameField != null)
                nameField.text = SystemInfo.deviceName;

            hostButton.onClick.AddListener(OnHostClicked);
            joinByIpButton.onClick.AddListener(OnJoinByIpClicked);
            leaveButton.onClick.AddListener(OnLeaveClicked);
            if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
            if (readyToggle != null) readyToggle.onValueChanged.AddListener(OnReadyToggled);
        }

        private void OnEnable()
        {
            var session = LanSession.Instance;
            if (session != null)
            {
                session.JoinFailed += OnJoinFailed;
                session.HostLost += OnHostLost;
            }
            if (discoveryClient != null) discoveryClient.RoomsChanged += RefreshRoomList;

            ShowMenu();
            StartSearching();
        }

        private void OnDisable()
        {
            var session = LanSession.Instance;
            if (session != null)
            {
                session.JoinFailed -= OnJoinFailed;
                session.HostLost -= OnHostLost;
            }
            if (discoveryClient != null) discoveryClient.RoomsChanged -= RefreshRoomList;
            UnsubscribeFromLobby();
        }

        private void Update()
        {
            var nm = NetworkManager.Singleton;
            bool inSession = nm != null && nm.IsListening;

            if (inSession && menuPanel.activeSelf) ShowLobby();
            else if (!inSession && lobbyPanel.activeSelf) ShowMenu();
        }

        // ---------------------------------------------------------------- Menu panel

        private void ShowMenu()
        {
            menuPanel.SetActive(true);
            lobbyPanel.SetActive(false);
            UnsubscribeFromLobby();
            ClearPlayerList(); // leftover rows from the previous session must not survive into the next lobby
        }

        private void ClearPlayerList()
        {
            foreach (PlayerListItemUI row in _playerRows)
                if (row != null) Destroy(row.gameObject);
            _playerRows.Clear();
            _rosterBuffer.Clear();
        }

        private void OnHostClicked()
        {
            SetStatus("");
            if (LanSession.Instance.StartHost(roomNameField.text, nameField.text))
            {
                if (yourIpText != null)
                    yourIpText.text = "Your IP: " + string.Join(", ", LanNetUtil.GetLocalIPv4Addresses());
                StopSearching();
            }
            else
            {
                SetStatus("Could not start host (see console).");
            }
        }

        private void OnJoinByIpClicked() => Join(ipField.text.Trim(), LanConstants.GamePort);

        private void Join(string address, ushort port)
        {
            SetStatus("Connecting...");
            StopSearching();
            if (!LanSession.Instance.JoinRoom(address, port, nameField.text))
            {
                StartSearching(); // start failed immediately; resume discovery
            }
        }

        private void StartSearching()
        {
            if (discoveryClient != null && !discoveryClient.IsListening) discoveryClient.StartListening();
            RefreshRoomList();
        }

        private void StopSearching()
        {
            if (discoveryClient != null) discoveryClient.StopListening();
            ClearRoomList();
        }

        private void RefreshRoomList()
        {
            ClearRoomList();
            if (discoveryClient == null) return;

            int count = 0;
            foreach (DiscoveredRoom room in discoveryClient.Rooms)
            {
                RoomListItemUI row = Instantiate(roomListItemPrefab, roomListContent);
                row.Bind(room, r => Join(r.HostAddress, r.Info.GamePort));
                _roomRows.Add(row);
                count++;
            }

            if (noRoomsLabel != null) noRoomsLabel.gameObject.SetActive(count == 0);
        }

        private void ClearRoomList()
        {
            foreach (RoomListItemUI row in _roomRows)
                if (row != null) Destroy(row.gameObject);
            _roomRows.Clear();
        }

        // ---------------------------------------------------------------- Lobby panel

        private void ShowLobby()
        {
            menuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
            SubscribeToLobby();
            RefreshRoster();
        }

        private void SubscribeToLobby()
        {
            if (_subscribedToLobby) return;
            LanLobby lobby = LanLobby.Instance;
            if (lobby == null) return;
            lobby.LobbyChanged += RefreshRoster;
            _subscribedToLobby = true;
        }

        private void UnsubscribeFromLobby()
        {
            if (!_subscribedToLobby) return;
            LanLobby lobby = LanLobby.Instance;
            if (lobby != null) lobby.LobbyChanged -= RefreshRoster;
            _subscribedToLobby = false;
        }

        private void RefreshRoster()
        {
            var nm = NetworkManager.Singleton;
            LanLobby lobby = LanLobby.Instance;

            if (nm == null || lobby == null || !lobby.IsSpawned)
            {
                SubscribeToLobby(); // lobby may have just spawned; try again next change
                return;
            }

            lobby.GetPlayers(_rosterBuffer);

            while (_playerRows.Count < _rosterBuffer.Count)
            {
                PlayerListItemUI row = Instantiate(playerListItemPrefab, playerListContent);
                _playerRows.Add(row);
            }
            while (_playerRows.Count > _rosterBuffer.Count)
            {
                int last = _playerRows.Count - 1;
                Destroy(_playerRows[last].gameObject);
                _playerRows.RemoveAt(last);
            }

            bool localIsReady = false;
            for (int i = 0; i < _rosterBuffer.Count; i++)
            {
                LobbyPlayer player = _rosterBuffer[i];
                bool isHost = player.ClientId == NetworkManager.ServerClientId;
                bool isLocal = player.ClientId == nm.LocalClientId;
                _playerRows[i].Bind(player, isHost, isLocal);
                if (isLocal) localIsReady = player.Ready;
            }

            bool isHostLocally = nm.IsServer;
            if (readyToggle != null)
            {
                readyToggle.gameObject.SetActive(!isHostLocally);
                if (readyToggle.isOn != localIsReady)
                {
                    _settingReadyFromCode = true;
                    readyToggle.isOn = localIsReady;
                    _settingReadyFromCode = false;
                }
            }

            if (startButton != null)
            {
                startButton.gameObject.SetActive(isHostLocally);
                bool canStart = lobby.CanStart(out string reason);
                startButton.interactable = canStart;
                if (startBlockedReasonText != null)
                    startBlockedReasonText.text = canStart ? "" : reason;
            }
        }

        private void OnReadyToggled(bool ready)
        {
            if (_settingReadyFromCode) return;
            LanLobby.Instance?.RequestReady(ready);
        }

        private void OnStartClicked()
        {
            if (LanLobby.Instance != null && !LanLobby.Instance.TryStartMatch(out string error))
                SetStatus(error);
        }

        private void OnLeaveClicked()
        {
            LanSession.Instance.Leave();
            SetStatus("");
            ShowMenu();
            StartSearching();
        }

        // ---------------------------------------------------------------- Session events

        private void OnJoinFailed(string reason)
        {
            SetStatus("Could not join: " + reason);
            ShowMenu();
            StartSearching();
        }

        private void OnHostLost()
        {
            SetStatus("The host left, or the connection was lost.");
            ShowMenu();
            StartSearching();
        }

        private void SetStatus(string message)
        {
            if (statusText != null) statusText.text = message;
        }
    }
}
