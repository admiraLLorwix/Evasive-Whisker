using UnityEngine;
using System.Collections.Generic;
using EvasiveWhisker.Cards;

public static class CardEffectRegistry
{
    private static readonly Dictionary<string, IAbilityEffect> abilities = new Dictionary<string, IAbilityEffect>();
    private static readonly Dictionary<string, IInteractiveAbilityEffect> interactiveAbilities = new Dictionary<string, IInteractiveAbilityEffect>();
    private static readonly Dictionary<string, ITrapEffect> traps = new Dictionary<string, ITrapEffect>();
    private static readonly Dictionary<string, IInteractiveTrapEffect> interactiveTraps = new Dictionary<string, IInteractiveTrapEffect>();
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
    public static void RegisterInteractiveTrap(string trapId, IInteractiveTrapEffect effect) => interactiveTraps[trapId] = effect;
    public static void RegisterCounterplay(string cardId, ICounterplayEffect effect) => counterplays[cardId] = effect;
    public static void RegisterDetective(DetectiveCatType catType, IDetectiveCatTrait trait) => detectiveTraits[catType] = trait;
    public static IDetectiveCatTrait GetDetective(DetectiveCatType catType) =>
    detectiveTraits.TryGetValue(catType, out var trait) ? trait : null;
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

    public static bool TriggerTrap(string trapId, ulong thiefId, ulong victimId, NetworkCard trapCard, System.Action onInteractiveFinished)
    {
        if (traps.TryGetValue(trapId, out var effect))
        {
            effect.Execute(thiefId, victimId, trapCard);
            return true;
        }
        if (interactiveTraps.TryGetValue(trapId, out var interactive))
        {
            interactive.Begin(thiefId, victimId, trapCard, onInteractiveFinished);
            return false;
        }
        Debug.LogWarning($"[registry-trap] nothing for: '{trapId}', discarding the card");
        DeckManager.Instance.Discard(trapCard);
        return true;
    }
    public static bool CanCounter(string counterCardId, string actionType) =>
        counterplays.TryGetValue(counterCardId, out var effect) && effect.CanCounter(actionType);

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