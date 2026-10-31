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

    [SerializeField] private float miniCardSpacingX = 42f; [SerializeField] private float miniMaxTiltZ = 9f;
    [SerializeField] private float miniArcDropY = 4f;
    public string PlayerName { get; private set; }

    public ulong OwnerClientId { get; private set; }
    public DetectiveCatType ChosenCat { get; private set; }
    public string ActiveAbilityCardId { get; private set; } = null;
    public int CurrentCardCount { get; private set; } = 3;
    private List<GameObject> spawnedCardBacks = new List<GameObject>();

    public void Initialize(ulong clientId, string playerName, Sprite islandSprite)
    {
        OwnerClientId = clientId;
        PlayerName = playerName;

        if (opponentNameText != null) opponentNameText.text = playerName;
        if (islandBackgroundImage != null && islandSprite != null) islandBackgroundImage.sprite = islandSprite;

        Button btn = GetComponent<Button>();
        if (btn == null) btn = gameObject.AddComponent<Button>();

        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            if (StealManager.Instance != null && StealManager.Instance.IsSelectingTarget)
            {
                StealManager.Instance.OnOpponentIslandClicked(OwnerClientId);
                return;
            }

            if (OpponentInfoModal.Instance != null)
            {
                OpponentInfoModal.Instance?.ShowOpponentInfoModal(OwnerClientId, PlayerName, ChosenCat, CurrentCardCount, ActiveAbilityCardId);
            }
        });

        ClearActiveAbility();
        UpdateCardCount(3);
    }

    public void SetDetectiveCat(DetectiveCatType catType)
    {
        ChosenCat = catType;
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
        ActiveAbilityCardId = cardId;
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
        ActiveAbilityCardId = null;
        if (activeAbilityCardSlot != null)
        {
            activeAbilityCardSlot.sprite = null;
            activeAbilityCardSlot.gameObject.SetActive(false);
        }
    }

    public void UpdateCardCount(int count)
    {
        CurrentCardCount = count;
        if (cardCountText != null) cardCountText.text = $"{count}";

        if (handCardsContainer != null && cardBackPrefab != null)
        {
            foreach (var card in spawnedCardBacks) if (card != null) Destroy(card);
            spawnedCardBacks.Clear();

            for (int i = 0; i < count; i++)
            {
                GameObject cardBack = Instantiate(cardBackPrefab, handCardsContainer);
                RectTransform rt = cardBack.GetComponent<RectTransform>();

                if (rt != null)
                {
                    rt.sizeDelta = new Vector2(60, 90);

                    float t = (count <= 1) ? 0f : ((float)i / (count - 1) * 2f) - 1f;

                    float xPos = t * (miniCardSpacingX * (count * 0.45f));
                    float yPos = -(t * t) * miniArcDropY;
                    float zRot = -t * miniMaxTiltZ;

                    rt.anchoredPosition = new Vector2(xPos, yPos);
                    rt.localRotation = Quaternion.Euler(0f, 0f, zRot);
                }

                Button btn = cardBack.GetComponentInChildren<Button>();
                if (btn != null) btn.interactable = false;

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