using UnityEngine;

public class CameraTrackEmpty : MonoBehaviour
{
	public Transform target; // Drag your empty GameObject here in the Inspector
	public float smoothSpeed = 5.0f;
	public Vector3 offset;

	void LateUpdate()
	{
		if (target == null) return;

		Vector3 desiredPosition = target.position + offset;
		transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
		transform.LookAt(target.position);
	}
}
