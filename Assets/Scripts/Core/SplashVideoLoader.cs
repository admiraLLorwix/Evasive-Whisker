using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

public class SplashVideoLoader : MonoBehaviour
{
    [Header("Fade Settings")]
    public Image fadeOverlay;
    public float fadeDuration = 1.0f;

    private VideoPlayer videoPlayer;
    private bool isTransitioning = false;

    void Start()
    {
        videoPlayer = GetComponent<VideoPlayer>();
        videoPlayer.loopPointReached += OnVideoFinished;

        if (fadeOverlay != null)
        {
            Color c = fadeOverlay.color;
            c.a = 0f;
            fadeOverlay.color = c;
        }
    }

    void OnVideoFinished(VideoPlayer vp)
    {
        StartFadeOut();
    }

    void StartFadeOut()
    {
        if (isTransitioning) return;
        isTransitioning = true;
        StartCoroutine(FadeAndLoad());
    }

    IEnumerator FadeAndLoad()
    {
        if (fadeOverlay != null)
        {
            float elapsed = 0f;
            Color c = fadeOverlay.color;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                c.a = Mathf.Clamp01(elapsed / fadeDuration);
                fadeOverlay.color = c;
                yield return null;
            }
        }

        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex + 1);
    }
}
