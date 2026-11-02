using EvasiveWhisker.Cards;

public class DetectiveNidoTrait : IDetectiveCatTrait
{
    public bool CanActivate(ulong playerId, int currentRound) =>
        GameManager.Instance.IsCatAvailable(playerId, DetectiveCatType.DetectiveNido, currentRound);

    public void Activate(ulong playerId, ulong targetId = 0) => GameManager.Instance.BeginNido(playerId);
}