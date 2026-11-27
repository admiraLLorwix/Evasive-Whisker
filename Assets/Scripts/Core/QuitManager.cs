using UnityEngine;

public class QuitManager : MonoBehaviour
{
    public void QuitGame()
    {
        Debug.Log("Quit button clicked!");
        Application.Quit();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }
}
