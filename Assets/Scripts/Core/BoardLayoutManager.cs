using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BoardLayoutManager : MonoBehaviour
{
    public static BoardLayoutManager Instance { get; private set; }

    [Header("pos")]
    [SerializeField] private Transform centerIsland;
    [SerializeField] private Transform localPlayerZone;
    [SerializeField] private Transform[] opponentIslandAnchorPoints;

    [Header("Assets")]
    [SerializeField] private GameObject opponentIslandPrefab;
    [SerializeField] private Texture2D[] islandImages;

    private Dictionary<ulong, OpponentController> spawnedOpponentIslands = new Dictionary<ulong, OpponentController>();

    private void Awake()
    {
        Instance = this;
    }

    public void SetupPlayerIslands(List<ulong> allPlayerClientIds, Dictionary<ulong, string> playerNamesMap)
    {
        ulong localClientId = NetworkManager.Singleton.LocalClientId;

        int localIndex = allPlayerClientIds.IndexOf(localClientId);
        List<ulong> orderedOpponents = new List<ulong>();

        for (int i = 1; i < allPlayerClientIds.Count; i++)
        {
            int nextIndex = (localIndex + i) % allPlayerClientIds.Count;
            orderedOpponents.Add(allPlayerClientIds[nextIndex]);
        }

        if (opponentIslandPrefab == null)
        {
            Debug.LogError("[BoardLayoutManager] !OpponentIslandPrefab");
            return;
        }

        for (int i = 0; i < orderedOpponents.Count; i++)
        {
            if (opponentIslandAnchorPoints == null
                || i >= opponentIslandAnchorPoints.Length
                || opponentIslandAnchorPoints[i] == null) continue;

            ulong opponentId = orderedOpponents[i];
            Transform anchor = opponentIslandAnchorPoints[i];

            // instantiates prefab based on opponentIslandPrefab specified in board manager
            GameObject islandObj = Instantiate(opponentIslandPrefab, anchor.position, anchor.rotation, anchor);
            OpponentController islandUI = islandObj.GetComponent<OpponentController>();

            if (islandUI == null) continue;

            spawnedOpponentIslands[opponentId] = islandUI;

            Texture2D islandImage = (islandImages != null && islandImages.Length > 0)
                ? islandImages[i % islandImages.Length]
                : null;

            string playerName = (playerNamesMap != null && playerNamesMap.ContainsKey(opponentId))
                ? playerNamesMap[opponentId]
                : $"Detective #{opponentId}";

            islandUI.Initialize(opponentId, playerName, islandImage);
        }
    }

    public OpponentController GetOpponentIsland(ulong clientId)
    {
        return spawnedOpponentIslands.TryGetValue(clientId, out var island) ? island : null;
    }
}