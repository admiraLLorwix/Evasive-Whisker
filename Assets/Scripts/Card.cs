using UnityEngine;

namespace ClowderStudio.EvasiveWhisker
{
    [CreateAssetMenu(fileName = "New Card", menuName = "Card")]
    public class Card : ScriptableObject
    {


		[SerializeField] public string cardName;
		[SerializeField] public CardType type;
        [SerializeField] public int size = 1;
        [SerializeField] public string effectText;
        [SerializeField] public string flavorText;

        public enum CardType { WhiskerBlower, Custody, Ability, Trap, Counterplay }

    }

}

