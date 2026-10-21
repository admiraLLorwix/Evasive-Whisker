using System;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Lobbies;
using Unity.Services.Lobbies.Models;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class OnlineLobbyManager : MonoBehaviour
{
    public static OnlineLobbyManager Instance { get; private set; }

    public Lobby CurrentLobby { get; private set; }
    public string LocalPlayerName { get; set; }
    public string RelayJoinCode { get; private set; }
    public event Action<List<Lobby>> OnLobbyListUpdated;
    public event Action<Lobby> OnLobbyJoined;
    public event Action<Lobby> OnLobbyUpdated;
    public event Action OnLobbyLeft;

    private float heartbeatTimer;
    private float lobbyPollTimer;
    private bool isPolling = false;

    private void Awake()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientDisconnectCallback += HandleClientDisconnect;
        }
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        LocalPlayerName = $"Detective_{UnityEngine.Random.Range(100, 999)}"; //generate random name
    }

    private void HandleClientDisconnect(ulong clientId)
    {
        if (!NetworkManager.Singleton.IsServer && clientId == NetworkManager.Singleton.LocalClientId)
        {
            Debug.LogWarning("[Netcode] Disconnected from host. Returning to menu...");
            LeaveLobby();
            UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
        }
    }

    private void Update()
    {
        HandleLobbyHeartbeat();
        HandleLobbyPolling();
    }




    public async Task<bool> InitializeServicesAsync()
    {
        try
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
            {
                InitializationOptions options = new InitializationOptions();

    #if UNITY_EDITOR
                string profileName = "Profile_" + Math.Abs(Application.dataPath.GetHashCode());
                options.SetProfile(profileName);
    #endif

                await UnityServices.InitializeAsync(options);
            }

            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
                Debug.Log($"[UGS] Signed in as ID: {AuthenticationService.Instance.PlayerId} (Profile: {AuthenticationService.Instance.Profile})");//generate random id
            }
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UGS] Auth Failed: {ex.Message}");
            return false;
        }
    }


    //host relay

    public async Task<bool> CreateLobbyAsync(string lobbyName, int maxPlayers = 6)
    {
        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);//relaying
            RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                false
            );

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayerDataObject(),
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Public, RelayJoinCode) }
                }
            };

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);

            NetworkManager.Singleton.StartHost();

            Debug.Log($"[Lobby] Created '{lobbyName}' | Lobby Code: {CurrentLobby.LobbyCode} | Relay Code: {RelayJoinCode}");

            OnLobbyJoined?.Invoke(CurrentLobby);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Failed to create lobby: {e.Message}");
            return false;
        }
    }


    //client join

    public async Task<bool> JoinLobbyByCodeAsync(string lobbyCode)
    {
        try
        {
            JoinLobbyByCodeOptions options = new JoinLobbyByCodeOptions { Player = GetPlayerDataObject() };
            CurrentLobby = await LobbyService.Instance.JoinLobbyByCodeAsync(lobbyCode, options);
            return await ConnectToRelayAndStartClient();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Join by code failed: {e.Message}");
            return false;
        }
    }

    public async Task<bool> JoinLobbyByIdAsync(string lobbyId)
    {
        try
        {
            JoinLobbyByIdOptions options = new JoinLobbyByIdOptions { Player = GetPlayerDataObject() };
            CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobbyId, options);
            return await ConnectToRelayAndStartClient();
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Join by ID failed: {e.Message}");
            return false;
        }
    }

    private async Task<bool> ConnectToRelayAndStartClient()
    {
        try
        {
            if (CurrentLobby == null || !CurrentLobby.Data.ContainsKey("RelayCode"))
            {
                Debug.LogError("[Lobby] Lobby data does not contain a valid Code");
                return false;
            }

            string relayCode = CurrentLobby.Data["RelayCode"].Value;
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(relayCode);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetClientRelayData(
                joinAllocation.RelayServer.IpV4,
                (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes,
                joinAllocation.Key,
                joinAllocation.ConnectionData,
                joinAllocation.HostConnectionData,
                false
            );

            bool success = NetworkManager.Singleton.StartClient();
            if (success)
            {
                OnLobbyJoined?.Invoke(CurrentLobby);
            }
            return success;
        }
        catch (Exception e)
        {
            Debug.LogError($"[Relay] Client join failed: {e.Message}");
            return false;
        }
    }


    // ready logic

    public async Task UpdatePlayerReadyStateAsync(bool isReady)
    {
        if (CurrentLobby == null) return;

        try
        {

            //auth refresh
            string playerId = AuthenticationService.Instance.PlayerId;
            UpdatePlayerOptions options = new UpdatePlayerOptions
            {

                Data = new Dictionary<string, PlayerDataObject>
                {
                    { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, LocalPlayerName) },
                    { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, isReady ? "True" : "False") }
                }
            };

            CurrentLobby = await LobbyService.Instance.UpdatePlayerAsync(CurrentLobby.Id, playerId, options);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Failed to update ready state: {e.Message}");
        }
    }


    //lobby get

    public async void RefreshLobbyList()
    {
        try
        {
            QueryLobbiesOptions options = new QueryLobbiesOptions
            {
                Count = 20,
                Filters = new List<QueryFilter>
                {
                    new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT)
                }
            };

            QueryResponse response = await LobbyService.Instance.QueryLobbiesAsync(options);
            OnLobbyListUpdated?.Invoke(response.Results);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Query failed: {e.Message}");
        }
    }

    //leaving lobby -.

    public async void LeaveLobby()
    {
        if (CurrentLobby == null) return;

        try
        {
            string playerId = AuthenticationService.Instance.PlayerId;


            if (IsHost())
            {
                await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, playerId);
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[Lobby] Leave warning: {e.Message}");
        }
        finally
        {
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
            {
                NetworkManager.Singleton.Shutdown();
            }
            CurrentLobby = null;
            OnLobbyLeft?.Invoke();
        }
    }

    public async void KickPlayer(string playerId)
    {
        if (!IsHost() || CurrentLobby == null) return;

        try
        {
            await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, playerId);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Kick failed: {e.Message}");
        }
    }





    private Player GetPlayerDataObject()
    {
        return new Player
        {
            Data = new Dictionary<string, PlayerDataObject>
            {
                { "PlayerName", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, LocalPlayerName) },
                { "IsReady", new PlayerDataObject(PlayerDataObject.VisibilityOptions.Public, "False") }
            }
        };
    }

    public bool IsHost()
    {
        return CurrentLobby != null &&
               AuthenticationService.Instance.IsSignedIn &&
               CurrentLobby.HostId == AuthenticationService.Instance.PlayerId;
    }

    private async void HandleLobbyHeartbeat()
    {
        if (IsHost() && CurrentLobby != null)
        {
            heartbeatTimer -= Time.deltaTime;
            if (heartbeatTimer <= 0f)
            {
                heartbeatTimer = 15f; //not lower than 15
                try
                {
                    await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Lobby] Heartbeat failed: {e.Message}");
                }
            }
        }
    }

    private async void HandleLobbyPolling()
    {
        if (CurrentLobby != null && !isPolling)
        {
            lobbyPollTimer -= Time.deltaTime;
            if (lobbyPollTimer <= 0f)
            {
                lobbyPollTimer = 2.5f; //prev: 2f = err
                isPolling = true;

                try
                {
                    CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
                    OnLobbyUpdated?.Invoke(CurrentLobby);

                    bool stillInLobby = CurrentLobby.Players.Exists(p => p.Id == AuthenticationService.Instance.PlayerId);
                    if (!stillInLobby)
                    {
                        LeaveLobby();
                    }
                }
                catch (LobbyServiceException ex) when (ex.Reason == LobbyExceptionReason.LobbyNotFound)
                {
                    LeaveLobby();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Lobby] Poll warning: {e.Message}");
                }
                finally
                {
                    isPolling = false;
                }
            }
        }
    }

}