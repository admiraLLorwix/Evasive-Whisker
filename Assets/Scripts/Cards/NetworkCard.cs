using System;
using Unity.Collections;
using Unity.Netcode;
using EvasiveWhisker.Cards;

public enum DetectiveCatType
{
    HoarderMolly,
    InspectorBebang,
    DetectiveNido,
    DecoyMokang,
    AgentPutol,
    SleightOfPawShadow,
    BombSquadWhamos,
    DumpsterDiverShimo
}

[System.Serializable]
public struct NetworkCard : INetworkSerializable, IEquatable<NetworkCard>
{
    public FixedString32Bytes CardID;
    public CardType Type;

    public string CardId => CardID.ToString();

    public NetworkCard(string cardId, CardType type)
    {
        CardID = new FixedString32Bytes(cardId);
        Type = type;
    }

    public NetworkCard(int cardId, CardType type)
    {
        CardID = new FixedString32Bytes(cardId.ToString());
        Type = type;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref CardID);
        serializer.SerializeValue(ref Type);
    }

    public bool Equals(NetworkCard other) => CardID.Equals(other.CardID);

    public Card GetScriptableObject()
    {
        return CardDatabase.Instance != null ? CardDatabase.Instance.GetCard(CardID.ToString()) : null;
    }
}