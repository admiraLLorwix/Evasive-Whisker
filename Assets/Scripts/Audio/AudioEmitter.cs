using UnityEngine;

/// <summary>
/// Attach to any GameObject to play a sound from the AudioManager.
/// Pick the sound name (must match a name in the AudioManager's library)
/// and tick when it should play.
///
/// Also callable from UI Buttons / UnityEvents / other scripts:
///     GetComponent<AudioEmitter>().Play();
/// </summary>
[AddComponentMenu("Audio/Audio Emitter")]
public class AudioEmitter : MonoBehaviour
{
    public enum SoundType { SFX, Music }

    [Header("Sound")]
    [Tooltip("Must match a name in the AudioManager's SFX or Music list.")]
    [SerializeField] private string soundName;
    [SerializeField] private SoundType soundType = SoundType.SFX;
    [Range(0f, 1f)]
    [SerializeField] private float volumeScale = 1f;

    [Header("SFX Options")]
    [Tooltip("Play from this object's position in 3D space instead of flat 2D.")]
    [SerializeField] private bool positional = false;
    [Tooltip("Minimum seconds between plays (prevents spam).")]
    [SerializeField] private float cooldown = 0f;

    [Header("Music Options")]
    [SerializeField] private float musicFadeTime = 1f;

    [Header("When to Play")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private bool playOnEnable = false;
    [SerializeField] private bool playOnMouseDown = false;
    [SerializeField] private bool playOnMouseEnter = false;
    [SerializeField] private bool playOnTrigger = false;
    [SerializeField] private bool playOnCollision = false;
    [SerializeField] private bool playOnDisable = false;
    [SerializeField] private bool playOnDestroy = false;

    [Header("Trigger / Collision Filter")]
    [Tooltip("Leave empty to react to anything.")]
    [SerializeField] private string requiredTag = "";

    private float lastPlayTime = -999f;
    private AudioSource lastSource;
    private bool quitting;

    // ----------------------------------------------------------- public API

    /// <summary>Play the assigned sound.</summary>
    public void Play()
    {
        PlayInternal(soundName);
    }

    /// <summary>Play a different sound by name (useful from UnityEvents).</summary>
    public void PlayByName(string name)
    {
        PlayInternal(name);
    }

    /// <summary>Stop this emitter's sound (looping SFX or music).</summary>
    public void Stop()
    {
        if (AudioManager.Instance == null) return;

        if (soundType == SoundType.Music)
            AudioManager.Instance.StopMusic(musicFadeTime);
        else if (lastSource != null)
            lastSource.Stop();
    }

    // -------------------------------------------------------------- internals

    private void PlayInternal(string name)
    {
        if (AudioManager.Instance == null)
        {
            Debug.LogWarning($"[AudioEmitter] No AudioManager in the scene ({gameObject.name}).");
            return;
        }

        if (string.IsNullOrEmpty(name)) return;
        if (Time.unscaledTime - lastPlayTime < cooldown) return;
        lastPlayTime = Time.unscaledTime;

        if (soundType == SoundType.Music)
        {
            AudioManager.Instance.PlayMusic(name, musicFadeTime);
        }
        else
        {
            lastSource = positional
                ? AudioManager.Instance.PlaySFXAtPosition(name, transform.position, volumeScale)
                : AudioManager.Instance.PlaySFX(name, volumeScale);
        }
    }

    private bool PassesFilter(GameObject other)
    {
        return string.IsNullOrEmpty(requiredTag) || other.CompareTag(requiredTag);
    }

    // ------------------------------------------------------------ Unity events

    private void Start() { if (playOnStart) Play(); }
    private void OnEnable() { if (playOnEnable) Play(); }
    private void OnDisable() { if (playOnDisable && !quitting) Play(); }
    private void OnDestroy() { if (playOnDestroy && !quitting) Play(); }
    private void OnApplicationQuit() { quitting = true; }

    private void OnMouseDown() { if (playOnMouseDown) Play(); }
    private void OnMouseEnter() { if (playOnMouseEnter) Play(); }

    // 3D
    private void OnTriggerEnter(Collider other)
    {
        if (playOnTrigger && PassesFilter(other.gameObject)) Play();
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (playOnCollision && PassesFilter(collision.gameObject)) Play();
    }

    // 2D
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playOnTrigger && PassesFilter(other.gameObject)) Play();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (playOnCollision && PassesFilter(collision.gameObject)) Play();
    }
}
