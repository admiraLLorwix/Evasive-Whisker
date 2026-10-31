using UnityEngine;
using System.Collections.Generic;
using EvasiveWhisker.Cards;

public static class CardEffectRegistry
{
    private static readonly Dictionary<string, IAbilityEffect> abilities = new Dictionary<string, IAbilityEffect>();
    private static readonly Dictionary<string, ITrapEffect> traps = new Dictionary<string, ITrapEffect>();
    private static readonly Dictionary<string, ICounterplayEffect> counterplays = new Dictionary<string, ICounterplayEffect>();
    private static readonly Dictionary<DetectiveCatType, IDetectiveCatTrait> detectiveTraits = new Dictionary<DetectiveCatType, IDetectiveCatTrait>();

    public static void RegisterAbility(string cardId, IAbilityEffect effect) => abilities[cardId] = effect;
    public static void RegisterTrap(string trapId, ITrapEffect effect) => traps[trapId] = effect;
    public static void RegisterCounterplay(string cardId, ICounterplayEffect effect) => counterplays[cardId] = effect;
    public static void RegisterDetective(DetectiveCatType catType, IDetectiveCatTrait trait) => detectiveTraits[catType] = trait;

    public static void TriggerAbility(string cardId, ulong senderId, ulong targetId, NetworkCard card)
    {
        if (abilities.TryGetValue(cardId, out var effect))
        {
            effect.Execute(senderId, targetId, card);
        }
        else
        {
            Debug.LogWarning($"[registry-ability] nothing found: '{cardId}'");
        }
    }

    public static void TriggerTrap(string trapId, ulong thiefId, ulong victimId, NetworkCard trapCard)
    {
        if (traps.TryGetValue(trapId, out var effect))
        {
            effect.Execute(thiefId, victimId, trapCard);
        }
        else
        {
            Debug.LogWarning($"[registry-trap] nothing for: '{trapId}'");
        }
    }

    public static bool TryCounter(string counterCardId, string actionType, ulong defenderId, ulong attackerId, NetworkCard card)
    {
        if (counterplays.TryGetValue(counterCardId, out var effect))
        {
            if (effect.CanCounter(actionType))
            {
                effect.Execute(defenderId, attackerId, card);
                return true;
            }
        }
        return false;
    }
}