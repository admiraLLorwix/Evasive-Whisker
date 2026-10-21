using UnityEngine;
using UnityEngine.UI;

//decap
namespace EvasiveWhisker.UI
{
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

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

        [Header("Navigation Buttons")]
        [SerializeField] private Button playBackButton;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            if (startTouchButton != null) startTouchButton.onClick.AddListener(ShowHomeScreen);
            if (playButton != null) playButton.onClick.AddListener(ShowPlayScreen);
            if (exitButton != null) exitButton.onClick.AddListener(QuitGame);
            if (playBackButton != null) playBackButton.onClick.AddListener(ShowHomeScreen);

            ShowStartScreen();
        }

        public void ShowStartScreen() => SetPanel(startPanel);
        public void ShowHomeScreen() => SetPanel(homePanel);
        public void ShowPlayScreen() => SetPanel(playPanel);
        public void ShowLobbyScreen() => SetPanel(lobbyPanel);
        public void ShowGameScreen() => SetPanel(gamePanel);

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