using System;
using UnityEngine;
using Unity.Services.Authentication;
using Unity.Services.Core;

public class UGSManager : MonoBehaviour
{
    public static UGSManager Instance { get; private set; }

    private async void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        try
        {
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[UGS] Signed in anonymously as ID: {AuthenticationService.Instance.PlayerId}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[UGS] Initialization failed: {e.Message}");
        }
    }
}