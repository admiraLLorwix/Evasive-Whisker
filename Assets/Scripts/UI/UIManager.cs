using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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

        [Header("transition")]
        [SerializeField] private CanvasGroup transitionOverlay;
        [SerializeField] private float fadeDuration = 0.25f;

        [Header("start screen btns")]
        [SerializeField] private Button startTouchButton;

        [Header("home screen btns")]
        [SerializeField] private Button playButton;
        [SerializeField] private Button exitButton;

        [Header("navi btn")]
        [SerializeField] private Button playBackButton;

        private GameObject currentActivePanel;
        private Coroutine activeTransitionCoroutine;

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

            if (exitButton != null) exitButton.onClick.AddListener(ShowStartScreen);

            if (playBackButton != null) playBackButton.onClick.AddListener(ShowHomeScreen);

            currentActivePanel = startPanel;
            SetInstantPanelState(startPanel);

            if (transitionOverlay != null)
            {
                transitionOverlay.gameObject.SetActive(false);
                transitionOverlay.alpha = 0f;
            }
        }

        public void ShowStartScreen() => TransitionToPanel(startPanel);
        public void ShowHomeScreen() => TransitionToPanel(homePanel);
        public void ShowPlayScreen() => TransitionToPanel(playPanel);
        public void ShowLobbyScreen() => TransitionToPanel(lobbyPanel);
        public void ShowGameScreen() => TransitionToPanel(gamePanel);

        public void TransitionToPanel(GameObject targetPanel)
        {
            if (targetPanel == null || targetPanel == currentActivePanel) return;

            if (activeTransitionCoroutine != null)
            {
                StopCoroutine(activeTransitionCoroutine);
            }

            activeTransitionCoroutine = StartCoroutine(AnimateSimpleFade(currentActivePanel, targetPanel));
        }

        private IEnumerator AnimateSimpleFade(GameObject current, GameObject next)
        {
            if (transitionOverlay != null)
            {
                transitionOverlay.gameObject.SetActive(true);
                transitionOverlay.transform.SetAsLastSibling();

                float elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    transitionOverlay.alpha = Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                transitionOverlay.alpha = 1f;

                if (current != null) current.SetActive(false);
                if (next != null) next.SetActive(true);
                currentActivePanel = next;

                if (next == startPanel)
                {
                    StartPanelAnimation startAnim = startPanel.GetComponent<StartPanelAnimation>();
                    if (startAnim != null)
                    {
                        startAnim.StartAnimations();
                    }
                }

                elapsed = 0f;
                while (elapsed < fadeDuration)
                {
                    elapsed += Time.deltaTime;
                    transitionOverlay.alpha = 1f - Mathf.Clamp01(elapsed / fadeDuration);
                    yield return null;
                }
                transitionOverlay.alpha = 0f;
                transitionOverlay.gameObject.SetActive(false);
            }
            else
            {
                if (current != null) current.SetActive(false);
                if (next != null) next.SetActive(true);
                currentActivePanel = next;

                if (next == startPanel)
                {
                    StartPanelAnimation startAnim = startPanel.GetComponent<StartPanelAnimation>();
                    if (startAnim != null)
                    {
                        startAnim.StartAnimations();
                    }
                }
            }

            activeTransitionCoroutine = null;
        }

        private void SetInstantPanelState(GameObject activePanel)
        {
            GameObject[] allPanels = { startPanel, homePanel, playPanel, lobbyPanel, gamePanel };
            foreach (var panel in allPanels)
            {
                if (panel == null) continue;
                panel.SetActive(panel == activePanel);
            }
        }
    }
}