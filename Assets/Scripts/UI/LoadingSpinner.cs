using UnityEngine;

public class LoadingSpinner : MonoBehaviour
{
    [SerializeField] private float spinSpeed = -200f;

    private void Update()
    {
        transform.Rotate(0f, 0f, spinSpeed * Time.deltaTime);
    }
}