using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CatInstinctUI : MonoBehaviour
{
    public static CatInstinctUI Instance { get; private set; }

    [SerializeField] private GameObject questionPanel;
    [SerializeField] private TMP_Text questionTitleText;
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;
    [SerializeField] private GameObject resultPanel;
    [SerializeField] private TMP_Text resultTitleText;
    [SerializeField] private Image resultCardImage;
    [SerializeField] private TMP_Text resultCaptionText;
    [SerializeField] private Sprite yesSprite;
    [SerializeField] private Sprite noSprite;

    private Coroutine timerRoutine;

    private void Awake()
    {
        Instance = this;

        if (questionPanel != null) questionPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);

        if (yesButton != null)
        {
            if (yesSprite != null && yesButton.image != null) yesButton.image.sprite = yesSprite;
            yesButton.onClick.AddListener(() => Answer(true));
        }

        if (noButton != null)
        {
            if (noSprite != null && noButton.image != null) noButton.image.sprite = noSprite;
            noButton.onClick.AddListener(() => Answer(false));
        }
    }

    // ------------------------------------------------------------------ target

    public void ShowQuestion(string askerName, float seconds)
    {
        if (questionPanel == null) return;

        if (questionTitleText != null)
            questionTitleText.text = $"<color=yellow>{askerName}</color> is asking if you hold the WhiskerBlower";

        if (yesButton != null) yesButton.interactable = true;
        if (noButton != null) noButton.interactable = true;

        questionPanel.SetActive(true);
        questionPanel.transform.SetAsLastSibling();

        if (timerRoutine != null) StopCoroutine(timerRoutine);
        if (timerText != null) timerRoutine = StartCoroutine(CountdownRoutine(seconds));
    }

    public void HideQuestion()
    {
        if (timerRoutine != null) StopCoroutine(timerRoutine);
        timerRoutine = null;
        if (questionPanel != null) questionPanel.SetActive(false);
    }

    private void Answer(bool saysYes)
    {
        if (yesButton != null) yesButton.interactable = false;
        if (noButton != null) noButton.interactable = false;

        GameManager.Instance?.SubmitCatInstinctAnswerServerRpc(saysYes);
        HideQuestion();
    }

    private IEnumerator CountdownRoutine(float seconds)
    {
        float remaining = seconds;
        while (remaining > 0f)
        {
            timerText.text = Mathf.CeilToInt(remaining).ToString();
            remaining -= Time.deltaTime;
            yield return null;
        }
        timerText.text = "0";
    }

    // ------------------------------------------------------------------ asker

    public void ShowResult(string targetName, bool saysYes, bool lying)
    {
        if (resultPanel == null) return;

        if (resultTitleText != null)
            resultTitleText.text = $"<color=yellow>{targetName}</color> answered:";

        if (resultCardImage != null)
        {
            Sprite answerSprite = saysYes ? yesSprite : noSprite;
            if (answerSprite != null) resultCardImage.sprite = answerSprite;
            resultCardImage.color = Color.white;
        }

        if (resultCaptionText != null)
        {
            resultCaptionText.text = lying
                ? "<color=red>This cat is lying.</color>"
                : "<color=green>This cat is telling the truth.</color>";
        }

        resultPanel.SetActive(true);
        resultPanel.transform.SetAsLastSibling();
    }

    public void HideResult()
    {
        if (resultPanel != null) resultPanel.SetActive(false);
    }
}