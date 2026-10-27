using UnityEngine;
using UnityEngine.InputSystem;

public class MouseTilt : MonoBehaviour
{
	[Header("Speed Settings")]
	public float tiltSpeed = 0.5f;
	public float turnSpeed = 2.0f; // Separate speed for the Y-axis rotation

	[Header("Angle Limits")]
	public float maxTiltAngle = 30.0f;

	private float tiltX = 0f;
	private float tiltZ = 0f;
	private float rotationY = 0f; // Tracks global horizontal rotation

	void Update()
	{
		if (Mouse.current == null) return;

		// Read mouse delta (movement since last frame)
		Vector2 mouseDelta = Mouse.current.delta.ReadValue();

		// 1. Calculate Tilts (X and Z)
		tiltX -= mouseDelta.y * tiltSpeed * Time.deltaTime;
		tiltZ -= mouseDelta.x * tiltSpeed * Time.deltaTime;

		// Clamp tilts so it doesn't completely invert upside down locally
		tiltX = Mathf.Clamp(tiltX, -maxTiltAngle, maxTiltAngle);
		tiltZ = Mathf.Clamp(tiltZ, -maxTiltAngle, maxTiltAngle);

		// 2. Calculate Global Spin (Y)
		// Moving mouse right/left spins it on the Y axis over time
		rotationY += mouseDelta.x * turnSpeed * Time.deltaTime;

		// 3. Combine Rotations
		// Global/Parent turning combined with local pitching/rolling
		Quaternion globalTurn = Quaternion.Euler(0f, -rotationY, 0f);
		Quaternion localTilt = Quaternion.Euler(-tiltX, 0f, 0f);

		transform.localRotation = globalTurn * localTilt;
	}
}
