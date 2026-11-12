using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    private const float ScatterPickSeconds = 15f;
    private const float ScatterResultWait = 2.6f;

    private class PendingScatter
    {
        public NetworkCard Card;
        public System.Action OnFinished;
        public List<ulong> Required = new List<ulong>();
        public Dictionary<ulong, NetworkCard> Picks = new Dictionary<ulong, NetworkCard>();
        public bool Executed;
    }

    private PendingScatter pendingScatter;

    public void BeginScatter(ulong askerId, NetworkCard card, System.Action onFinished)
    {
        if (!IsServer) return;

        var pending = new PendingScatter { Card = card, OnFinished = onFinished };

        foreach (ulong id in turnOrder)
        {
            if (playerHands.TryGetValue(id, out var hand) && hand.Count > 0) pending.Required.Add(id);
        }

        if (pending.Required.Count == 0)
        {
            Debug.Log("[scatter] nobody has cards, fizzling");
            DeckManager.Instance.Discard(card);
            onFinished?.Invoke();
            return;
        }

        pendingScatter = pending;
        Debug.Log($"<color=orange>[scatter] {GetPlayerName(askerId)} played Scatter - waiting for {pending.Required.Count} pick(s)</color>");

        foreach (ulong id in pending.Required)
        {
            OpenScatterPickClientRpc(GetPlayerName(GetLeftNeighbor(id)), SingleTarget(id));
        }

        StartCoroutine(ScatterTimeoutRoutine(pending));
    }

    private ulong GetLeftNeighbor(ulong playerId)
    {
        int index = turnOrder.IndexOf(playerId);
        return turnOrder[(index + 1) % turnOrder.Count];
    }

    [Rpc(SendTo.Server)]
    public void SubmitScatterPickServerRpc(NetworkCard card, RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;
        PendingScatter pending = pendingScatter;

        if (pending == null || pending.Executed) return;
        if (!pending.Required.Contains(senderId) || pending.Picks.ContainsKey(senderId)) return;

        if (!playerHands.TryGetValue(senderId, out var hand) || !hand.Contains(card))
        {
            SendNotice(senderId, "<color=red>That card isn't in your hand</color>");
            ReopenScatterPickClientRpc(SingleTarget(senderId));
            return;
        }

        pending.Picks[senderId] = card;
        Debug.Log($"[scatter] {GetPlayerName(senderId)} picked ({pending.Picks.Count}/{pending.Required.Count})");

        if (pending.Picks.Count >= pending.Required.Count) ExecuteScatter(pending);
    }

    private void ExecuteScatter(PendingScatter pending)
    {
        if (pending.Executed) return;
        pending.Executed = true;

        List<ulong> givers = new List<ulong>();
        List<ulong> receivers = new List<ulong>();
        List<NetworkCard> cards = new List<NetworkCard>();

        foreach (var pick in pending.Picks)
        {
            ulong giver = pick.Key;
            ulong receiver = GetLeftNeighbor(giver);
            givers.Add(giver);
            receivers.Add(receiver);
            cards.Add(pick.Value);
        }
        PlayScatterAnimationClientRpc(givers.ToArray(), receivers.ToArray(), cards.ToArray());
        StartCoroutine(FinishScatterAfterAnimation(pending));
    }

    [ClientRpc]
    private void PlayScatterAnimationClientRpc(ulong[] givers, ulong[] receivers, NetworkCard[] cards)
    {
        if (CardFlightManager.Instance == null) return;

        for (int i = 0; i < givers.Length; i++)
        {
            Vector3 startPos = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);
            Vector3 endPos = new Vector3(Screen.width / 2f, Screen.height / 2f, 0);

            if (givers[i] == NetworkManager.Singleton.LocalClientId)
                startPos = PlayerHandController.Instance != null ? PlayerHandController.Instance.GetHandCenterPosition() : startPos;
            else
            {
                var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(givers[i]) : null;
                if (island != null) startPos = island.transform.position;
            }

            if (receivers[i] == NetworkManager.Singleton.LocalClientId)
                endPos = PlayerHandController.Instance != null ? PlayerHandController.Instance.GetHandCenterPosition() : endPos;
            else
            {
                var island = BoardLayoutManager.Instance != null ? BoardLayoutManager.Instance.GetOpponentIsland(receivers[i]) : null;
                if (island != null) endPos = island.transform.position;
            }
            Sprite sprite = CardDatabase.Instance != null ? CardDatabase.Instance.GetCardSprite(cards[i].CardId) : null;
            CardFlightManager.Instance.FlyCard(startPos, endPos, sprite, null, 1.5f);
        }
    }

    private IEnumerator FinishScatterAfterAnimation(PendingScatter pending)
    {
        yield return new WaitForSeconds(1.8f);
        var receivedCard = new Dictionary<ulong, NetworkCard>();
        var receivedFrom = new Dictionary<ulong, ulong>();

        foreach (var pick in pending.Picks)
        {
            playerHands[pick.Key].Remove(pick.Value);
        }
        foreach (var pick in pending.Picks)
        {
            ulong giver = pick.Key;
            ulong receiver = GetLeftNeighbor(giver);

            playerHands[receiver].Add(pick.Value);
            receivedCard[receiver] = pick.Value;
            receivedFrom[receiver] = giver;
        }
        var affected = new HashSet<ulong>(pending.Picks.Keys);
        affected.UnionWith(receivedCard.Keys);
        foreach (ulong id in affected)
        {
            UpdateOpponentHandCountClientRpc(id, playerHands[id].Count);

            bool gave = pending.Picks.TryGetValue(id, out NetworkCard passed);
            bool got = receivedCard.TryGetValue(id, out NetworkCard received);
            ulong fromId = got ? receivedFrom[id] : 0;

            ScatterResultClientRpc(gave, gave ? passed : default, got, got ? received : default, fromId, SingleTarget(id));
        }
        Debug.Log("<color=orange>[scatter] cards passed</color>");
        StartCoroutine(FinishScatterRoutine(pending));
    }
    private IEnumerator FinishScatterRoutine(PendingScatter pending)
    {
        yield return new WaitForSeconds(ScatterResultWait);

        DeckManager.Instance.Discard(pending.Card);
        if (pendingScatter == pending) pendingScatter = null;
        pending.OnFinished?.Invoke();
    }

    private IEnumerator ScatterTimeoutRoutine(PendingScatter pending)
    {
        yield return new WaitForSeconds(ScatterPickSeconds);

        if (pending.Executed) yield break;

        foreach (ulong id in pending.Required)
        {
            if (pending.Picks.ContainsKey(id)) continue;
            if (!playerHands.TryGetValue(id, out var hand) || hand.Count == 0) continue;

            List<NetworkCard> safeChoices = hand.FindAll(c => c.Type != CardType.WhiskerBlower);
            List<NetworkCard> pool = safeChoices.Count > 0 ? safeChoices : hand;

            Debug.LogWarning($"[scatter] {GetPlayerName(id)} timed out - passing a random card");
            pending.Picks[id] = pool[Random.Range(0, pool.Count)];
        }

        ExecuteScatter(pending);
    }
    [ClientRpc]
    private void OpenScatterPickClientRpc(string neighborName, ClientRpcParams clientRpcParams = default)
    {
        PlayerHandController.Instance?.BeginScatterPick(neighborName);
    }

    [ClientRpc]
    private void ReopenScatterPickClientRpc(ClientRpcParams clientRpcParams = default)
    {
        PlayerHandController.Instance?.ReopenScatterPick();
    }

    [ClientRpc]
    private void ScatterResultClientRpc(bool gave, NetworkCard passed, bool got, NetworkCard received, ulong fromId, ClientRpcParams clientRpcParams = default)
    {
        PlayerHandController hand = PlayerHandController.Instance;
        hand?.EndScatterPick();
        hand?.ApplyScatterResult(gave, passed, got, received);

        if (!got) return;

        string fromName = GetPlayerName(fromId);
        string message = $"{fromName} passed you {received.CardId}";
        Color glow = new Color(1f, 0.6f, 0.2f, 1f);

        if (CardSpotlightController.Instance != null)
        {
            CardSpotlightController.Instance.PlaySpotlight(received.CardId, fromId, message, glow, 3f);
        }
        else
        {
            var spotlight = FindAnyObjectByType<CardSpotlightController>(FindObjectsInactive.Include);
            spotlight?.PlaySpotlight(received.CardId, fromId, message, glow, 3f);
        }
    }
}