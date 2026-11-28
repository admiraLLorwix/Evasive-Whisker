using UnityEngine;
using EvasiveWhisker.Cards;

public class DiscardPileClick : MonoBehaviour
{
    public void OnDiscardPileClicked()
    {
        if (DiscardPile2D.Instance != null && CardGalleryController.Instance != null)
        {
            var cards = DiscardPile2D.Instance.GetDiscardPile();
            CardGalleryController.Instance.ShowDynamicCards(cards, "Discard Pile");
        }
    }
}