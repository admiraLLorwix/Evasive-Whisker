using EvasiveWhisker.Cards;
using UnityEngine;

public class CardController : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private Card cardData;
    [SerializeField] private MeshRenderer frontFaceRenderer;

    private static readonly int BaseMapPropId = Shader.PropertyToID("_BaseMap");

    private MaterialPropertyBlock _propBlock;

    void Awake()
    {
        _propBlock = new MaterialPropertyBlock();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (cardData != null) InitializeCard(cardData);
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void InitializeCard(Card card)
    {
        // visuals related code here
        // whatever visuals that depend on the properties of the current card
        if (frontFaceRenderer == null) return;

        frontFaceRenderer.GetPropertyBlock(_propBlock);
        _propBlock.SetTexture(BaseMapPropId, cardData.frontFace);
        frontFaceRenderer.SetPropertyBlock(_propBlock);
    }

	private void OnValidate()
	{
		if (frontFaceRenderer != null)
		{
			if (_propBlock == null)
			{
				_propBlock = new MaterialPropertyBlock();
			}
			InitializeCard(cardData);
		}
	}
}
