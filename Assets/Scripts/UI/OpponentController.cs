using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using EvasiveWhisker.Cards;
using UnityEditor;

public class OpponentController : MonoBehaviour
{
	[Header("Island Visuals")]
	[SerializeField] private MeshRenderer islandRenderer;
	[SerializeField] private TMP_Text opponentNameText;

	[Header("Center Slots")]
	[SerializeField] private Image detectiveProfileImage;
	[SerializeField] private Image activeAbilityCardSlot;

	[Header("Hand Cards")]
	[SerializeField] private Transform handCardsContainer;
	[SerializeField] private GameObject cardBackPrefab;
	[SerializeField] private TMP_Text cardCountText;

	public ulong OwnerClientId { get; private set; }
	private List<GameObject> spawnedCardBacks = new List<GameObject>();

	[Header("Island Highlights")]
	[SerializeField] private GameObject islandGlowObject;

	private static readonly int BaseMapPropId = Shader.PropertyToID("_BaseMap");

	private MaterialPropertyBlock _islandPropBlock;


	void Awake()
	{
		_islandPropBlock = new MaterialPropertyBlock();
	}

	public void SetIslandGlow(bool enable, Color glowColor)
	{
		if (islandGlowObject != null)
		{
			islandGlowObject.SetActive(enable);
			Image glowImg = islandGlowObject.GetComponent<Image>();
			if (glowImg != null) glowImg.color = glowColor;
			return;
		}

		//if (islandImageSource != null)
		//{
		//	Outline outline = islandImageSource.GetComponent<Outline>();
		//	if (outline == null)
		//	{
		//		outline = islandImageSource.gameObject.AddComponent<Outline>();
		//		outline.effectDistance = new Vector2(8f, -8f);
		//	}

		//	outline.effectColor = glowColor;
		//	outline.enabled = enable;
		//}
	}

	public void Initialize(ulong clientId, string playerName, Texture2D islandImage)
	{
		OwnerClientId = clientId;

		if (opponentNameText != null)
		{
			opponentNameText.text = string.IsNullOrEmpty(playerName) ? $"Detective #{clientId}" : playerName;
		}

		if (islandRenderer != null && islandImage != null)
		{
			islandRenderer.GetPropertyBlock(_islandPropBlock);
			_islandPropBlock.SetTexture(BaseMapPropId, islandImage);
			islandRenderer.SetPropertyBlock(_islandPropBlock);
		}

		//ClearActiveAbility();
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