
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using EvasiveWhisker.UI;

public class LobbyPanelController : MonoBehaviour
{
    [Header("Lobby Header Info")]
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private TMP_Text lobbyCodeText;

    [Header("Player List Container")]
    [SerializeField] private RectTransform playerListContainer;
    [SerializeField] private GameObject playerEntryPrefab;

    [Header("Action Buttons")]
    [SerializeField] private Button readyButton;
    [SerializeField] private Button startMatchButton;
    [SerializeField] private Button leaveButton;

    private bool isLocalPlayerReady = false;

    private void Start()
    {
        readyButton.onClick.AddListener(OnReadyButtonClicked);
        startMatchButton.onClick.AddListener(OnStartMatchClicked);
        leaveButton.onClick.AddListener(OnLeaveButtonClicked);

        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.OnLobbyJoined += HandleLobbyJoined;
            OnlineLobbyManager.Instance.OnLobbyLeft += HandleLobbyLeft;
        }
    }

    private void OnDestroy()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.OnLobbyJoined -= HandleLobbyJoined;
            OnlineLobbyManager.Instance.OnLobbyLeft -= HandleLobbyLeft;
        }
    }

    private void Update()
    {
        if (OnlineLobbyManager.Instance != null && OnlineLobbyManager.Instance.CurrentLobby != null)
        {
            UpdateLobbyUI(OnlineLobbyManager.Instance.CurrentLobby);
        }
    }

    private void HandleLobbyJoined(Lobby lobby)
    {
        gameObject.SetActive(true);
        UpdateLobbyUI(lobby);
    }

    private void HandleLobbyLeft()
    {
        isLocalPlayerReady = false;
        gameObject.SetActive(false);
    }

    private void UpdateLobbyUI(Lobby lobby)
    {
        if (lobbyNameText != null) lobbyNameText.text = lobby.Name;
        if (lobbyCodeText != null) lobbyCodeText.text = $"LOBBY CODE: {lobby.LobbyCode}";

        foreach (Transform child in playerListContainer)
        {
            Destroy(child.gameObject);
        }

        bool allPlayersReady = true;
        int connectedCount = lobby.Players.Count;

        foreach (Player player in lobby.Players)
        {
            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);

            TMP_Text playerNameText = entry.transform.Find("PlayerNameText").GetComponent<TMP_Text>();
            TMP_Text statusText = entry.transform.Find("StatusText").GetComponent<TMP_Text>();
            Button kickButton = entry.transform.Find("KickButton")?.GetComponent<Button>();

            string pName = player.Data.ContainsKey("PlayerName") ? player.Data["PlayerName"].Value : "Player";
            bool pReady = player.Data.ContainsKey("IsReady") && player.Data["IsReady"].Value == "True";

            if (playerNameText != null) playerNameText.text = pName;
            if (statusText != null) statusText.text = pReady ? "<color=green>READY</color>" : "<color=red>NOT READY</color>";

            if (!pReady) allPlayersReady = false;

            if (kickButton != null)
            {
                bool isHost = OnlineLobbyManager.Instance.IsHost();
                bool isTargetHost = player.Id == lobby.HostId;

                kickButton.gameObject.SetActive(isHost && !isTargetHost);

                string targetPlayerId = player.Id;
                kickButton.onClick.RemoveAllListeners();
                kickButton.onClick.AddListener(() =>
                {
                    OnlineLobbyManager.Instance.KickPlayer(targetPlayerId);
                });
            }
        }

        bool isHostPlayer = OnlineLobbyManager.Instance.IsHost();

        if (startMatchButton != null)
        {
            startMatchButton.gameObject.SetActive(isHostPlayer);
            startMatchButton.interactable = isHostPlayer && connectedCount >= 3 && allPlayersReady;
        }
    }

    private async void OnReadyButtonClicked()
    {
        isLocalPlayerReady = !isLocalPlayerReady;

        if (OnlineLobbyManager.Instance != null && OnlineLobbyManager.Instance.CurrentLobby != null)
        {
            await OnlineLobbyManager.Instance.UpdatePlayerReadyStateAsync(isLocalPlayerReady);
        }
    }

    private void OnStartMatchClicked()
    {
        if (OnlineLobbyManager.Instance != null && OnlineLobbyManager.Instance.IsHost())
        {
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
}