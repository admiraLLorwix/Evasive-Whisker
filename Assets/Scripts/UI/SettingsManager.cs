using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance { get; private set; }

    [Header("general")]
    [SerializeField] private GameObject settingsModalRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private Button backgroundDimButton;

    [Header("audio (sliders)")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider bgmVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("audio (objects)")]
    [SerializeField] private AudioSource bgmAudioSource;
    [SerializeField] private Transform sfxParentGroup;

    [Header("main menu")]
    [SerializeField] private GameObject menuOnlySection;
    [SerializeField] private TMP_InputField playerNameInput;

    [Tooltip("ingame")]
    [SerializeField] private GameObject inGameOnlySection;
    [SerializeField] private Button leaveMatchButton;
    [SerializeField] private Button rulebookButton;
    [SerializeField] private GameObject rulebookModalObject;

    private bool isInGameScene = false;
    private CanvasGroup settingsCanvasGroup;
    private CanvasGroup rulebookCanvasGroup;

    private void Awake()
    {
        Instance = this;

        string activeScene = SceneManager.GetActiveScene().name;
        isInGameScene = activeScene.Contains("Game");

        if (menuOnlySection != null) menuOnlySection.SetActive(!isInGameScene);
        if (inGameOnlySection != null) inGameOnlySection.SetActive(isInGameScene);

        if (closeButton != null) closeButton.onClick.AddListener(CloseSettings);
        if (backgroundDimButton != null) backgroundDimButton.onClick.AddListener(CloseSettings);
        if (leaveMatchButton != null) leaveMatchButton.onClick.AddListener(OnLeaveMatchClicked);
        if (rulebookButton != null) rulebookButton.onClick.AddListener(OnRulebookClicked);

        if (masterVolumeSlider != null)
        {
            masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);
        }

        if (bgmVolumeSlider != null)
        {
            bgmVolumeSlider.value = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
            bgmVolumeSlider.onValueChanged.AddListener(OnBGMVolumeChanged);
        }

        if (sfxVolumeSlider != null)
        {
            sfxVolumeSlider.value = PlayerPrefs.GetFloat("SFXVolume", 1f);
            sfxVolumeSlider.onValueChanged.AddListener(OnSFXVolumeChanged);
        }

        if (playerNameInput != null)
        {
            playerNameInput.text = PlayerPrefs.GetString("PlayerName", "Detective");
            playerNameInput.onEndEdit.AddListener(OnPlayerNameChanged);
        }

        ApplyAudioVolumes();

        if (settingsModalRoot != null)
        {
            settingsCanvasGroup = settingsModalRoot.GetComponent<CanvasGroup>();
            if (settingsCanvasGroup == null)
                settingsCanvasGroup = settingsModalRoot.AddComponent<CanvasGroup>();

            settingsCanvasGroup.alpha = 0f;
            settingsCanvasGroup.blocksRaycasts = false;
            settingsCanvasGroup.interactable = false;
        }
        if (rulebookModalObject != null)
        {
            rulebookCanvasGroup = rulebookModalObject.GetComponent<CanvasGroup>();
            if (rulebookCanvasGroup == null)
                rulebookCanvasGroup = rulebookModalObject.AddComponent<CanvasGroup>();

            rulebookCanvasGroup.alpha = 0f;
            rulebookCanvasGroup.blocksRaycasts = false;
            rulebookCanvasGroup.interactable = false;
        }
    }

    public void OpenSettings()
    {
        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = 1f;
            settingsCanvasGroup.blocksRaycasts = true;
            settingsCanvasGroup.interactable = true;
            settingsModalRoot.transform.SetAsLastSibling();
        }
    }

    public void CloseSettings()
    {
        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = 0f;
            settingsCanvasGroup.blocksRaycasts = false;
            settingsCanvasGroup.interactable = false;
        }
        PlayerPrefs.Save();
    }

    private void OnRulebookClicked()
    {
        if (rulebookCanvasGroup != null)
        {
            rulebookCanvasGroup.alpha = 1f;
            rulebookCanvasGroup.blocksRaycasts = true;
            rulebookCanvasGroup.interactable = true;
            rulebookModalObject.transform.SetAsLastSibling();
        }
    }

    private void OnMasterVolumeChanged(float val)
    {
        PlayerPrefs.SetFloat("MasterVolume", val);
        AudioListener.volume = val;
    }

    private void OnBGMVolumeChanged(float val)
    {
        PlayerPrefs.SetFloat("BGMVolume", val);
        if (bgmAudioSource != null) bgmAudioSource.volume = val;
    }

    private void OnSFXVolumeChanged(float val)
    {
        PlayerPrefs.SetFloat("SFXVolume", val);
        UpdateAllSFXVolumes(val);
    }

    private void ApplyAudioVolumes()
    {
        AudioListener.volume = PlayerPrefs.GetFloat("MasterVolume", 1f);
        if (bgmAudioSource != null) bgmAudioSource.volume = PlayerPrefs.GetFloat("BGMVolume", 0.75f);
        UpdateAllSFXVolumes(PlayerPrefs.GetFloat("SFXVolume", 1f));
    }
    private void UpdateAllSFXVolumes(float volume)
    {
        if (sfxParentGroup == null) return;
        AudioSource[] sources = sfxParentGroup.GetComponentsInChildren<AudioSource>(true);
        foreach (var src in sources)
        {
            src.volume = volume;
        }
    }

    private void OnPlayerNameChanged(string newName)
    {
        if (!string.IsNullOrWhiteSpace(newName))
        {
            PlayerPrefs.SetString("PlayerName", newName);
            if (OnlineLobbyManager.Instance != null)
            {
                OnlineLobbyManager.Instance.LocalPlayerName = newName;
            }
        }
    }

    private async void OnLeaveMatchClicked()
    {
        CloseSettings();

        if (GameManager.Instance != null && GameManager.Instance.IsSpawned)
        {
            if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            {
                await GameManager.Instance.HostLeaveMatchAsync();
            }
            else
            {
                GameManager.Instance.LeaveMatchClient();
            }
        }

        if (OnlineLobbyManager.Instance != null)
        {
            await OnlineLobbyManager.Instance.LeaveAllAsync();
        }
        else if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.Shutdown();
        }
        SceneManager.LoadScene("MainMenu");
    }
}