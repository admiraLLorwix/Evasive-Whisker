using UnityEngine;
using TMPro;
using System.Collections;

public class LoadingTextAnimator : MonoBehaviour
{
    private TMP_Text textComponent;
    private string currentBaseText = "Loading";
    private Coroutine animationRoutine;

    private void Awake()
    {
        textComponent = GetComponent<TMP_Text>();
    }

    public void StartAnimating(string baseText)
    {
        currentBaseText = baseText;
        if (animationRoutine != null) StopCoroutine(animationRoutine);
        animationRoutine = StartCoroutine(AnimateTextRoutine());
    }

    private IEnumerator AnimateTextRoutine()
    {
        int dotCount = 0;
        while (true)
        {
            string dots = new string('.', dotCount);
            textComponent.text = currentBaseText + dots;

            dotCount++;
            if (dotCount > 3) dotCount = 0;

            yield return new WaitForSeconds(0.4f);
        }
    }
}