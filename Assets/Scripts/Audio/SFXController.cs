using UnityEngine;
using UnityEngine.EventSystems;

public class SFXController : MonoBehaviour, IPointerClickHandler
{
    public enum PlayMode
    {
        Manual,
        OnAwake,
        OnEnable,
        OnClick
    }

    [Header("Sound Effect GameObject")]
    [SerializeField] private GameObject sfxObject;

    [Header("Playback Settings")]
    [SerializeField] private PlayMode playMode = PlayMode.OnClick;

    private AudioSource audioSource;

    private void Awake()
    {
        CacheAudioSource();

        if (playMode == PlayMode.OnAwake)
        {
            PlaySFX();
        }
    }

    private void OnEnable()
    {
        if (playMode == PlayMode.OnEnable)
        {
            PlaySFX();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playMode == PlayMode.OnClick)
        {
            PlaySFX();
        }
    }

    public void PlaySFX()
    {
        if (sfxObject == null)
        {
            Debug.LogWarning("SFX GameObject is not assigned.", this);
            return;
        }

        if (audioSource == null)
        {
            CacheAudioSource();
        }

        if (audioSource == null)
        {
            Debug.LogWarning(
                $"The assigned SFX GameObject '{sfxObject.name}' has no AudioSource component.",
                sfxObject
            );
            return;
        }

        audioSource.Play();
    }

    private void CacheAudioSource()
    {
        if (sfxObject != null)
        {
            audioSource = sfxObject.GetComponent<AudioSource>();
        }
    }

    public void SetSFXObject(GameObject newSFXObject)
    {
        sfxObject = newSFXObject;
        audioSource = null;
        CacheAudioSource();
    }
}
