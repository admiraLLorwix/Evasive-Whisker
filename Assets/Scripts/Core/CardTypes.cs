using System;

namespace EvasiveWhisker.Core
{
    /// <summary>
    /// File: Assets/Scripts/Core/CardTypes.cs
    /// Purpose: Defines the card taxonomy and the immutable card data record.
    /// Runs on: Both (pure data — no networking dependency by itself).
    /// Network responsibility: None directly. Card identity is the payload carried
    /// by targeted RPCs (private hand delivery) and by public broadcasts (plays,
    /// trap-card steal reveals, match-end full reveal). This file just defines
    /// the shape of that payload so every other script agrees on it.
    /// </summary>

    /// <summary>
    /// The category of a card. Extend this enum as the game's card set grows —
    /// gameplay-specific subtypes (e.g. different trap variants) can be layered
    /// on top of CardId lookups rather than expanding this enum indefinitely.
    /// </summary>
    public enum CardType
    {
        Normal = 0,
        Trap = 1,
        WhiskerBlower = 2
    }

    /// <summary>
    /// Immutable identity of a single card. CardId is the stable identifier used
    /// everywhere a card needs to be referenced without exposing its full data
    /// (e.g. RequestPlayCard(cardId), RequestSteal target validation).
    ///
    /// Deliberately a plain struct, not a NetworkBehaviour/INetworkStruct:
    /// individual Card instances are never networked directly. Only aggregate,
    /// non-revealing information about cards (counts) is synced as networked
    /// state (see MatchState). Actual Card values travel only via:
    ///   - targeted RPC to the owning client (hand contents), or
    ///   - public broadcast at the moment identity is meant to become known
    ///     (play, trap-card steal reveal, match-end reveal).
    /// </summary>
    [Serializable]
    public readonly struct Card : IEquatable<Card>
    {
        public readonly int CardId;      // Stable unique id, assigned at deck construction.
        public readonly CardType Type;
        public readonly int DefinitionId; // Index into a design-time card definition table (art, name, effect text, etc.) — kept separate from CardType so multiple "Normal" cards can differ in flavor without new enum values.

        public Card(int cardId, CardType type, int definitionId)
        {
            CardId = cardId;
            Type = type;
            DefinitionId = definitionId;
        }

        public bool IsWhiskerBlower => Type == CardType.WhiskerBlower;
        public bool IsTrap => Type == CardType.Trap;

        public bool Equals(Card other) => CardId == other.CardId;
        public override bool Equals(object obj) => obj is Card other && Equals(other);
        public override int GetHashCode() => CardId;

        public override string ToString() => $"Card(id={CardId}, type={Type}, def={DefinitionId})";
    }
}
