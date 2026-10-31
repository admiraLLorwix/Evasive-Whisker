public interface IDetectiveCatTrait
{
    bool CanActivate(ulong playerId, int currentRound);
    void Activate(ulong playerId, ulong targetId = 0);
}