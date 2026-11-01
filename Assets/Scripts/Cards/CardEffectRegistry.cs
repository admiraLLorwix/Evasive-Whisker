using UnityEngine;
using System.Collections.Generic;
using EvasiveWhisker.Cards;

public static class CardEffectRegistry
{
    private static readonly Dictionary<string, IAbilityEffect> abilities = new Dictionary<string, IAbilityEffect>();
    private static readonly Dictionary<string, IInteractiveAbilityEffect> interactiveAbilities = new Dictionary<string, IInteractiveAbilityEffect>();
    private static readonly Dictionary<string, ITrapEffect> traps = new Dictionary<string, ITrapEffect>();
    private static readonly Dictionary<string, ICounterplayEffect> counterplays = new Dictionary<string, ICounterplayEffect>();
    private static readonly Dictionary<DetectiveCatType, IDetectiveCatTrait> detectiveTraits = new Dictionary<DetectiveCatType, IDetectiveCatTrait>();

    private static readonly HashSet<string> targetedAbilities = new HashSet<string>
    {
        "Declaw", "BombasticCatEyes", "CatInstinct"
    };

    private static readonly HashSet<string> slotAbilities = new HashSet<string>
    {
        "NineLives", "Zoomies"
    };

    public static void RegisterAbility(string cardId, IAbilityEffect effect, bool requiresTarget = false)
    {
        abilities[cardId] = effect;
        if (requiresTarget) targetedAbilities.Add(cardId);
    }

    public static void RegisterInteractiveAbility(string cardId, IInteractiveAbilityEffect effect, bool requiresTarget = false)
    {
        interactiveAbilities[cardId] = effect;
        if (requiresTarget) targetedAbilities.Add(cardId);
    }

    public static void RegisterTrap(string trapId, ITrapEffect effect) => traps[trapId] = effect;
    public static void RegisterCounterplay(string cardId, ICounterplayEffect effect) => counterplays[cardId] = effect;
    public static void RegisterDetective(DetectiveCatType catType, IDetectiveCatTrait trait) => detectiveTraits[catType] = trait;

    public static bool HasAbility(string cardId) => abilities.ContainsKey(cardId) || interactiveAbilities.ContainsKey(cardId);
    public static bool RequiresTarget(string cardId) => targetedAbilities.Contains(cardId);
    public static bool OccupiesSlot(string cardId) => slotAbilities.Contains(cardId);

    public static bool TriggerAbility(string cardId, ulong senderId, ulong targetId, NetworkCard card, System.Action onInteractiveFinished)
    {
        if (abilities.TryGetValue(cardId, out var effect))
        {
            effect.Execute(senderId, targetId, card);
            return true;
        }

        if (interactiveAbilities.TryGetValue(cardId, out var interactive))
        {
            interactive.Begin(senderId, targetId, card, onInteractiveFinished);
            return false;
        }

        Debug.LogWarning($"[registry-ability] nothing found: '{cardId}', discarding the card");
        DeckManager.Instance.Discard(card);
        return true;
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