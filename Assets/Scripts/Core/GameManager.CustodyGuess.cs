using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using EvasiveWhisker.Cards;

public partial class GameManager
{
    public const int CustodyCardsRequired = 3;
    private const float GuessRevealSeconds = 3.5f;
    private class GuessImmunity
    {
        public ulong guesserId;
        public bool guesserTurnStarted;
    }
    private readonly Dictionary<ulong, GuessImmunity> guessImmunity = new Dictionary<ulong, GuessImmunity>();

    public bool IsGuessImmune(ulong playerId) => guessImmunity.ContainsKey(playerId);

    private string GuessImmuneMessage(ulong playerId) =>
        $"{GetPlayerName(playerId)} is protected after a failed accusation";

    private int CountCustodyCards(ulong playerId) =>
        playerHands.TryGetValue(playerId, out var hand) ? hand.FindAll(c => c.Type == CardType.Custody).Count : 0;
    private void TickGuessImmunity(ulong startingPlayerId)
    {
        if (guessImmunity.Count == 0) return;

        List<ulong> expired = null;
        foreach (var pair in guessImmunity)
        {
            if (pair.Value.guesserId == startingPlayerId) pair.Value.guesserTurnStarted = true;
            else if (pair.Value.guesserTurnStarted)
            {
                if (expired == null) expired = new List<ulong>();
                expired.Add(pair.Key);
            }
        }

        if (expired == null) return;
        foreach (ulong id in expired)
        {
            guessImmunity.Remove(id);
            SetGuessImmunityClientRpc(id, false);
            Debug.Log($"[custody] {GetPlayerName(id)}'s accusation immunity ended");
        }
    }

    private void GrantGuessImmunity(ulong accusedId, ulong guesserId)
    {
        guessImmunity[accusedId] = new GuessImmunity { guesserId = guesserId, guesserTurnStarted = false };
        SetGuessImmunityClientRpc(accusedId, true);
    }


    private void ConsumeCustodyCards(ulong guesserId, int count)
    {
        List<NetworkCard> hand = playerHands[guesserId];
        var consumed = new List<NetworkCard>();

        foreach (NetworkCard card in hand.ToArray())
        {
            if (consumed.Count >= count) break;
            if (card.Type != CardType.Custody) continue;
            consumed.Add(card);
            hand.Remove(card);
        }

        RemoveCardsFromHandClientRpc(consumed.ToArray(), SingleTarget(guesserId));
        foreach (NetworkCard card in consumed) DeckManager.Instance.Discard(card);
        UpdateOpponentHandCountClientRpc(guesserId, hand.Count);
    }

    private IEnumerator ResolveCorrectGuessRoutine(ulong guesserId, ulong accusedId)
    {
        string accusedName = GetPlayerName(accusedId);
        string winnerName = GetPlayerName(guesserId);

        RevealAccusedHandClientRpc(accusedName, playerHands[accusedId].ToArray());
        yield return new WaitForSeconds(GuessRevealSeconds + 0.5f);

        AnnounceWinnerClientRpc(guesserId, $"{winnerName} correctly guessed the Custodian with 3 Custody cards");
    }
    private IEnumerator ResolveWrongGuessRoutine(ulong guesserId, ulong accusedId)
    {
        string accusedName = GetPlayerName(accusedId);
        string guesserName = GetPlayerName(guesserId);
        Debug.Log($"[custody] {guesserName} wrongly accused {accusedName}");

        ConsumeCustodyCards(guesserId, CustodyCardsRequired);
        GrantGuessImmunity(accusedId, guesserId);

        CloseThiefStealUIClientRpc(SingleTarget(guesserId));
        ShowInnocentPanelClientRpc(accusedName, guesserName);

        yield return new WaitForSeconds(GuessRevealSeconds);

        AfterAction(guesserId, TurnActionKind.Guess);
    }
    [ClientRpc]
    private void RevealAccusedHandClientRpc(string accusedName, NetworkCard[] hand)
    {
        string title = $"<color=yellow>{accusedName}</color> was holding the WhiskerBlower!";
        if (HandRevealUI.Instance != null) HandRevealUI.Instance.ShowTimed(title, hand, GuessRevealSeconds);
    }

    [ClientRpc]
    private void ShowInnocentPanelClientRpc(string accusedName, string guesserName)
    {
        string title = $"<color=green>{accusedName}</color> is innocent!\n<size=60%>{guesserName}'s accusation was wrong</size>";
        if (HandRevealUI.Instance != null) HandRevealUI.Instance.ShowTimed(title, null, GuessRevealSeconds);
    }

    [ClientRpc]
    private void RemoveCardsFromHandClientRpc(NetworkCard[] cards, ClientRpcParams clientRpcParams = default)
    {
        if (PlayerHandController.Instance == null) return;
        foreach (NetworkCard card in cards) PlayerHandController.Instance.RemoveCard(card);
    }

    [ClientRpc]
    private void SetGuessImmunityClientRpc(ulong playerId, bool immune)
    {
        if (BoardLayoutManager.Instance == null) return;
        PlayerIslandUI island = BoardLayoutManager.Instance.GetOpponentIsland(playerId);
        if (island != null) island.SetGuessImmune(immune);
    }
}