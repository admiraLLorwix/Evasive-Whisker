public enum CatTiming { Passive, DrawPhase, ActionPhase, Reactive }
public enum RoundParity { Any, Odd, Even }

public static class DetectiveCatRules
{
    public static CatTiming Timing(DetectiveCatType cat)
    {
        switch (cat)
        {
            case DetectiveCatType.HoarderMolly: return CatTiming.Passive;
            case DetectiveCatType.InspectorBebang: return CatTiming.DrawPhase;
            case DetectiveCatType.DumpsterDiverShimo: return CatTiming.DrawPhase;
            case DetectiveCatType.DetectiveNido: return CatTiming.ActionPhase;
            default: return CatTiming.Reactive;
        }
    }

    public static RoundParity Parity(DetectiveCatType cat)
    {
        switch (cat)
        {
            case DetectiveCatType.InspectorBebang: return RoundParity.Odd;
            case DetectiveCatType.BombSquadWhamos: return RoundParity.Odd;
            case DetectiveCatType.DetectiveNido: return RoundParity.Even;
            case DetectiveCatType.DumpsterDiverShimo: return RoundParity.Even;
            default: return RoundParity.Any;
        }
    }

    public static int CooldownRounds(DetectiveCatType cat)
    {
        switch (cat)
        {
            case DetectiveCatType.DecoyMokang: return 2;
            case DetectiveCatType.AgentPutol: return 1;
            case DetectiveCatType.SleightOfPawShadow: return 2;
            default: return 0;
        }
    }

    // lastUsedRound: 0 = never used
    public static bool IsAvailable(DetectiveCatType cat, int round, int lastUsedRound) =>
        UnavailableReason(cat, round, lastUsedRound) == null;

    // null = available
    public static string UnavailableReason(DetectiveCatType cat, int round, int lastUsedRound)
    {
        if (Timing(cat) == CatTiming.Passive) return "This cat is a passive ability";

        RoundParity parity = Parity(cat);
        if (parity == RoundParity.Odd && round % 2 == 0) return "Only usable in odd rounds";
        if (parity == RoundParity.Even && round % 2 == 1) return "Only usable in even rounds";

        int cooldown = CooldownRounds(cat);
        if (cooldown > 0)
        {
            if (lastUsedRound > 0 && round <= lastUsedRound + cooldown)
            {
                int wait = lastUsedRound + cooldown + 1 - round;
                return $"On cooldown - ready in {wait} round{(wait == 1 ? "" : "s")}";
            }
            return null;
        }

        if (lastUsedRound == round) return "Already used this round";
        return null;
    }

    public static string DisplayName(DetectiveCatType cat)
    {
        switch (cat)
        {
            case DetectiveCatType.HoarderMolly: return "Hoarder Molly";
            case DetectiveCatType.InspectorBebang: return "Inspector Bebang";
            case DetectiveCatType.DetectiveNido: return "Detective Nido";
            case DetectiveCatType.DecoyMokang: return "Decoy Mokang";
            case DetectiveCatType.AgentPutol: return "Agent Putol";
            case DetectiveCatType.SleightOfPawShadow: return "Sleight of Paw Shadow";
            case DetectiveCatType.BombSquadWhamos: return "Bomb Squad Whamos";
            case DetectiveCatType.DumpsterDiverShimo: return "Dumpster Diver Shimo";
            default: return cat.ToString();
        }
    }
    public static string PhaseHint(DetectiveCatType cat)
    {
        switch (Timing(cat))
        {
            case CatTiming.Passive: return "Passive";
            case CatTiming.DrawPhase: return "Use in your draw phase";
            case CatTiming.ActionPhase: return "Use in your action phase";
            default: return "Reacts automatically";
        }
    }
}