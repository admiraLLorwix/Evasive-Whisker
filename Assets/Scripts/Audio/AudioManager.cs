using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central audio manager. Add to ONE GameObject in your first scene.
/// It persists between scenes and is reachable via AudioManager.Instance.
///
/// Usage from any script:
///     AudioManager.Instance.PlaySFX("CardFlip");
///     AudioManager.Instance.PlayMusic("MenuTheme", 1.5f);
///     AudioManager.Instance.SetMusicVolume(0.5f);
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        public bool loop;

        [Header("Variation")]
        public bool randomizePitch;
        [Range(0f, 0.5f)] public float pitchVariance = 0.1f;
    }

    [Header("Sound Library")]
    [SerializeField] private List<Sound> musicTracks = new List<Sound>();
    [SerializeField] private List<Sound> sfxSounds = new List<Sound>();

    [Header("Settings")]
    [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField] private int initialSfxPoolSize = 8;
    [SerializeField] private int maxSfxPoolSize = 24;
    [SerializeField] private bool persistBetweenScenes = true;

    // PlayerPrefs keys
    private const string KeyMaster = "Audio_Master";
    private const string KeyMusic = "Audio_Music";
    private const string KeySfx = "Audio_SFX";

    private readonly Dictionary<string, Sound> musicLookup = new Dictionary<string, Sound>();
    private readonly Dictionary<string, Sound> sfxLookup = new Dictionary<string, Sound>();
    private readonly List<AudioSource> sfxPool = new List<AudioSource>();

    private AudioSource[] musicSources = new AudioSource[2];
    private int activeMusic;
    private Sound currentMusic;
    private Coroutine musicFade;
    private int poolCursor;

    public float MasterVolume => masterVolume;
    public float MusicVolume => musicVolume;
    public float SfxVolume => sfxVolume;
    public string CurrentMusicName => currentMusic != null ? currentMusic.name : null;

    // ------------------------------------------------------------------ setup

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        if (persistBetweenScenes)
        {
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
        }

        BuildLookups();
        CreateSources();
        LoadVolumes();
        ApplyVolumes();
    }

    private void BuildLookups()
    {
        foreach (var s in musicTracks)
        {
            if (string.IsNullOrEmpty(s.name)) continue;
            if (!musicLookup.ContainsKey(s.name)) musicLookup.Add(s.name, s);
            else Debug.LogWarning($"[AudioManager] Duplicate music name '{s.name}'.");
        }

        foreach (var s in sfxSounds)
        {
            if (string.IsNullOrEmpty(s.name)) continue;
            if (!sfxLookup.ContainsKey(s.name)) sfxLookup.Add(s.name, s);
            else Debug.LogWarning($"[AudioManager] Duplicate SFX name '{s.name}'.");
        }
    }

    private void CreateSources()
    {
        for (int i = 0; i < musicSources.Length; i++)
        {
            var go = new GameObject($"MusicSource_{i}");
            go.transform.SetParent(transform);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f;
            musicSources[i] = src;
        }

        for (int i = 0; i < initialSfxPoolSize; i++)
            CreateSfxSource();
    }

    private AudioSource CreateSfxSource()
    {
        var go = new GameObject($"SFXSource_{sfxPool.Count}");
        go.transform.SetParent(transform);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        sfxPool.Add(src);
        return src;
    }

    private AudioSource GetFreeSfxSource()
    {
        foreach (var src in sfxPool)
            if (!src.isPlaying) return src;

        if (sfxPool.Count < maxSfxPoolSize)
            return CreateSfxSource();

        // Pool is full: reuse sources round-robin (cuts off the oldest-ish sound)
        poolCursor = (poolCursor + 1) % sfxPool.Count;
        return sfxPool[poolCursor];
    }

    // -------------------------------------------------------------------- SFX

    /// <summary>Play a sound effect by name (2D). Returns the AudioSource used, or null.</summary>
    public AudioSource PlaySFX(string soundName, float volumeScale = 1f)
    {
        return PlaySFXInternal(soundName, volumeScale, false, Vector3.zero);
    }

    /// <summary>Play a sound effect at a world position (3D / spatial).</summary>
    public AudioSource PlaySFXAtPosition(string soundName, Vector3 position, float volumeScale = 1f)
    {
        return PlaySFXInternal(soundName, volumeScale, true, position);
    }

    private AudioSource PlaySFXInternal(string soundName, float volumeScale, bool spatial, Vector3 position)
    {
        if (!sfxLookup.TryGetValue(soundName, out var sound) || sound.clip == null)
        {
            Debug.LogWarning($"[AudioManager] SFX '{soundName}' not found (or has no clip).");
            return null;
        }

        var src = GetFreeSfxSource();
        src.Stop();
        src.clip = sound.clip;
        src.loop = sound.loop;
        src.volume = sound.volume * volumeScale * sfxVolume;
        src.pitch = sound.randomizePitch
            ? sound.pitch + UnityEngine.Random.Range(-sound.pitchVariance, sound.pitchVariance)
            : sound.pitch;
        src.spatialBlend = spatial ? 1f : 0f;
        src.transform.position = spatial ? position : transform.position;
        src.Play();
        return src;
    }

    /// <summary>Stop every playing SFX that uses this sound (handy for looping SFX).</summary>
    public void StopSFX(string soundName)
    {
        if (!sfxLookup.TryGetValue(soundName, out var sound)) return;
        foreach (var src in sfxPool)
            if (src.isPlaying && src.clip == sound.clip) src.Stop();
    }

    public void StopAllSFX()
    {
        foreach (var src in sfxPool) src.Stop();
    }

    public bool HasSFX(string soundName) => sfxLookup.ContainsKey(soundName);

    // ------------------------------------------------------------------ Music

    /// <summary>Play a music track, crossfading from the current one.</summary>
    public void PlayMusic(string trackName, float fadeTime = 1f)
    {
        if (!musicLookup.TryGetValue(trackName, out var track) || track.clip == null)
        {
            Debug.LogWarning($"[AudioManager] Music '{trackName}' not found (or has no clip).");
            return;
        }

        // Already playing this track
        if (currentMusic == track && musicSources[activeMusic].isPlaying) return;

        currentMusic = track;

        var from = musicSources[activeMusic];
        activeMusic = 1 - activeMusic;
        var to = musicSources[activeMusic];

        to.clip = track.clip;
        to.loop = true;
        to.pitch = track.pitch;
        to.volume = 0f;
        to.Play();

        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(CrossFade(from, to, track.volume, fadeTime));
    }

    public void StopMusic(float fadeTime = 1f)
    {
        currentMusic = null;
        if (musicFade != null) StopCoroutine(musicFade);
        musicFade = StartCoroutine(CrossFade(musicSources[activeMusic], null, 0f, fadeTime));
    }

    public void PauseMusic() { musicSources[activeMusic].Pause(); }
    public void ResumeMusic() { musicSources[activeMusic].UnPause(); }

    private IEnumerator CrossFade(AudioSource from, AudioSource to, float targetTrackVolume, float duration)
    {
        float fromStart = from != null ? from.volume : 0f;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;

            if (from != null) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to != null) to.volume = Mathf.Lerp(0f, targetTrackVolume * musicVolume, k);
            yield return null;
        }

        if (from != null) { from.volume = 0f; from.Stop(); }
        if (to != null) to.volume = targetTrackVolume * musicVolume;
        musicFade = null;
    }

    // ---------------------------------------------------------------- Volumes

    public void SetMasterVolume(float value)
    {
        masterVolume = Mathf.Clamp01(value);
        ApplyVolumes();
        PlayerPrefs.SetFloat(KeyMaster, masterVolume);
    }

    public void SetMusicVolume(float value)
    {
        musicVolume = Mathf.Clamp01(value);
        ApplyVolumes();
        PlayerPrefs.SetFloat(KeyMusic, musicVolume);
    }

    public void SetSfxVolume(float value)
    {
        sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(KeySfx, sfxVolume);
    }

    private void LoadVolumes()
    {
        masterVolume = PlayerPrefs.GetFloat(KeyMaster, masterVolume);
        musicVolume = PlayerPrefs.GetFloat(KeyMusic, musicVolume);
        sfxVolume = PlayerPrefs.GetFloat(KeySfx, sfxVolume);
    }

    private void ApplyVolumes()
    {
        AudioListener.volume = masterVolume;

        // Update the currently playing music (if not mid-fade)
        if (musicFade == null && currentMusic != null)
            musicSources[activeMusic].volume = currentMusic.volume * musicVolume;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Live-tweak sliders in the inspector during play mode
        if (Application.isPlaying && Instance == this && musicSources[0] != null)
            ApplyVolumes();
    }
#endif
}
