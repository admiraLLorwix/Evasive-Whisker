using UnityEngine;

namespace EvasiveWhisker.Cards
{
    public enum CardType
    {
        Ability,
        Counterplay,
        Trap,
        WhiskerBlower,
        Custody,
        DetectiveCat
    }

    public enum TrapEffect
    {
        None,
        Swipe,
        RobinPaw,
        Catnip,
        CatFlash
    }

    [CreateAssetMenu(fileName = "SO_Card_NewCard", menuName = "Evasive Whisker/Card Data")]
    public class Card : ScriptableObject
    {
        [Header("Card Header")]
        public string cardID;
        public string cardName;
        public CardType cardType;



        [Header("Card Art")]
        public Sprite cardIllustration;
        public Sprite cardFrame;



        [Header("Card Use Case")]
        public string cardUseCase;



        [Header("Card Description")]
        [TextArea(2, 4)]
        public string effectDescription;



        [Header("Card Rules")]
        public bool canBeDiscarded = true;
        public bool occupiesAbilitySlot = false;



        [Header("Trap Parameters")]
        public TrapEffect trapEffect = TrapEffect.None;



        [Header("Detective Cat Trait")]
        public bool isOddRoundOnly = false;
        public bool isEvenRoundOnly = false;
        public int traitCooldownRounds = 0;
    }
}