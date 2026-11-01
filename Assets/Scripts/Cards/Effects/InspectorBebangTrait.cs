using EvasiveWhisker.Cards;
public class InspectorBebangTrait : IDetectiveCatTrait
{
    public bool CanActivate(ulong playerId, int currentRound) =>
        GameManager.Instance.IsCatAvailable(playerId, DetectiveCatType.InspectorBebang, currentRound);

    public void Activate(ulong playerId, ulong targetId = 0) => GameManager.Instance.BeginBebang(playerId);
}