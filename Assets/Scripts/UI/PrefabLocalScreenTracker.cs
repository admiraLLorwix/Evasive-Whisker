using UnityEngine;

[RequireComponent(typeof(RectTransform))]
public class PrefabLocalScreenTracker : MonoBehaviour
{
	[Tooltip("The 3D transform inside this prefab that the UI should follow.")]
	[SerializeField] private Transform targetAnchor;

	private Camera mainCamera;
	private RectTransform rectTransform;

	private void Awake()
	{
		rectTransform = GetComponent<RectTransform>();

		// FIX 1: Auto-detach the Canvas from the 3D object at runtime.
		// This stops the 3D Transform from fighting the Overlay Canvas.
		Canvas parentCanvas = GetComponentInParent<Canvas>();
		if (parentCanvas != null && parentCanvas.transform.parent != null)
		{
			parentCanvas.transform.SetParent(null);
			// Optional: Keep the scene clean by renaming the detached canvas
			parentCanvas.name = targetAnchor.parent.name + "_DetachedUI";
		}
	}

	private void Start()
	{
		mainCamera = Camera.main;
	}

	private void LateUpdate()
	{
		if (targetAnchor == null) return;
		if (mainCamera == null) mainCamera = Camera.main;

		Vector3 screenPos = mainCamera.WorldToScreenPoint(targetAnchor.position);

		// Cull UI if the 3D anchor moves behind the camera
		if (screenPos.z < 0)
		{
			if (rectTransform.gameObject.activeSelf)
				rectTransform.gameObject.SetActive(false);
			return;
		}

		if (!rectTransform.gameObject.activeSelf)
			rectTransform.gameObject.SetActive(true);

		// FIX 2: Strip the Z depth by casting to Vector2.
		// This ensures the UI stays perfectly flat on the screen.
		rectTransform.position = (Vector2)screenPos;
	}
}