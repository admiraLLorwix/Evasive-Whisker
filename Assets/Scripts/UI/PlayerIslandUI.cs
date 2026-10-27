using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using EvasiveWhisker.Cards;

public class PlayerIslandUI : MonoBehaviour
{
    [Header("island visuals")]
    [SerializeField] private Image islandBackgroundImage;
    [SerializeField] private TMP_Text opponentNameText;

    [Header("center slots")]
    [SerializeField] private Image detectiveProfileImage;
    [SerializeField] private Image activeAbilityCardSlot;

    [Header("hand cards")]
    [SerializeField] private Transform handCardsContainer;
    [SerializeField] private GameObject cardBackPrefab;
    [SerializeField] private TMP_Text cardCountText;

    public ulong OwnerClientId { get; private set; }
    private List<GameObject> spawnedCardBacks = new List<GameObject>();

    [Header("island highlights")]
    [SerializeField] private GameObject islandGlowObject;
    public void SetIslandGlow(bool enable, Color glowColor)
    {
        if (islandGlowObject != null)
        {
            islandGlowObject.SetActive(enable);
            Image glowImg = islandGlowObject.GetComponent<Image>();
            if (glowImg != null) glowImg.color = glowColor;
            return;
        }

        if (islandBackgroundImage != null)
        {
            Outline outline = islandBackgroundImage.GetComponent<Outline>();
            if (outline == null)
            {
                outline = islandBackgroundImage.gameObject.AddComponent<Outline>();
                outline.effectDistance = new Vector2(8f, -8f);
            }

            outline.effectColor = glowColor;
            outline.enabled = enable;
        }
    }

    public void Initialize(ulong clientId, string playerName, Sprite islandSprite)
    {
        OwnerClientId = clientId;

        if (opponentNameText != null)
        {
            opponentNameText.text = string.IsNullOrEmpty(playerName) ? $"Detective #{clientId}" : playerName;
        }

        if (islandBackgroundImage != null && islandSprite != null)
        {
            islandBackgroundImage.sprite = islandSprite;
        }

        ClearActiveAbility();
        UpdateHandCardCount(3);
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

    public void SetActiveAbility(Card abilityCard)
    {
        if (activeAbilityCardSlot == null) return;

        if (abilityCard != null)
        {
            //Sprite abilitySprite = Sprite.Create(
            //    abilityCard.frontFace,
            //    new Rect(0, 0, abilityCard.frontFace.width, abilityCard.frontFace.height),
            //    new Vector2(0.5f, 0.5f)
            //);

            Sprite abilitySprite = CardDatabase.Instance != null
                ? CardDatabase.Instance.GetCardSprite(abilityCard.cardID)
                : null;

            if (abilitySprite == null && abilityCard.frontfaceFrame != null)
            {
                abilitySprite = Sprite.Create(
                    abilityCard.frontfaceFrame,
                    new Rect(0, 0, abilityCard.frontfaceFrame.width, abilityCard.frontfaceFrame.height),
                    new Vector2(0.5f, 0.5f)
                );
            }

            if (abilitySprite != null)
            {
                activeAbilityCardSlot.sprite = abilitySprite;
                activeAbilityCardSlot.gameObject.SetActive(true);
                return;
            }
        }
        ClearActiveAbility();
    }

    public void ClearActiveAbility()
    {
        if (activeAbilityCardSlot != null)
        {
            activeAbilityCardSlot.sprite = null;
            activeAbilityCardSlot.gameObject.SetActive(false);
        }
    }

    public void UpdateHandCardCount(int cardCount)
    {
        if (cardCountText != null)
        {
            cardCountText.text = $"{cardCount}";
        }

        if (handCardsContainer != null && cardBackPrefab != null)
        {
            foreach (var card in spawnedCardBacks)
            {
                Destroy(card);
            }
            spawnedCardBacks.Clear();

            for (int i = 0; i < cardCount; i++)
            {
                GameObject cardBack = Instantiate(cardBackPrefab, handCardsContainer);
                spawnedCardBacks.Add(cardBack);
            }
        }
    }
}