using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using EvasiveWhisker.UI;

public class LobbyPanelController : MonoBehaviour
{
    [Header("lobby header info")]
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text lobbyCodeText;

    [Header("player list container")]
    [SerializeField] private RectTransform playerListContainer;
    [SerializeField] private GameObject playerEntryPrefab;
    [SerializeField] private Sprite playerReadySprite;
    [SerializeField] private Sprite playerNotReadySprite;

    [Header("action btns")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Image readyButtonImage;
    [SerializeField] private Sprite readySprite;
    [SerializeField] private Sprite notReadySprite;
    //[SerializeField] private TMP_Text readyButtonText;
    [SerializeField] private Button startMatchButton;
    [SerializeField] private Button leaveButton;

    [SerializeField] private Button lockLobbyButton;
    [SerializeField] private Image lockLobbyButtonImage;
    [SerializeField] private Sprite lockedSprite;
    [SerializeField] private Sprite unlockedSprite;

    //testing only
    [Header("match settings")]
    [SerializeField] private int minPlayersToStart = 3;
    //bots
    [SerializeField] private Button addBotButton;
    private int currentBotCount = 0;
    private bool isLocalPlayerReady = false;

    private void Start()
    {
        readyButton.onClick.AddListener(OnReadyButtonClicked);
        startMatchButton.onClick.AddListener(OnStartMatchClicked);
        leaveButton.onClick.AddListener(OnLeaveButtonClicked);
        if (addBotButton != null) addBotButton.onClick.AddListener(OnAddBotClicked);
        if (lockLobbyButton != null) lockLobbyButton.onClick.AddListener(OnLockLobbyClicked);

        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.OnLobbyJoined += HandleLobbyJoined;
            OnlineLobbyManager.Instance.OnLobbyUpdated += HandleLobbyUpdated;
            OnlineLobbyManager.Instance.OnLobbyLeft += HandleLobbyLeft;

            if (OnlineLobbyManager.Instance.CurrentLobby != null)
            {
                UpdateLobbyUI(OnlineLobbyManager.Instance.CurrentLobby);
            }
        }
    }
    private void OnDestroy()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.OnLobbyJoined -= HandleLobbyJoined;
            OnlineLobbyManager.Instance.OnLobbyUpdated -= HandleLobbyUpdated;
            OnlineLobbyManager.Instance.OnLobbyLeft -= HandleLobbyLeft;
        }
    }

    //decap ts
    //private void Update()
    //{
    //    if (OnlineLobbyManager.Instance != null && OnlineLobbyManager.Instance.CurrentLobby != null)
    //    {
    //        UpdateLobbyUI(OnlineLobbyManager.Instance.CurrentLobby);
    //    }
    //}

    private void HandleLobbyJoined(Lobby lobby)
    {
        gameObject.SetActive(true);
        isLocalPlayerReady = false;
        UpdateReadyButtonVisual();
        UpdateLobbyUI(lobby);
    }

    private void HandleLobbyUpdated(Lobby lobby)
    {
        UpdateLobbyUI(lobby);
    }

    private void HandleLobbyLeft()
    {
        isLocalPlayerReady = false;
        gameObject.SetActive(false);
    }

    private void UpdateLobbyUI(Lobby lobby)
    {
        if (lobby == null) return;
        if (lobbyNameText != null) lobbyNameText.text = lobby.Name;
        if (lobbyCodeText != null) lobbyCodeText.text = $"{lobby.LobbyCode}";

        foreach (Transform child in playerListContainer)
        {
            Destroy(child.gameObject);
        }

        bool allPlayersReady = true;
        int connectedCount = lobby.Players.Count;
        bool isHostPlayer = OnlineLobbyManager.Instance.IsHost();
        int botCount = 0;
        if (lobby.Data != null && lobby.Data.ContainsKey("BotCount"))
            int.TryParse(lobby.Data["BotCount"].Value, out botCount);
        foreach (Player player in lobby.Players)
        {
            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);

            TMP_Text playerNameText = entry.transform.Find("PlayerNameText")?.GetComponent<TMP_Text>();
            Image statusImage = entry.transform.Find("StatusImage")?.GetComponent<Image>();
            //TMP_Text statusText = entry.transform.Find("StatusText")?.GetComponent<TMP_Text>();
            Button kickButton = entry.transform.Find("KickButton")?.GetComponent<Button>();

            string pName = (player.Data != null && player.Data.ContainsKey("PlayerName")) ? player.Data["PlayerName"].Value : "Player";
            bool pReady = (player.Data != null && player.Data.ContainsKey("IsReady")) && player.Data["IsReady"].Value == "True";

            if (playerNameText != null) playerNameText.text = pName;
            //if (statusText != null) statusText.text = pReady ? "<color=green>READY</color>" : "<color=red>NOT READY</color>";

            if (statusImage != null)
            {
                statusImage.sprite = pReady ? playerReadySprite : playerNotReadySprite;
            }

            if (!pReady) allPlayersReady = false;

            if (kickButton != null)
            {
                //bool isHost = OnlineLobbyManager.Instance.IsHost();
                bool isTargetHost = player.Id == lobby.HostId;

                kickButton.gameObject.SetActive(isHostPlayer && !isTargetHost);

                string targetPlayerId = player.Id;
                kickButton.onClick.RemoveAllListeners();
                kickButton.onClick.AddListener(async() =>
                {
                    kickButton.interactable = false;
                    await OnlineLobbyManager.Instance.KickPlayer(targetPlayerId);
                });
            }
        }

        for (int i = 0; i < botCount; i++)
        {
            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);
            TMP_Text playerNameText = entry.transform.Find("PlayerNameText")?.GetComponent<TMP_Text>();
            Image statusImage = entry.transform.Find("StatusImage")?.GetComponent<Image>();
            Button kickButton = entry.transform.Find("KickButton")?.GetComponent<Button>();

            if (playerNameText != null) playerNameText.text = $"Bot {i + 1}";
            if (statusImage != null) statusImage.sprite = playerReadySprite;

            if (kickButton != null)
            {
                kickButton.gameObject.SetActive(isHostPlayer);
                kickButton.onClick.RemoveAllListeners();
                kickButton.onClick.AddListener(() => OnRemoveBotClicked(botCount));
            }
        }
        connectedCount += botCount;
        //bool isHostPlayer = OnlineLobbyManager.Instance.IsHost();

        if (startMatchButton != null)
        {
            startMatchButton.gameObject.SetActive(isHostPlayer);
            startMatchButton.interactable = isHostPlayer && (connectedCount >= minPlayersToStart) && allPlayersReady;
        }

        if (addBotButton != null)
        {
            addBotButton.gameObject.SetActive(isHostPlayer);
            addBotButton.interactable = connectedCount < lobby.MaxPlayers;
        }

        if (lockLobbyButton != null)
        {
            lockLobbyButton.gameObject.SetActive(isHostPlayer);

            bool isLocked = lobby.Data != null && lobby.Data.ContainsKey("IsLocked") && lobby.Data["IsLocked"].Value == "True";

            if (lockLobbyButtonImage != null)
            {
                lockLobbyButtonImage.sprite = isLocked ? lockedSprite : unlockedSprite;
            }
        }
    }

    private async void OnReadyButtonClicked()
    {
        isLocalPlayerReady = !isLocalPlayerReady;
        UpdateReadyButtonVisual();

        if (OnlineLobbyManager.Instance != null && OnlineLobbyManager.Instance.CurrentLobby != null)
        {
            readyButton.interactable = false;
            await OnlineLobbyManager.Instance.UpdatePlayerReadyStateAsync(isLocalPlayerReady);
            readyButton.interactable = true;
        }
    }

    private void UpdateReadyButtonVisual()
    {
        if (readyButtonImage == null && readyButton != null)
        {
            readyButtonImage = readyButton.GetComponent<Image>();
        }
        if (readyButtonImage != null)
        {
            readyButtonImage.sprite = isLocalPlayerReady ? readySprite : notReadySprite;
        }
        //if (readyButtonText != null)
        //{
        //    readyButtonText.text = isLocalPlayerReady ? "CANCEL READY" : "READY";
        //}
    }

    private void OnStartMatchClicked()
    {
        if (OnlineLobbyManager.Instance != null && OnlineLobbyManager.Instance.IsHost())
        {
            var lobby = OnlineLobbyManager.Instance.CurrentLobby;
            MatchContext.BotCount = 0;
            if (lobby.Data != null && lobby.Data.ContainsKey("BotCount"))
                int.TryParse(lobby.Data["BotCount"].Value, out MatchContext.BotCount);
            LoadingScreenManager.Instance?.Show("Loading Match");
            NetworkManager.Singleton.SceneManager.LoadScene("Game", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private void OnLeaveButtonClicked()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.LeaveLobby();
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowPlayScreen();
        }
    }

    private async void OnAddBotClicked()
    {
        if (OnlineLobbyManager.Instance == null || !OnlineLobbyManager.Instance.IsHost()) return;

        var lobby = OnlineLobbyManager.Instance.CurrentLobby;
        if (lobby == null) return;

        int currentBots = 0;
        if (lobby.Data != null && lobby.Data.ContainsKey("BotCount"))
            int.TryParse(lobby.Data["BotCount"].Value, out currentBots);

        if (currentBots + lobby.Players.Count >= lobby.MaxPlayers) return;

        await OnlineLobbyManager.Instance.UpdateLobbyBotCountAsync(currentBots + 1);
    }

    private async void OnRemoveBotClicked(int currentBots)
    {
        if (OnlineLobbyManager.Instance == null || !OnlineLobbyManager.Instance.IsHost()) return;
        if (currentBots > 0)
        {
            await OnlineLobbyManager.Instance.UpdateLobbyBotCountAsync(currentBots - 1);
        }
    }

    private async void OnLockLobbyClicked()
    {
        if (OnlineLobbyManager.Instance != null && OnlineLobbyManager.Instance.CurrentLobby != null && OnlineLobbyManager.Instance.IsHost())
        {
            lockLobbyButton.interactable = false;

            var lobbyData = OnlineLobbyManager.Instance.CurrentLobby.Data;
            bool currentLockState = lobbyData != null &&
                                    lobbyData.ContainsKey("IsLocked") &&
                                    lobbyData["IsLocked"].Value == "True";

            await OnlineLobbyManager.Instance.ToggleLobbyLockAsync(!currentLockState);
            lockLobbyButton.interactable = true;
        }
    }
}