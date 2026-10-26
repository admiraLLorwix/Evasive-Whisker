using UnityEngine;

public class FloatingIsland : MonoBehaviour
{
    [Header("float Settings")]
    [SerializeField] private float floatDistance = 12f;

    [SerializeField] private float floatSpeed = 1.8f;

    [Header("tilt Settings")]
    [SerializeField] private bool enableTilt = true;
    [SerializeField] private float maxTiltAngle = 1.5f;

    private RectTransform rectTransform;
    private Vector2 startAnchoredPos;
    private Vector3 startLocalPos;
    private float timeOffset;
    private bool isUI = false;

    private void Start()
    {
        rectTransform = GetComponent<RectTransform>();
        isUI = rectTransform != null;

        if (isUI)
        {
            startAnchoredPos = rectTransform.anchoredPosition;
        }
        else
        {
            startLocalPos = transform.localPosition;
        }

        timeOffset = Random.Range(0f, 10f);
    }

    private void Update()
    {
        float elapsed = (Time.time * floatSpeed) + timeOffset;

        float yOffset = Mathf.Sin(elapsed) * floatDistance;

        if (isUI)
        {
            rectTransform.anchoredPosition = new Vector2(startAnchoredPos.x, startAnchoredPos.y + yOffset);
        }
        else
        {
            transform.localPosition = new Vector3(startLocalPos.x, startLocalPos.y + yOffset, startLocalPos.z);
        }

        if (enableTilt)
        {
            float zRotation = Mathf.Cos(elapsed * 0.7f) * maxTiltAngle;
            transform.localRotation = Quaternion.Euler(0f, 0f, zRotation);
        }
    }
}