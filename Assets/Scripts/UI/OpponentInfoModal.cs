using UnityEngine;
using UnityEngine.UI;
using TMPro;
using EvasiveWhisker.Cards;

public class OpponentInfoModal : MonoBehaviour
{
    public static OpponentInfoModal Instance { get; private set; }

    [SerializeField] private GameObject modalRoot;
    [SerializeField] private Button backgroundDimButton; [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text playerNameText; [SerializeField] private TMP_Text cardCountText;
    [SerializeField] private Image detectiveCatImage; [SerializeField] private Image peekingCatAvatar;
    [SerializeField] private Image activeAbilityImage;
    [SerializeField] private TMP_Text noAbilityPlaceholderText;

    private void Awake()
    {
        Instance = this;
        if (modalRoot != null) modalRoot.SetActive(false);

        if (closeButton != null) closeButton.onClick.AddListener(Hide);
        if (backgroundDimButton != null) backgroundDimButton.onClick.AddListener(Hide);
    }

    public void ShowOpponentInfoModal(ulong opponentId, string directName = "", DetectiveCatType catType = DetectiveCatType.HoarderMolly, int cardCount = 3, string directAbilityId = null)
    {
        gameObject.SetActive(true);
        if (modalRoot != null) modalRoot.SetActive(true);

        Transform panel = transform.Find("InfoCardPanel");
        if (panel != null) panel.gameObject.SetActive(true);

        Transform dim = transform.Find("DimBackground");
        if (dim != null) dim.gameObject.SetActive(true);

        transform.SetAsLastSibling();

        string pName = !string.IsNullOrEmpty(directName) ? directName : $"Detective #{opponentId}";

        if (playerNameText != null) playerNameText.text = pName;
        if (cardCountText != null) cardCountText.text = $"Cards in Hand: {cardCount}";

        if (peekingCatAvatar != null && CardDatabase.Instance != null)
        {
            Sprite avatarSprite = CardDatabase.Instance.GetIslandProfileSprite(catType);
            if (avatarSprite != null)
            {
                peekingCatAvatar.sprite = avatarSprite;
                peekingCatAvatar.gameObject.SetActive(true);
            }
        }

        if (detectiveCatImage != null && CardDatabase.Instance != null)
        {
            Sprite catCardSprite = CardDatabase.Instance.GetLocalHandCardSprite(catType);
            if (catCardSprite != null)
            {
                detectiveCatImage.sprite = catCardSprite;
                detectiveCatImage.color = Color.white;
                detectiveCatImage.gameObject.SetActive(true);
            }
        }

        string activeAbilityId = !string.IsNullOrEmpty(directAbilityId) ? directAbilityId :
            (GameManager.Instance != null ? GameManager.Instance.GetPlayerActiveAbilityId(opponentId) : null);

        if (!string.IsNullOrEmpty(activeAbilityId) && CardDatabase.Instance != null)
        {
            Sprite abilitySprite = CardDatabase.Instance.GetCardSprite(activeAbilityId);
            if (abilitySprite != null && activeAbilityImage != null)
            {
                activeAbilityImage.sprite = abilitySprite;
                activeAbilityImage.color = Color.white;
                activeAbilityImage.gameObject.SetActive(true);
                if (noAbilityPlaceholderText != null) noAbilityPlaceholderText.gameObject.SetActive(false);
            }
        }
        else
        {
            if (activeAbilityImage != null) activeAbilityImage.gameObject.SetActive(false);
            if (noAbilityPlaceholderText != null)
            {
                noAbilityPlaceholderText.gameObject.SetActive(true);
                noAbilityPlaceholderText.text = "No Active Ability";
            }
        }
    }

    public void Hide()
    {
        if (modalRoot != null) modalRoot.SetActive(false);
    }
}