using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

public class BoardLayoutManager : MonoBehaviour
{
    public static BoardLayoutManager Instance { get; private set; }

    [Header("anchors")]
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

        int opponentCount = orderedOpponents.Count;
        if (opponentCount == 0 || opponentIslandPrefab == null) return;

        foreach (var pair in spawnedOpponentIslands)
        {
            if (pair.Value != null) Destroy(pair.Value.gameObject);
        }
        spawnedOpponentIslands.Clear();

        int[] targetAnchorIndices;

        switch (opponentCount)
        {
            case 1:
                targetAnchorIndices = new int[] { 2 };
                break;
            case 2:
                targetAnchorIndices = new int[] { 1, 3 };
                break;
            case 3:
                targetAnchorIndices = new int[] { 1, 2, 3 };
                break;
            case 4:
                targetAnchorIndices = new int[] { 0, 1, 3, 4 };
                break;
            default:
                targetAnchorIndices = new int[] { 0, 1, 2, 3, 4 };
                break;
        }

        for (int i = 0; i < opponentCount; i++)
        {
            int anchorIdx = targetAnchorIndices[i];
            if (anchorIdx >= opponentIslandAnchorPoints.Length || opponentIslandAnchorPoints[anchorIdx] == null) continue;

            ulong opponentId = orderedOpponents[i];
            Transform anchor = opponentIslandAnchorPoints[anchorIdx];

            GameObject islandObj = Instantiate(opponentIslandPrefab, anchor);
            islandObj.transform.localPosition = Vector3.zero;

            PlayerIslandUI islandUI = islandObj.GetComponent<PlayerIslandUI>();
            if (islandUI == null) continue;

            spawnedOpponentIslands[opponentId] = islandUI;

            Sprite islandSprite = (islandSprites != null && islandSprites.Length > 0)
                ? islandSprites[i % islandSprites.Length]
                : null;

            string realName = (playerNamesMap != null && playerNamesMap.ContainsKey(opponentId))
                ? playerNamesMap[opponentId]
                : $"Detective_{Random.Range(100, 999)}";

            islandUI.Initialize(opponentId, realName, islandSprite);
        }

        Debug.Log($"[BoardLayout] placed {opponentCount} isladns on: {string.Join(", ", targetAnchorIndices)}");
    }

    public PlayerIslandUI GetOpponentIsland(ulong clientId)
    {
        return spawnedOpponentIslands.TryGetValue(clientId, out var island) ? island : null;
    }

    public void HighlightActivePlayer(ulong activePlayerId)
    {
        ulong localId = NetworkManager.Singleton.LocalClientId;

        foreach (var pair in spawnedOpponentIslands)
        {
            if (pair.Value != null)
            {
                bool isTargetActive = (pair.Key == activePlayerId) && (activePlayerId != localId);
                pair.Value.SetActiveTurn(isActiveTurn: isTargetActive);
            }
        }
    }





    //island debugging

    [ContextMenu("layout: 1 opp")]
    public void TestLayout1() => SpawnDebugIslands(1);

    [ContextMenu("layout: 2 opps")]
    public void TestLayout2() => SpawnDebugIslands(2);

    [ContextMenu("layout: 3 opps")]
    public void TestLayout3() => SpawnDebugIslands(3);

    [ContextMenu("layout: 4 opps")]
    public void TestLayout4() => SpawnDebugIslands(4);

    [ContextMenu("layout: 5 opps")]
    public void TestLayout5() => SpawnDebugIslands(5);

    [ContextMenu("clear")]
    public void ClearDebugIslands()
    {
        foreach (var pair in spawnedOpponentIslands)
        {
            if (pair.Value != null)
            {
                DestroyImmediate(pair.Value.gameObject);
            }
        }
        spawnedOpponentIslands.Clear();
        Debug.Log("[boardlayout] clear islands.");
    }

    private void SpawnDebugIslands(int opponentCount)
    {
        ClearDebugIslands();

        int[] targetAnchorIndices;
        switch (opponentCount)
        {
            case 1: targetAnchorIndices = new int[] { 2 }; break;
            case 2: targetAnchorIndices = new int[] { 1, 3 }; break;
            case 3: targetAnchorIndices = new int[] { 1, 2, 3 }; break;
            case 4: targetAnchorIndices = new int[] { 0, 1, 3, 4 }; break;
            default: targetAnchorIndices = new int[] { 0, 1, 2, 3, 4 }; break;
        }

        for (int i = 0; i < opponentCount; i++)
        {
            int anchorIdx = targetAnchorIndices[i];
            if (anchorIdx >= opponentIslandAnchorPoints.Length || opponentIslandAnchorPoints[anchorIdx] == null) continue;

            Transform anchor = opponentIslandAnchorPoints[anchorIdx];
            GameObject islandObj = Instantiate(opponentIslandPrefab, anchor);
            islandObj.transform.localPosition = Vector3.zero;

            PlayerIslandUI islandUI = islandObj.GetComponent<PlayerIslandUI>();
            if (islandUI == null) continue;

            ulong mockId = (ulong)(i + 1);
            spawnedOpponentIslands[mockId] = islandUI;

            Sprite islandSprite = (islandSprites != null && islandSprites.Length > 0)
                ? islandSprites[i % islandSprites.Length]
                : null;

            islandUI.Initialize(mockId, $"test opp {i + 1}", islandSprite);
        }

        Debug.Log($"[boardlayout] spawned {opponentCount} test islands");
    }
}