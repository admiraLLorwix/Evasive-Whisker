using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using EvasiveWhisker.Cards;

public class PlayerIslandUI : MonoBehaviour
{
    [Header("vis")]
    [SerializeField] private Image islandBackgroundImage;
    [SerializeField] private TMP_Text opponentNameText;
    [SerializeField] private Image detectiveProfileImage;
    [SerializeField] private Image activeAbilityCardSlot;

    [SerializeField] private Transform handCardsContainer;
    [SerializeField] private GameObject cardBackPrefab;
    [SerializeField] private TMP_Text cardCountText;

    public ulong OwnerClientId { get; private set; }
    private List<GameObject> spawnedCardBacks = new List<GameObject>();

    public void Initialize(ulong clientId, string playerName, Sprite islandSprite)
    {
        OwnerClientId = clientId;

        if (opponentNameText != null) opponentNameText.text = playerName;
        if (islandBackgroundImage != null && islandSprite != null) islandBackgroundImage.sprite = islandSprite;

        ClearActiveAbility();
        UpdateCardCount(3);
    }

    public void SetDetectiveCat(DetectiveCatType catType)
    {
        if (detectiveProfileImage == null) return;

        Sprite sprite = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetIslandProfileSprite(catType)
            : null;

        if (sprite != null)
        {
            detectiveProfileImage.sprite = sprite;
            detectiveProfileImage.gameObject.SetActive(true);
        }
    }

    public void SetActiveAbility(string cardId)
    {
        if (activeAbilityCardSlot == null) return;

        Sprite sprite = CardDatabase.Instance != null
            ? CardDatabase.Instance.GetCardSprite(cardId)
            : null;

        if (sprite != null)
        {
            activeAbilityCardSlot.sprite = sprite;
            activeAbilityCardSlot.color = Color.white;
            activeAbilityCardSlot.gameObject.SetActive(true);
        }
        else
        {
            ClearActiveAbility();
        }
    }

    public void ClearActiveAbility()
    {
        if (activeAbilityCardSlot != null)
        {
            activeAbilityCardSlot.sprite = null;
            activeAbilityCardSlot.gameObject.SetActive(false);
        }
    }

    public void UpdateCardCount(int count)
    {
        if (cardCountText != null) cardCountText.text = $"{count}";

        if (handCardsContainer != null && cardBackPrefab != null)
        {
            foreach (var card in spawnedCardBacks) if (card != null) Destroy(card);
            spawnedCardBacks.Clear();

            for (int i = 0; i < count; i++)
            {
                GameObject cardBack = Instantiate(cardBackPrefab, handCardsContainer);
                spawnedCardBacks.Add(cardBack);
            }
        }
    }

    public void SetIslandGlow(bool enable, Color glowColor = default)
    {
        Outline outline = GetComponent<Outline>();
        if (outline != null)
        {
            outline.effectColor = glowColor;
            outline.enabled = enable;
        }
    }
}