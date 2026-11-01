using EvasiveWhisker.Cards;
public class DumpsterDiverShimoTrait : IDetectiveCatTrait
{
    public bool CanActivate(ulong playerId, int currentRound) =>
        GameManager.Instance.IsCatAvailable(playerId, DetectiveCatType.DumpsterDiverShimo, currentRound);

    public void Activate(ulong playerId, ulong targetId = 0) => GameManager.Instance.BeginShimo(playerId);
}