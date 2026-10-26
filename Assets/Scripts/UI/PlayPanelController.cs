
using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Services.Lobbies.Models;
using UnityEngine.UI;
using EvasiveWhisker.UI;

public class PlayPanelController : MonoBehaviour
{
    [Header("player & host inpts")]
    [SerializeField] private TMP_InputField playerNameInput;
    [SerializeField] private TMP_InputField lobbyNameInput;
    [SerializeField] private Button hostLobbyButton;

    [Header("code join inpts")]
    [SerializeField] private TMP_InputField lobbyCodeInput;
    [SerializeField] private Button joinByCodeButton;

    [Header("lobby list")]
    [SerializeField] private GameObject emptyStateContainer;
    [SerializeField] private RectTransform lobbyListContainer;
    [SerializeField] private GameObject lobbyEntryPrefab;
    [SerializeField] private Button refreshListButton;


    private Coroutine autoRefreshCoroutine;
    private const float AUTO_REFRESH_INTERVAL = 3.5f; //adjust not too low

    //[SerializeField] private Button backButton;

    private void Awake()
    {
        //if (OnlineLobbyManager.Instance != null)
        //{
        //    playerNameInput.text = OnlineLobbyManager.Instance.LocalPlayerName;
        //    OnlineLobbyManager.Instance.OnLobbyListUpdated += PopulateLobbyList;
        //    OnlineLobbyManager.Instance.RefreshLobbyList();
        //}

        //playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);
        hostLobbyButton.onClick.AddListener(OnHostLobbyClicked);
        joinByCodeButton.onClick.AddListener(OnJoinByCodeClicked);

        if (refreshListButton != null)
        {
            refreshListButton.onClick.AddListener(OnRefreshListClicked);
        }

        if (playerNameInput != null)
        {
            playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);
        }
    }

    private void OnEnable()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            if (playerNameInput != null) { playerNameInput.text = OnlineLobbyManager.Instance.LocalPlayerName; }
        }
        OnlineLobbyManager.Instance.OnLobbyListUpdated += PopulateLobbyList;
        autoRefreshCoroutine = StartCoroutine(AutoRefreshLobbiesLoop());
    }

    private void OnDisable()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.OnLobbyListUpdated -= PopulateLobbyList;
        }

        if(autoRefreshCoroutine != null)
        {
            StopCoroutine(autoRefreshCoroutine);
            autoRefreshCoroutine = null;
        }
    }

    private IEnumerator AutoRefreshLobbiesLoop()
    {
        while (true)
        {
            if(OnlineLobbyManager.Instance != null)
            {
                OnlineLobbyManager.Instance.RefreshLobbyList();
            }
            yield return new WaitForSeconds(AUTO_REFRESH_INTERVAL);
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
        hostLobbyButton.interactable = false;

        string roomName = string.IsNullOrEmpty(lobbyNameInput.text)
            ? $"{OnlineLobbyManager.Instance.LocalPlayerName}'s Room"
            : lobbyNameInput.text;

        bool success = 
        await OnlineLobbyManager.Instance.CreateLobbyAsync(roomName);

        if (success && UIManager.Instance != null)
        {
            UIManager.Instance.ShowLobbyScreen();
        }
        hostLobbyButton.interactable = true;
    }

    private async void OnJoinByCodeClicked()
    {
        string code = lobbyCodeInput.text.Trim();
        if (string.IsNullOrEmpty(code)) return;

        joinByCodeButton.interactable = false;

        bool success = 
        await OnlineLobbyManager.Instance.JoinLobbyByCodeAsync(code);


        if (success && UIManager.Instance != null)
        {
            UIManager.Instance.ShowLobbyScreen();
        }
        joinByCodeButton.interactable = true;
    }

    private void OnRefreshListClicked()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.RefreshLobbyList();
        }
    }

    private void PopulateLobbyList(List<Lobby> lobbies)
    {
        foreach (Transform child in lobbyListContainer)
        {
            Destroy(child.gameObject);
        }

        bool hasNoLobbies = (lobbies == null || lobbies.Count == 0);

        if (emptyStateContainer != null)
        {
            emptyStateContainer.SetActive(hasNoLobbies);
        }
        if (hasNoLobbies) return;

        foreach (Lobby lobby in lobbies)
        {
            GameObject entry = Instantiate(lobbyEntryPrefab, lobbyListContainer);

            TMP_Text lobbyNameText = entry.transform.Find("LobbyNameText")?.GetComponent<TMP_Text>();
            TMP_Text playerCountText = entry.transform.Find("PlayerCountText")?.GetComponent<TMP_Text>();
            Button joinButton = entry.transform.Find("JoinButton")?.GetComponent<Button>();

            if (lobbyNameText != null) lobbyNameText.text = lobby.Name;
            if (playerCountText != null) playerCountText.text = $"{lobby.Players.Count}/{lobby.MaxPlayers}";

            if (joinButton != null)
            {
                string lobbyId = lobby.Id;
                joinButton.onClick.RemoveAllListeners();
                joinButton.onClick.AddListener(async () =>
                {
                    joinButton.interactable = false;
                    bool joined = await OnlineLobbyManager.Instance.JoinLobbyByIdAsync(lobbyId);

                    if (joined && UIManager.Instance != null)
                    {
                        UIManager.Instance.ShowLobbyScreen();
                    }
                    else
                    {
                        joinButton.interactable = true;
                    }
                });
            }
        }
    }
}