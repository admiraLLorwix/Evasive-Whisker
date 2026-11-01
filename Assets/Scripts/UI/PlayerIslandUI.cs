using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using EvasiveWhisker.Cards;
using System.Collections;

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
    [SerializeField] private GameObject guessImmunityBadge; //need new asset for badge

    [SerializeField] private float miniCardSpacingX = 42f; [SerializeField] private float miniMaxTiltZ = 9f;
    [SerializeField] private float miniArcDropY = 4f;
    public string PlayerName { get; private set; }

    public ulong OwnerClientId { get; private set; }
    public DetectiveCatType ChosenCat { get; private set; }
    public string ActiveAbilityCardId { get; private set; } = null;
    public int CurrentCardCount { get; private set; } = 3;
    public bool IsGuessImmune { get; private set; } = false;
    private List<GameObject> spawnedCardBacks = new List<GameObject>();
    //adtl for handreshuffle
    private List<Vector2> restPositions = new List<Vector2>();
    private List<Quaternion> restRotations = new List<Quaternion>();
    private Coroutine shuffleRoutine;

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
            bool selecting = (TargetingManager.Instance != null && TargetingManager.Instance.IsTargeting)
                          || (StealManager.Instance != null && StealManager.Instance.IsSelectingTarget);
            if (selecting && IsGuessImmune)
            {
                TurnActionUI.Instance?.ShowTemporaryNotification($"<color=red>{PlayerName} is protected after a failed accusation</color>", 2.5f);
                return;
            }
            //shouldn't open infomodal
            if (TargetingManager.Instance != null && TargetingManager.Instance.IsTargeting)
            {
                TargetingManager.Instance.OnOpponentTargetChosen(OwnerClientId);
                return;
            }
            //shouldn't open infomodal
            if (StealManager.Instance != null && StealManager.Instance.IsSelectingTarget)
            {
                StealManager.Instance.OnOpponentIslandClicked(OwnerClientId);
                return;
            }
            //only case to open
            if (OpponentInfoModal.Instance != null)
            {
                OpponentInfoModal.Instance.ShowOpponentInfoModal(OwnerClientId, PlayerName, ChosenCat, CurrentCardCount, ActiveAbilityCardId);
            }
        });

        ClearActiveAbility();
        SetGuessImmune(false);
        UpdateCardCount(3);
    }
    //for wrong accusation
    public void SetGuessImmune(bool immune)
    {
        IsGuessImmune = immune;
        if (guessImmunityBadge != null) guessImmunityBadge.SetActive(immune);
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
        if (shuffleRoutine != null)
        {
            StopCoroutine(shuffleRoutine);
            shuffleRoutine = null;
        }

        CurrentCardCount = count;
        if (cardCountText != null) cardCountText.text = $"{count}";

        if (handCardsContainer != null && cardBackPrefab != null)
        {
            foreach (var card in spawnedCardBacks) if (card != null) Destroy(card);
            spawnedCardBacks.Clear(); 
            restPositions.Clear(); restRotations.Clear();

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
                restPositions.Add(rt != null ? rt.anchoredPosition : Vector2.zero);
                restRotations.Add(rt != null ? rt.localRotation : Quaternion.identity);

                Button btn = cardBack.GetComponentInChildren<Button>();
                if (btn != null) btn.interactable = false;

                spawnedCardBacks.Add(cardBack);
            }
        }
    }
    //animation reference from rrepo#10
    public void PlayShuffleAnimation()
    {
        if (!isActiveAndEnabled) return;
        if (spawnedCardBacks.Count < 2 || restPositions.Count != spawnedCardBacks.Count) return;

        if (shuffleRoutine != null)
        {
            StopCoroutine(shuffleRoutine);
            SnapCardBacksToRest();
        }
        shuffleRoutine = StartCoroutine(ShuffleAnimationRoutine());
    }

    private void SnapCardBacksToRest()
    {
        for (int i = 0; i < spawnedCardBacks.Count && i < restPositions.Count; i++)
        {
            if (spawnedCardBacks[i] == null) continue;
            RectTransform rt = spawnedCardBacks[i].GetComponent<RectTransform>();
            if (rt == null) continue;
            rt.anchoredPosition = restPositions[i];
            rt.localRotation = restRotations[i];
        }
    }

    private IEnumerator ShuffleAnimationRoutine()
    {
        const float gatherTime = 0.35f;
        const float riffleTime = 0.55f;
        const float fanTime = 0.40f;
        const float riffleSpread = 38f;
        const float riffleLift = 6f;
        const int riffleCount = 2;

        int n = spawnedCardBacks.Count;
        var rts = new RectTransform[n];
        for (int i = 0; i < n; i++)
            rts[i] = spawnedCardBacks[i] != null ? spawnedCardBacks[i].GetComponent<RectTransform>() : null;

        Vector2 StackPos(int i) => new Vector2(0f, i * 1.2f);

        float t = 0f;
        while (t < gatherTime)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, t / gatherTime);
            for (int i = 0; i < n; i++)
            {
                if (rts[i] == null) continue;
                rts[i].anchoredPosition = Vector2.Lerp(restPositions[i], StackPos(i), k);
                rts[i].localRotation = Quaternion.Slerp(restRotations[i], Quaternion.identity, k);
            }
            yield return null;
        }

        t = 0f;
        while (t < riffleTime)
        {
            t += Time.deltaTime;
            float u = Mathf.Repeat(t / riffleTime * riffleCount, 1f);
            float s = Mathf.Sin(u * Mathf.PI);
            for (int i = 0; i < n; i++)
            {
                if (rts[i] == null) continue;
                float side = (i % 2 == 0) ? -1f : 1f;
                Vector2 p = StackPos(i);
                p.x += side * s * riffleSpread;
                p.y += side * s * riffleLift;
                rts[i].anchoredPosition = p;
                rts[i].localRotation = Quaternion.Euler(0f, 0f, side * s * 4f);
            }
            yield return null;
        }

        t = 0f;
        while (t < fanTime)
        {
            t += Time.deltaTime;
            for (int i = 0; i < n; i++)
            {
                if (rts[i] == null) continue;
                float delay = (n > 1 ? (float)i / (n - 1) : 0f) * 0.25f * fanTime;
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - delay) / (fanTime - delay)));
                rts[i].anchoredPosition = Vector2.Lerp(StackPos(i), restPositions[i], k);
                rts[i].localRotation = Quaternion.Slerp(Quaternion.identity, restRotations[i], k);
            }
            yield return null;
        }

        SnapCardBacksToRest();
        shuffleRoutine = null;
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