using EvasiveWhisker.UI;
using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [SerializeField] private GameObject gameOverModal;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private TMP_Text reasonText;
    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    private bool _isShown;
    private bool _busy;

    public bool IsShowing() => _isShown;

    private void Awake()
    {
        Instance = this;
        if (gameOverModal != null) gameOverModal.SetActive(false);
        if (returnToLobbyButton != null) returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    public void ShowWinner(string winnerName, string reason) => ShowInternal(winnerName, reason);

    public void ShowMatchEnded(string reason) => ShowInternal(null, reason);

    private void ShowInternal(string winnerName, string reason)
    {
        if (_isShown) return;
        _isShown = true;

        gameObject.SetActive(true);
        if (gameOverModal != null) gameOverModal.SetActive(true);

        var box = transform.Find("VictoryBox");
        if (box != null) box.gameObject.SetActive(true);

        var dim = transform.Find("Dim");
        if (dim != null) dim.gameObject.SetActive(true);

        transform.SetAsLastSibling();

        if (winnerText != null)
        {
            if (string.IsNullOrEmpty(winnerName))
            {
                winnerText.gameObject.SetActive(false);
            }
            else
            {
                winnerText.gameObject.SetActive(true);
                winnerText.text = $"<color=green>{winnerName}</color>";
            }
        }
        if (reasonText != null) reasonText.text = reason;
    }

    private void SetBusy(bool busy)
    {
        _busy = busy;
        if (returnToLobbyButton != null) returnToLobbyButton.interactable = !busy;
        if (mainMenuButton != null) mainMenuButton.interactable = !busy;
    }

    private async void OnReturnToLobbyClicked()
    {
        if (_busy) return;
        SetBusy(true);
        Debug.Log("[go] return to lobby");

        try
        {
            if (NetworkManager.Singleton != null &&
                (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
                await Task.Delay(150);
            }

            _isShown = false;
            if (gameObject != null) gameObject.SetActive(false);

            SceneManager.LoadScene(mainMenuSceneName);
            await Task.Delay(200);

            if (OnlineLobbyManager.Instance == null)
                throw new Exception("OnlineLobbyManager unavailable.");

            await OnlineLobbyManager.Instance.ReturnToLobbyAsync();
            UIManager.Instance?.ShowLobbyScreen();
        }
        catch (Exception e)
        {
            Debug.LogError($"[go] Return-to-Lobby failed: {e.Message}");

            _isShown = false;
            ShowMatchEnded(e.Message);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void OnMainMenuClicked()
    {
        if (_busy) return;
        SetBusy(true);
        Debug.Log("[go] exiting to main menu");

        _isShown = false;
        if (gameObject != null) gameObject.SetActive(false);

        try
        {
            if (OnlineLobbyManager.Instance != null)
                await OnlineLobbyManager.Instance.LeaveAllAsync();
        }
        catch (Exception e) { Debug.LogWarning($"[go] LeaveAll warning: {e.Message}"); }
        finally
        {
            if (NetworkManager.Singleton != null)
            {
                try { NetworkManager.Singleton.Shutdown(); } catch { }
            }
            SceneManager.LoadScene(mainMenuSceneName);
            UIManager.Instance?.ShowPlayScreen();
            SetBusy(false);
        }
    }
}