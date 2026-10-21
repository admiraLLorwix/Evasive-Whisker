
using UnityEngine;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using EvasiveWhisker.UI;

public class PlayPanelController : MonoBehaviour
{
    [Header("Player & Host Inputs")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private Button hostLobbyButton;

    [Header("Join by Code Inputs")]
    [SerializeField] private TMP_InputField lobbyCodeInput;
    [SerializeField] private Button joinByCodeButton;

    [Header("Lobby Browser")]
    [SerializeField] private RectTransform lobbyListContainer;
    [SerializeField] private GameObject lobbyEntryPrefab;
    [SerializeField] private Button refreshListButton;

    //[SerializeField] private Button backButton;


    private void Start()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            playerNameInput.text = OnlineLobbyManager.Instance.LocalPlayerName;
            OnlineLobbyManager.Instance.OnLobbyListUpdated += PopulateLobbyList;
            OnlineLobbyManager.Instance.RefreshLobbyList();
        }

        playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);
        hostLobbyButton.onClick.AddListener(OnHostLobbyClicked);
        joinByCodeButton.onClick.AddListener(OnJoinByCodeClicked);

        if (refreshListButton != null)
        {
            refreshListButton.onClick.AddListener(OnRefreshListClicked);
        }
    }

    private void OnDestroy()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.OnLobbyListUpdated -= PopulateLobbyList;
        }
    }

    private void OnPlayerNameChanged(string newName)
    {
        if (!string.IsNullOrEmpty(newName) && OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.LocalPlayerName = newName;
        }
    }

    private async void OnHostLobbyClicked()
    {
        string roomName = string.IsNullOrEmpty(lobbyNameInput.text)
            ? $"{OnlineLobbyManager.Instance.LocalPlayerName}'s Room"
            : lobbyNameInput.text;

        await OnlineLobbyManager.Instance.CreateLobbyAsync(roomName);

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowLobbyScreen();
        }
    }

    private async void OnJoinByCodeClicked()
    {
        string code = lobbyCodeInput.text.Trim();
        if (!string.IsNullOrEmpty(code))
        {
            await OnlineLobbyManager.Instance.JoinLobbyByCodeAsync(code);

            if (UIManager.Instance != null)
            {
                UIManager.Instance.ShowLobbyScreen();
            }
        }
    }

    private void OnRefreshListClicked()
    {
        OnlineLobbyManager.Instance.RefreshLobbyList();
    }

    private void PopulateLobbyList(List<Lobby> lobbies)
    {
        foreach (Transform child in lobbyListContainer)
        {
            Destroy(child.gameObject);
        }

        foreach (Lobby lobby in lobbies)
        {
            GameObject entry = Instantiate(lobbyEntryPrefab, lobbyListContainer);

            TMP_Text lobbyNameText = entry.transform.Find("LobbyNameText").GetComponent<TMP_Text>();
            TMP_Text playerCountText = entry.transform.Find("PlayerCountText").GetComponent<TMP_Text>();
            Button joinButton = entry.transform.Find("JoinButton").GetComponent<Button>();

            lobbyNameText.text = lobby.Name;
            playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

            string lobbyId = lobby.Id;
            joinButton.onClick.AddListener(async () =>
            {
                await OnlineLobbyManager.Instance.JoinLobbyByIdAsync(lobbyId);

                if (UIManager.Instance != null)
                {
                    UIManager.Instance.ShowLobbyScreen();
                }
            });
        }
    }
}