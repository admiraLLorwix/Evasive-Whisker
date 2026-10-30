using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Netcode;
using UnityEngine.SceneManagement;

public class GameOverUI : MonoBehaviour
{
    public static GameOverUI Instance { get; private set; }

    [SerializeField] private GameObject gameOverModal;
    [SerializeField] private TMP_Text winnerText;
    [SerializeField] private TMP_Text reasonText;

    [SerializeField] private Button returnToLobbyButton;
    [SerializeField] private Button mainMenuButton;

    private void Awake()
    {
        Instance = this;
        if (gameOverModal != null) gameOverModal.SetActive(false);

        if (returnToLobbyButton != null) returnToLobbyButton.onClick.AddListener(OnReturnToLobbyClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    public void ShowWinner(string winnerName, string reason)
    {
        if (gameOverModal != null)
        {
            gameOverModal.SetActive(true);
            gameOverModal.transform.SetAsLastSibling();
        }

        if (winnerText != null) winnerText.text = $"<color=yellow>{winnerName}</color>";
        if (reasonText != null) reasonText.text = reason;
    }

    private void OnReturnToLobbyClicked()
    {
        if (NetworkManager.Singleton.IsServer)
        {
            NetworkManager.Singleton.SceneManager.LoadScene("MainMenu", LoadSceneMode.Single);
        }
    }

    private void OnMainMenuClicked()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.LeaveLobby();
        }
        else if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
            SceneManager.LoadScene("MainMenu");
        }
    }
}