using UnityEngine;

[ExecuteAlways]
public class FollowObject : MonoBehaviour
{
	[Tooltip("The object this GameObject should track.")]
	public Transform target;

	[Tooltip("Keep checking and updating in the editor even if nothing is moving.")]
	public bool updateEveryFrame = true;

	void Update()
	{
		// Prevent running if there is no target assigned
		if (target == null) return;

		// Track the object
		transform.LookAt(target);
	}

	// Ensures smooth updates in the Scene View when moving objects manually
	void OnDrawGizmos()
	{
		if (!updateEveryFrame && target != null)
		{
			transform.LookAt(target);
		}
	}
}
