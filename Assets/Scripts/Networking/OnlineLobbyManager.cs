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
using EvasiveWhisker.UI;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

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
    private const string ReturnLobbyNamePrefix = "Ret-";

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
        if (NetworkManager.Singleton == null) return;
        if (NetworkManager.Singleton.IsServer) return;
        if (clientId != NetworkManager.Singleton.LocalClientId) return;

        if (GameManager.Instance != null && GameManager.Instance.IsSpawned)
            return;

        Debug.LogWarning("[dc] disconnected from host");
        LeaveLobby();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
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
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
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
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
            RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData, false);

            CreateLobbyOptions options = new CreateLobbyOptions
            {
                IsPrivate = false,
                Player = GetPlayerDataObject(),
                Data = new Dictionary<string, DataObject>
                {
                    { "RelayCode", new DataObject(DataObject.VisibilityOptions.Public, RelayJoinCode) },
                    { "BotCount", new DataObject(DataObject.VisibilityOptions.Public, "0") }
                }
            };

            CurrentLobby = await LobbyService.Instance.CreateLobbyAsync(lobbyName, maxPlayers, options);
            NetworkManager.Singleton.StartHost();

            Debug.Log($"[Lobby] Created '{lobbyName}' | Code: {CurrentLobby.LobbyCode} | Relay Code: {RelayJoinCode}");
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

            if (CurrentLobby.Data != null && CurrentLobby.Data.TryGetValue("IsLocked", out var lockData) && lockData.Value == "True")
            {
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
                CurrentLobby = null;
                Debug.LogWarning("[Lobby] Cannot join: Lobby is locked by the host");
                return false;
            }

            return await ConnectToRelayAndStartClient();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[Lobby] Cannot join: {e.Message}");
            return false;
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

            if (CurrentLobby.Data != null && CurrentLobby.Data.TryGetValue("IsLocked", out var lockData) && lockData.Value == "True")
            {
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
                CurrentLobby = null;
                Debug.LogWarning("[Lobby] Cannot join: Lobby is locked by the host");
                return false;
            }

            return await ConnectToRelayAndStartClient();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogError($"[Lobby] Cannot join: {e.Message}");
            return false;
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
                joinAllocation.RelayServer.IpV4, (ushort)joinAllocation.RelayServer.Port,
                joinAllocation.AllocationIdBytes, joinAllocation.Key,
                joinAllocation.ConnectionData, joinAllocation.HostConnectionData, false);

            bool success = NetworkManager.Singleton.StartClient();
            if (success) OnLobbyJoined?.Invoke(CurrentLobby);
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
        catch (Exception e) { Debug.LogError($"[Lobby] Update ready failed: {e.Message}"); }
    }

    public async Task ToggleLobbyLockAsync(bool isLocked)
    {
        if (CurrentLobby == null || !IsHost()) return;

        try
        {
            string oldId = MatchContext.PreviousLobbyId ?? CurrentLobby.Id;

            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Password = isLocked ? DeriveReturnPassword(oldId) : "",

                Data = new Dictionary<string, DataObject>
                {
                    { "IsLocked", new DataObject(DataObject.VisibilityOptions.Public, isLocked ? "True" : "False") }
                }
            };

            CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, options);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Failed to toggle lock: {e.Message}");
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

            var visibleLobbies = response.Results.Where(l =>
                l.Data == null || !l.Data.ContainsKey("IsLocked") || l.Data["IsLocked"].Value != "True"
            ).ToList();

            OnLobbyListUpdated?.Invoke(visibleLobbies);
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
            string myPlayerId = AuthenticationService.Instance.PlayerId;

            if (IsHost() && CurrentLobby.Players.Count > 1)
            {
                Player nextHost = CurrentLobby.Players[1];
                await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions { HostId = nextHost.Id });
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, myPlayerId);
            }
            else if (IsHost())
            {
                await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
            }
            else
            {
                await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, myPlayerId);
            }
        }
        catch (Exception e) { Debug.LogWarning($"[Lobby] Leave warning: {e.Message}"); }
        finally
        {
            if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
                NetworkManager.Singleton.Shutdown();
            CurrentLobby = null;
            OnLobbyLeft?.Invoke();
        }
    }

    public async Task KickPlayer(string ugsPlayerId)
    {
        if (!IsHost() || CurrentLobby == null) return;
        if (ugsPlayerId == AuthenticationService.Instance.PlayerId) return;
        try
        {
            await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, ugsPlayerId);
            CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (Exception e) { Debug.LogError($"[Lobby] Kick failed: {e.Message}"); }
    }

    public async Task LeaveAllAsync()
    {
        if (NetworkManager.Singleton != null)
        {
            try { NetworkManager.Singleton.Shutdown(); } catch { }
            await Task.Delay(150);
        }

        if (CurrentLobby != null)
        {
            try
            {
                if (IsHost())
                    await LobbyService.Instance.DeleteLobbyAsync(CurrentLobby.Id);
                else
                    await LobbyService.Instance.RemovePlayerAsync(CurrentLobby.Id, AuthenticationService.Instance.PlayerId);
            }
            catch (Exception e) { Debug.LogWarning($"[Lobby] LeaveAll warning: {e.Message}"); }
            CurrentLobby = null;
            OnLobbyLeft?.Invoke();
        }
    }

    public async Task<Lobby> ReturnToLobbyAsync()
    {
        string oldId = MatchContext.PreviousLobbyId;
        if (string.IsNullOrEmpty(oldId))
            throw new InvalidOperationException("No previous lobby id; cannot return");

        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            try { NetworkManager.Singleton.Shutdown(); } catch { }
            await Task.Delay(200);
        }
        CurrentLobby = null;

        var existing = await QueryReturnLobbiesAsync(oldId);
        if (existing.Count > 0)
            return await JoinReturnLobbyAsync(existing.OrderBy(l => l.Created).First(), oldId);

        var created = await CreateReturnLobbyAsync(oldId);

        await Task.Delay(1500);
        var after = await QueryReturnLobbiesAsync(oldId);
        if (after.Count > 1)
        {
            var oldest = after.OrderBy(l => l.Created).First();
            if (oldest.Id != created.Id)
            {
                try { await LobbyService.Instance.DeleteLobbyAsync(created.Id); }
                catch (LobbyServiceException) { }
                return await JoinReturnLobbyAsync(oldest, oldId);
            }
        }
        return created;
    }

    private static async Task<List<Lobby>> QueryReturnLobbiesAsync(string oldLobbyId)
    {
        var opts = new QueryLobbiesOptions
        {
            Count = 10,
            Filters = new List<QueryFilter>
            {
                new QueryFilter(QueryFilter.FieldOptions.Name, ReturnLobbyName(oldLobbyId), QueryFilter.OpOptions.EQ),
                new QueryFilter(QueryFilter.FieldOptions.AvailableSlots, "0", QueryFilter.OpOptions.GT),
            }
        };
        try
        {
            var res = await LobbyService.Instance.QueryLobbiesAsync(opts);
            return res.Results.ToList();
        }
        catch (LobbyServiceException e)
        {
            Debug.LogWarning($"[Lobby] QueryReturn failed: {e.Message}");
            return new List<Lobby>();
        }
    }

    private async Task<Lobby> CreateReturnLobbyAsync(string oldLobbyId)
    {
        int maxPlayers = Mathf.Max(2, MatchContext.PreviousMaxPlayers);

        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxPlayers - 1);
        RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

        var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetHostRelayData(
            allocation.RelayServer.IpV4, (ushort)allocation.RelayServer.Port,
            allocation.AllocationIdBytes, allocation.Key, allocation.ConnectionData, false);

        var opts = new CreateLobbyOptions
        {
            IsPrivate = false,
            Password = DeriveReturnPassword(oldLobbyId),
            Player = GetPlayerDataObject(),
            Data = new Dictionary<string, DataObject>
            {
                { "RelayCode",       new DataObject(DataObject.VisibilityOptions.Public, RelayJoinCode) },
                { "returnMatchId",   new DataObject(DataObject.VisibilityOptions.Public, oldLobbyId) },
                { "IsLocked",        new DataObject(DataObject.VisibilityOptions.Public, "True") } // NEW
            }
        };

        var lobby = await LobbyService.Instance.CreateLobbyAsync(
            ReturnLobbyName(oldLobbyId), maxPlayers, opts);

        NetworkManager.Singleton.StartHost();
        CurrentLobby = lobby;
        Debug.Log($"[Lobby] Created return-lobby '{lobby.Name}' | Code: {lobby.LobbyCode}");
        OnLobbyJoined?.Invoke(lobby);
        return lobby;
    }

    private async Task<Lobby> JoinReturnLobbyAsync(Lobby lobby, string oldLobbyId)
    {
        var joinOpts = new JoinLobbyByIdOptions
        {
            Player = GetPlayerDataObject(),
            Password = DeriveReturnPassword(oldLobbyId)
        };

        try
        {
            CurrentLobby = await LobbyService.Instance.JoinLobbyByIdAsync(lobby.Id, joinOpts);
            bool ok = await ConnectToRelayAndStartClient();
            if (!ok) throw new Exception("Joined return-lobby but failed to connect to relay");
            return CurrentLobby;
        }
        catch (LobbyServiceException e) when (e.Reason == LobbyExceptionReason.LobbyLocked)
        {
            throw new Exception("The host has locked the lobby. Please wait for them to unlock it");
        }
    }

    private static string ReturnLobbyName(string oldLobbyId) => ReturnLobbyNamePrefix + HashHex(oldLobbyId, 6);
    private static string DeriveReturnPassword(string oldLobbyId) => HashHex(oldLobbyId + "|EvasiveWhiskerReturn", 4);

    private static string HashHex(string s, int byteCount)
    {
        using var sha = SHA256.Create();
        var h = sha.ComputeHash(Encoding.UTF8.GetBytes(s));
        return BitConverter.ToString(h, 0, byteCount).Replace("-", "");
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
                heartbeatTimer = 15f;
                try { await LobbyService.Instance.SendHeartbeatPingAsync(CurrentLobby.Id); }
                catch (Exception e) { Debug.LogWarning($"[Lobby] Heartbeat failed: {e.Message}"); }
            }
        }
    }

    private async void HandleLobbyPolling()
    {
        if (CurrentLobby == null || isPolling) return;
        lobbyPollTimer -= Time.deltaTime;
        if (lobbyPollTimer > 0f) return;

        lobbyPollTimer = 2.5f;
        isPolling = true;
        try
        {
            CurrentLobby = await LobbyService.Instance.GetLobbyAsync(CurrentLobby.Id);
            OnLobbyUpdated?.Invoke(CurrentLobby);

            string myId = AuthenticationService.Instance.PlayerId;
            bool stillInLobby = CurrentLobby.Players.Exists(p => p.Id == myId);
            if (!stillInLobby) { HandleKickedOrHostClosed(); return; }

            if (CurrentLobby.HostId == myId && !NetworkManager.Singleton.IsHost)
            {
                Debug.Log("[Lobby] You're the new host");
                await TakeOverAsHostAsync();
            }
        }
        catch (LobbyServiceException ex) when (ex.Reason == LobbyExceptionReason.LobbyNotFound)
        {
            HandleKickedOrHostClosed();
        }
        catch (Exception e) { Debug.LogWarning($"[Lobby] Poll warning: {e.Message}"); }
        finally { isPolling = false; }
    }

    private async Task TakeOverAsHostAsync()
    {
        try
        {
            if (NetworkManager.Singleton.IsClient) NetworkManager.Singleton.Shutdown();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(CurrentLobby.MaxPlayers - 1);
            RelayJoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            transport.SetHostRelayData(
                allocation.RelayServer.IpV4,
                (ushort)allocation.RelayServer.Port,
                allocation.AllocationIdBytes,
                allocation.Key,
                allocation.ConnectionData,
                false);

            await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    {"RelayCode", new DataObject(DataObject.VisibilityOptions.Public, RelayJoinCode) }
                }
            });

            NetworkManager.Singleton.StartHost();
            Debug.Log($"[Lobby] Took over lobby. New Relay Code: {RelayJoinCode}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[Lobby] Failed to takeover: {e.Message}");
        }
    }

    private void HandleKickedOrHostClosed()
    {
        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        NetworkManager.Singleton.Shutdown();

        CurrentLobby = null;
        OnLobbyLeft?.Invoke();
        UIManager.Instance?.ShowPlayScreen();
    }

    public async Task UpdateLobbyBotCountAsync(int count)
    {
        if (CurrentLobby == null || !IsHost()) return;

        try
        {
            UpdateLobbyOptions options = new UpdateLobbyOptions
            {
                Data = new Dictionary<string, DataObject>
                {
                    { "BotCount", new DataObject(DataObject.VisibilityOptions.Public, count.ToString()) }
                }
            };

            CurrentLobby = await LobbyService.Instance.UpdateLobbyAsync(CurrentLobby.Id, options);
            OnLobbyUpdated?.Invoke(CurrentLobby);
        }
        catch (Exception e)
        {
            Debug.LogError($"[lobby] failed to update bot count {e.Message}");
        }
    }
}