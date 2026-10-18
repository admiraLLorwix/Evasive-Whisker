using System;
using Unity.Netcode;
using EvasiveWhisker.Cards;

namespace EvasiveWhisker.Core
{
    /// <summary>
    /// One physical card in a match, as the networking layer sees it. Deliberately NOT named
    /// "Card": EvasiveWhisker.Cards.Card is the ScriptableObject definition, and a struct with
    /// that name inside this namespace would hijack DeckManager/PlayerHand's references to it.
    ///
    /// Read-only from the outside: the fields are private and only INetworkSerializable's
    /// NetworkSerialize writes them (that is how Netcode fills the struct when receiving an RPC).
    ///
    ///  - Id           unique per physical card in this match (two copies of one card have different Ids)
    ///  - DefinitionId which card definition (ScriptableObject) this is a copy of
    ///  - Type         copied from the definition so the host can check Trap / WhiskerBlower
    ///                 without loading ScriptableObjects
    /// </summary>
    public struct CardInstance : IEquatable<CardInstance>, INetworkSerializable
    {
        private int _id;
        private int _definitionId;
        private CardType _type;

        public int Id => _id;
        public int DefinitionId => _definitionId;
        public CardType Type => _type;

        public CardInstance(int id, int definitionId, CardType type)
        {
            _id = id;
            _definitionId = definitionId;
            _type = type;
        }

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref _id);
            serializer.SerializeValue(ref _definitionId);
            serializer.SerializeValue(ref _type);
        }

        public bool Equals(CardInstance other) => _id == other._id;
        public override bool Equals(object obj) => obj is CardInstance other && Equals(other);
        public override int GetHashCode() => _id;
        public override string ToString() => $"Card#{_id} (def {_definitionId}, {_type})";
    }
}
