using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Networking.Lan;
using EvasiveWhisker.Networking.Lan.UI;
using Unity.Netcode;

namespace EvasiveWhisker.UI
{
    public class UIManager : MonoBehaviour
    {
        [Header("UI Panels")]
        [SerializeField] private GameObject startPanel;
        [SerializeField] private GameObject homePanel;
        [SerializeField] private GameObject playPanel;
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private GameObject gamePanel;

        [Header("Start Screen")]
        [SerializeField] private Button startTouchButton;

        [Header("Home Screen")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;

        [Header("PlayPanel Controls")]
        [SerializeField] private TMP_InputField playerNameInput;
        [SerializeField] private TMP_InputField hostRoomNameInput;
        [SerializeField] private Button hostButton;
        [SerializeField] private TMP_InputField joinIpInput;
        [SerializeField] private Button joinIpButton;
        [SerializeField] private TMP_Text playPanelIpLabel;
        [SerializeField] private Button playBackButton;

        [Header("PlayPanel Room List")]
        [SerializeField] private Transform roomListContainer;
        [SerializeField] private RoomListItemUI roomItemPrefab;
        [SerializeField] private TMP_Text noRoomsText;

        [Header("LobbyPanel Controls")]
        [SerializeField] private Button readyButton;
        [SerializeField] private Image readyButtonImage;
        [SerializeField] private TMP_Text readyButtonText;
        [SerializeField] private Color readyColor = Color.green;
        [SerializeField] private Color notReadyColor = Color.red;
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private Button startMatchButton;
        [SerializeField] private TMP_Text lobbyIpLabel;
        [SerializeField] private TMP_Text roomNameLabel;

        [Header("LobbyPanel Player List")]
        [SerializeField] private Transform playerListContainer;
        [SerializeField] private PlayerListItemUI playerItemPrefab;

        [Header("LAN Discovery")]
        [SerializeField] private LanDiscoveryClient discoveryClient;

        private bool _isLocalReady = false;
        private readonly List<LobbyPlayer> _rosterBuffer = new List<LobbyPlayer>();
        private bool _subscribedToLobby = false;

        private void Awake()
        {
            // Navigation
            if (startTouchButton != null) startTouchButton.onClick.AddListener(ShowHomeScreen);
            if (playButton != null) playButton.onClick.AddListener(ShowPlayScreen);
            if (exitButton != null) exitButton.onClick.AddListener(QuitGame);
            if (playBackButton != null) playBackButton.onClick.AddListener(ShowHomeScreen);

            // PlayPanel Actions
            if (hostButton != null) hostButton.onClick.AddListener(OnHostClicked);
            if (joinIpButton != null) joinIpButton.onClick.AddListener(OnJoinIpClicked);

            // LobbyPanel Actions
            if (readyButton != null) readyButton.onClick.AddListener(OnReadyClicked);
            if (leaveLobbyButton != null) leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
            if (startMatchButton != null) startMatchButton.onClick.AddListener(OnStartMatchClicked);

            ShowStartScreen();
        }

        private void Start()
        {
            // Default Player Name Setup
            if (playerNameInput != null && string.IsNullOrWhiteSpace(playerNameInput.text))
            {
                playerNameInput.text = "Player " + Random.Range(100, 999);
            }

            if (discoveryClient == null)
            {
                discoveryClient = FindAnyObjectByType<LanDiscoveryClient>();
            }

            if (LanSession.Instance != null)
            {
                LanSession.Instance.JoinFailed += OnJoinFailed;
                LanSession.Instance.HostLost += OnHostLost;
            }
        }

        private void OnDestroy()
        {
            StopDiscovery();
            UnsubscribeFromLobby();

            if (LanSession.Instance != null)
            {
                LanSession.Instance.JoinFailed -= OnJoinFailed;
                LanSession.Instance.HostLost -= OnHostLost;
            }
        }

        private void Update()
        {
            if (lobbyPanel != null && lobbyPanel.activeSelf)
            {
                UpdateStartMatchButton();
            }
        }

        // --- PlayPanel Handlers ---

        private void OnHostClicked()
        {
            string pName = GetValidatedPlayerName();
            string rName = (hostRoomNameInput != null && !string.IsNullOrWhiteSpace(hostRoomNameInput.text))
                ? hostRoomNameInput.text.Trim()
                : pName + "'s Lobby";

            StopDiscovery();

            if (LanSession.Instance != null && LanSession.Instance.StartHost(rName, pName))
            {
                ShowLobbyScreen();
            }
            else
            {
                Debug.LogError("[UIManager] Failed to host game.");
                StartDiscovery();
            }
        }

        private void OnJoinIpClicked()
        {
            string pName = GetValidatedPlayerName();
            string targetIp = (joinIpInput != null && !string.IsNullOrWhiteSpace(joinIpInput.text))
                ? joinIpInput.text.Trim()
                : "127.0.0.1";

            StopDiscovery();

            if (LanSession.Instance != null && LanSession.Instance.JoinByIp(targetIp, pName))
            {
                ShowLobbyScreen();
            }
            else
            {
                Debug.LogWarning($"[UIManager] Could not connect to IP {targetIp}.");
                StartDiscovery();
            }
        }

        private void RefreshIpLabels()
        {
            var ips = LanNetUtil.GetLocalIPv4Addresses();
            string ipText = ips.Count > 0 ? "Your IP: " + ips[0] : "Your IP: 127.0.0.1";

            if (playPanelIpLabel != null) playPanelIpLabel.text = ipText;
            if (lobbyIpLabel != null) lobbyIpLabel.text = ipText;
        }

        // --- LAN Discovery & Room List ---

        private void StartDiscovery()
        {
            if (discoveryClient == null) discoveryClient = FindAnyObjectByType<LanDiscoveryClient>();

            if (discoveryClient != null && !discoveryClient.IsListening)
            {
                discoveryClient.RoomsChanged += RefreshRoomListUI;
                discoveryClient.StartListening();
                RefreshRoomListUI();
            }
        }

        private void StopDiscovery()
        {
            if (discoveryClient != null)
            {
                discoveryClient.RoomsChanged -= RefreshRoomListUI;
                discoveryClient.StopListening();
            }
        }

        private void RefreshRoomListUI()
        {
            if (roomListContainer == null || discoveryClient == null) return;

            foreach (Transform child in roomListContainer)
            {
                Destroy(child.gameObject);
            }

            var rooms = discoveryClient.Rooms;
            bool hasRooms = rooms.Count > 0;

            if (noRoomsText != null) noRoomsText.gameObject.SetActive(!hasRooms);

            foreach (DiscoveredRoom room in rooms)
            {
                if (roomItemPrefab == null) break;

                RoomListItemUI item = Instantiate(roomItemPrefab, roomListContainer);
                item.Bind(room, r => {
                    string pName = GetValidatedPlayerName();
                    StopDiscovery();
                    LanSession.Instance.JoinRoom(r.HostAddress, r.Info.GamePort, pName);
                    ShowLobbyScreen();
                });
            }
        }

        // --- LobbyPanel Handlers ---

        private void SubscribeToLobby()
        {
            if (_subscribedToLobby) return;
            LanLobby lobby = LanLobby.Instance;
            if (lobby == null) return;

            lobby.LobbyChanged += RefreshPlayerListUI;
            _subscribedToLobby = true;
            RefreshPlayerListUI();
        }

        private void UnsubscribeFromLobby()
        {
            if (!_subscribedToLobby) return;
            LanLobby lobby = LanLobby.Instance;
            if (lobby != null) lobby.LobbyChanged -= RefreshPlayerListUI;
            _subscribedToLobby = false;
        }

        private void OnReadyClicked()
        {
            _isLocalReady = !_isLocalReady;

            if (LanLobby.Instance != null)
            {
                LanLobby.Instance.RequestReady(_isLocalReady);
            }

            UpdateReadyButtonVisuals();
        }

        private void UpdateReadyButtonVisuals()
        {
            if (readyButtonImage != null)
            {
                readyButtonImage.color = _isLocalReady ? readyColor : notReadyColor;
            }
            if (readyButtonText != null)
            {
                readyButtonText.text = _isLocalReady ? "READY" : "NOT READY";
            }
        }

        private void RefreshPlayerListUI()
        {
            if (LanLobby.Instance == null || playerListContainer == null) return;

            if (roomNameLabel != null && LanSession.Instance != null)
            {
                roomNameLabel.text = "Lobby: " + LanSession.Instance.RoomName;
            }

            foreach (Transform child in playerListContainer)
            {
                Destroy(child.gameObject);
            }

            LanLobby.Instance.GetPlayers(_rosterBuffer);
            var nm = NetworkManager.Singleton;

            foreach (LobbyPlayer player in _rosterBuffer)
            {
                if (playerItemPrefab == null) break;

                PlayerListItemUI item = Instantiate(playerItemPrefab, playerListContainer);

                bool isHost = nm != null && player.ClientId == NetworkManager.ServerClientId;
                bool isLocal = nm != null && player.ClientId == nm.LocalClientId;

                item.Bind(player, isHost, isLocal);
            }
        }

        private void UpdateStartMatchButton()
        {
            if (startMatchButton == null || LanLobby.Instance == null) return;

            var nm = NetworkManager.Singleton;
            if (nm != null && nm.IsServer)
            {
                startMatchButton.gameObject.SetActive(true);
                startMatchButton.interactable = LanLobby.Instance.CanStart(out _);
            }
            else
            {
                startMatchButton.gameObject.SetActive(false);
            }
        }

        private void OnStartMatchClicked()
        {
            string error = string.Empty;
            if (LanLobby.Instance != null && LanLobby.Instance.TryStartMatch(out error))
            {
                ShowGameScreen();
            }
            else
            {
                Debug.LogWarning($"[UIManager] Cannot start match: {error}");
            }
        }

        private void OnLeaveLobbyClicked()
        {
            if (LanSession.Instance != null)
            {
                LanSession.Instance.JoinFailed -= OnJoinFailed;
                LanSession.Instance.Leave();
            }

            _isLocalReady = false;
            UpdateReadyButtonVisuals();
            ShowPlayScreen();
        }

        private void OnJoinFailed(string reason)
        {
            Debug.LogError($"[UIManager] Join failed: {reason}");
            ShowPlayScreen();
        }

        private void OnHostLost()
        {
            Debug.LogWarning("[UIManager] Host disconnected.");
            ShowHomeScreen();
        }

        private string GetValidatedPlayerName()
        {
            if (playerNameInput != null && !string.IsNullOrWhiteSpace(playerNameInput.text))
            {
                return playerNameInput.text.Trim();
            }
            string fallback = "Player " + Random.Range(100, 999);
            if (playerNameInput != null) playerNameInput.text = fallback;
            return fallback;
        }

        // --- Screen Navigation ---

        public void ShowStartScreen() { SetPanel(startPanel); StopDiscovery(); UnsubscribeFromLobby(); }
        public void ShowHomeScreen() { SetPanel(homePanel); StopDiscovery(); UnsubscribeFromLobby(); }
        public void ShowPlayScreen()
        {
            SetPanel(playPanel);
            RefreshIpLabels();
            StartDiscovery();
            UnsubscribeFromLobby();

            if (LanSession.Instance != null)
            {
                LanSession.Instance.JoinFailed -= OnJoinFailed;
                LanSession.Instance.JoinFailed += OnJoinFailed;
            }
        }
        public void ShowLobbyScreen() { SetPanel(lobbyPanel); RefreshIpLabels(); StopDiscovery(); SubscribeToLobby(); }
        public void ShowGameScreen() { SetPanel(gamePanel); StopDiscovery(); UnsubscribeFromLobby(); }

        private void SetPanel(GameObject activePanel)
        {
            if (startPanel != null) startPanel.SetActive(startPanel == activePanel);
            if (homePanel != null) homePanel.SetActive(homePanel == activePanel);
            if (playPanel != null) playPanel.SetActive(playPanel == activePanel);
            if (lobbyPanel != null) lobbyPanel.SetActive(lobbyPanel == activePanel);
            if (gamePanel != null) gamePanel.SetActive(gamePanel == activePanel);
        }

        private void QuitGame() => Application.Quit();
    }
}