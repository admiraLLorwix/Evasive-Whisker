using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BoardLayoutManager : MonoBehaviour
{
    public static BoardLayoutManager Instance { get; private set; }

    [Header("pos")]
    [SerializeField] private Transform centerIsland;
    [SerializeField] private Transform localPlayerSlot;
    [SerializeField] private Transform[] opponentIslandAnchorPoints;

    [Header("assets")]
    [SerializeField] private GameObject opponentIslandPrefab;
    [SerializeField] private Sprite[] islandSprites;

    private Dictionary<ulong, PlayerIslandUI> spawnedOpponentIslands = new Dictionary<ulong, PlayerIslandUI>();

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
            if (opponentIslandAnchorPoints == null || i >= opponentIslandAnchorPoints.Length || opponentIslandAnchorPoints[i] == null)
            {
                continue;
            }

            ulong opponentId = orderedOpponents[i];
            Transform anchor = opponentIslandAnchorPoints[i];

            GameObject islandObj = Instantiate(opponentIslandPrefab, anchor.position, anchor.rotation, anchor);
            PlayerIslandUI islandUI = islandObj.GetComponent<PlayerIslandUI>();

            if (islandUI == null) continue;

            spawnedOpponentIslands[opponentId] = islandUI;

            Sprite islandSprite = (islandSprites != null && islandSprites.Length > 0)
                ? islandSprites[i % islandSprites.Length]
                : null;

            string realName = (playerNamesMap != null && playerNamesMap.ContainsKey(opponentId))
                ? playerNamesMap[opponentId]
                : $"Detective #{opponentId}";

            islandUI.Initialize(opponentId, realName, islandSprite);
        }
    }

    public PlayerIslandUI GetOpponentIsland(ulong clientId)
    {
        return spawnedOpponentIslands.TryGetValue(clientId, out var island) ? island : null;
    }
}