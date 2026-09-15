using System;
using Fusion;

namespace EvasiveWhisker.Core
{
    /// <summary>
    /// The three card categories that drive visibility/reveal rules.
    /// Card *abilities* are out of scope for the networking layer — this enum only
    /// exists so host-side logic knows when a reveal trigger applies
    /// (e.g. stealing a Trap card, or the Whisker Blower's special handling).
    /// </summary>
    public enum CardType
    {
        Normal,
        Trap,
        WhiskerBlower
    }

    /// <summary>
    /// Immutable card instance. Id is unique per physical card in the match's full pool;
    /// DefinitionId maps to whatever card-art/definition table the client uses to render it
    /// (multiple physical cards can share a DefinitionId, e.g. several copies of the same
    /// Normal card).
    /// </summary>

    public struct Card : INetworkStruct, IEquatable<Card>
    {
        public int Id;
        public int DefinitionId;
        public CardType Type;

        public Card(int id, int definitionId, CardType type)
        {
            Id = id;
            DefinitionId = definitionId;
            Type = type;
        }

        public bool Equals(Card other) => Id == other.Id;

        public override bool Equals(object obj) =>
            obj is Card other && Equals(other);

        public override int GetHashCode() => Id;

        public static bool operator ==(Card a, Card b) =>
            a.Equals(b);

        public static bool operator !=(Card a, Card b) =>
            !a.Equals(b);

        public override string ToString() =>
            $"Card(Id={Id}, Def={DefinitionId}, Type={Type})";
    }
}
