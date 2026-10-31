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

    [SerializeField] private Button returnToLobbyButton; [SerializeField] private string mainMenuSceneName = "MainMenu";
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
        gameObject.SetActive(true);
        if (gameOverModal != null) gameOverModal.SetActive(true);

        Transform box = transform.Find("VictoryBox");
        if (box != null) box.gameObject.SetActive(true);

        Transform dim = transform.Find("Dim");
        if (dim != null) dim.gameObject.SetActive(true);

        transform.SetAsLastSibling();

        if (winnerText != null) winnerText.text = $"<color=yellow>{winnerName}</color>";
        if (reasonText != null) reasonText.text = reason;
    }

    private void OnReturnToLobbyClicked()
    {
        Debug.Log("[go] return to lobby");

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer)
            {
                NetworkManager.Singleton.SceneManager.LoadScene(mainMenuSceneName, LoadSceneMode.Single);
            }
            else
            {
                NetworkManager.Singleton.Shutdown();
                SceneManager.LoadScene(mainMenuSceneName);
            }
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
    }


    private void OnMainMenuClicked()
    {
        Debug.Log("[go] exiting");

        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.LeaveLobby();
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }

        SceneManager.LoadScene(mainMenuSceneName);
    }
}