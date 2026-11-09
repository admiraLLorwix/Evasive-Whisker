using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class CardFlightManager : MonoBehaviour
{
    public static CardFlightManager Instance { get; private set; }

    [SerializeField] private GameObject flyingCardDummyPrefab;
    [SerializeField] private float flightDuration = 0.38f;
    [SerializeField] private AnimationCurve flightCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    private Canvas mainCanvas;

    private void Awake()
    {
        Instance = this;
        mainCanvas = GetComponentInParent<Canvas>();
    }

    public void FlyCard(Vector3 startScreenPos, Vector3 targetScreenPos, Sprite cardSprite, System.Action onComplete = null)
    {
        StartCoroutine(FlightRoutine(startScreenPos, targetScreenPos, cardSprite, onComplete));
    }

    private IEnumerator FlightRoutine(Vector3 start, Vector3 target, Sprite sprite, System.Action onComplete)
    {
        if (mainCanvas == null) yield break;

        GameObject dummy = new GameObject("FlyingCardDummy", typeof(RectTransform), typeof(Image), typeof(Canvas));
        dummy.transform.SetParent(mainCanvas.transform, false);

        Canvas dummyCanvas = dummy.GetComponent<Canvas>();
        dummyCanvas.overrideSorting = true;
        dummyCanvas.sortingOrder = 50;

        dummy.transform.SetAsLastSibling();

        RectTransform rt = dummy.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(130f, 190f);

        Image img = dummy.GetComponent<Image>();
        img.sprite = sprite;
        img.color = Color.white;
        img.raycastTarget = false;

        float elapsed = 0f;
        while (elapsed < flightDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);
            float curveT = flightCurve.Evaluate(t);

            Vector3 currentPos = Vector3.Lerp(start, target, curveT);
            float arcPeak = Mathf.Sin(t * Mathf.PI) * 80f;
            currentPos.y += arcPeak;

            float tilt = Mathf.Sin(t * Mathf.PI) * 12f;
            dummy.transform.localRotation = Quaternion.Euler(0f, 0f, tilt);

            dummy.transform.position = currentPos;
            yield return null;
        }

        Destroy(dummy);
        onComplete?.Invoke();
    }
}