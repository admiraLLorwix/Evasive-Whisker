using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Services.Lobbies.Models;

public class LobbySettingsUI : MonoBehaviour
{
    [SerializeField] private Button openSettingsButton;
    [SerializeField] private GameObject settingsModalRoot;
    [SerializeField] private Slider actionTimeSlider;
    [SerializeField] private TMP_Text actionTimeText;
    [SerializeField] private Slider resolutionTimeSlider;
    [SerializeField] private TMP_Text resolutionTimeText;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (openSettingsButton != null)
            openSettingsButton.onClick.AddListener(OpenSettings);

        if (closeButton != null)
            closeButton.onClick.AddListener(CloseSettings);

        if (settingsModalRoot != null)
            settingsModalRoot.SetActive(false);

        if (actionTimeSlider != null)
        {
            actionTimeSlider.minValue = 10f;
            actionTimeSlider.maxValue = 120f;
            actionTimeSlider.wholeNumbers = true;
            actionTimeSlider.onValueChanged.AddListener(OnActionTimeChanged);
        }

        if (resolutionTimeSlider != null)
        {
            resolutionTimeSlider.minValue = 10f;
            resolutionTimeSlider.maxValue = 120f;
            resolutionTimeSlider.wholeNumbers = true;
            resolutionTimeSlider.onValueChanged.AddListener(OnResolutionTimeChanged);
        }
    }

    private void Start()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.OnLobbyUpdated += SyncFromLobbyData;
            if (OnlineLobbyManager.Instance.CurrentLobby != null)
                SyncFromLobbyData(OnlineLobbyManager.Instance.CurrentLobby);
        }
    }

    private void OnDestroy()
    {
        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.OnLobbyUpdated -= SyncFromLobbyData;
        }
    }

    private void OpenSettings()
    {
        Debug.Log("[lsui] match settings button");

        if (OnlineLobbyManager.Instance == null || !OnlineLobbyManager.Instance.IsHost())
        {
            Debug.LogWarning("[lsui] host only");
            return;
        }

        if (settingsModalRoot != null)
            settingsModalRoot.SetActive(true);
    }

    private void CloseSettings()
    {
        Debug.Log("[lsui] saved match settings");
        if (settingsModalRoot != null)
            settingsModalRoot.SetActive(false);

        if (OnlineLobbyManager.Instance != null)
        {
            OnlineLobbyManager.Instance.UpdateLobbyTimerSettingsAsync((int)actionTimeSlider.value, (int)resolutionTimeSlider.value);
        }
    }

    private void OnActionTimeChanged(float val)
    {
        if (actionTimeText != null) actionTimeText.text = $"{val}s";
    }

    private void OnResolutionTimeChanged(float val)
    {
        if (resolutionTimeText != null) resolutionTimeText.text = $"{val}s";
    }

    private void SyncFromLobbyData(Lobby lobby)
    {
        if (lobby == null || lobby.Data == null) return;

        if (lobby.Data.TryGetValue("ActionTime", out var actionData))
        {
            if (int.TryParse(actionData.Value, out int aTime) && actionTimeSlider != null)
            {
                actionTimeSlider.value = aTime;
                if (actionTimeText != null) actionTimeText.text = $"{aTime}s";
            }
        }

        if (lobby.Data.TryGetValue("ResolutionTime", out var resData))
        {
            if (int.TryParse(resData.Value, out int rTime) && resolutionTimeSlider != null)
            {
                resolutionTimeSlider.value = rTime;
                if (resolutionTimeText != null) resolutionTimeText.text = $"{rTime}s";
            }
        }
    }
}