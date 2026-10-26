using EvasiveWhisker.Cards;
using UnityEngine;

public class CardController : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private Card cardData;
	
    [Header("Mesh Renderers")]
	[SerializeField] private MeshRenderer frontFaceRenderer;
    //[SerializeField] private MeshRenderer backFaceRenderer;
    [SerializeField] private MeshRenderer characterArtRenderer;

    [Header("Visual Settings")]
    //[SerializeField] private Texture2D frameMask;
    [ColorUsage(true, hdr: true)]
    [SerializeField] private Color glintColor;
    [SerializeField] private float glintStrength;
    [SerializeField] private float moveSpeedX;
    [SerializeField] private float moveSpeedY;
    [SerializeField] private float scale;

	// int hash calculated once for pointing to specific _BaseMap var in memory
	private static readonly int BaseMapPropId = Shader.PropertyToID("_BaseMap");
	//private static readonly int FrameMaskPropId = Shader.PropertyToID("_FrameMask");
    private static readonly int GlintColorPropId = Shader.PropertyToID("_GlintColor");
	private static readonly int GlintStrengthPropId = Shader.PropertyToID("_GlintStrength");
	private static readonly int MoveSpeedXPropId = Shader.PropertyToID("_MoveSpeedX");
	private static readonly int MoveSpeedYPropId = Shader.PropertyToID("_MoveSpeedY");
	private static readonly int ScalePropId = Shader.PropertyToID("_Scale");
	private MaterialPropertyBlock _frontFacePropBlock;
	private MaterialPropertyBlock _characterArtPropBlock;
    //private MaterialPropertyBlock _backFacePropBlock;

    void Awake()
    {
        _frontFacePropBlock = new MaterialPropertyBlock();
        _characterArtPropBlock = new MaterialPropertyBlock();
        //_backFacePropBlock = new MaterialPropertyBlock();
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
        if (frontFaceRenderer == null
            //|| backFaceRenderer == null
            || characterArtRenderer == null) return;

		frontFaceRenderer.GetPropertyBlock(_frontFacePropBlock);
        _frontFacePropBlock.SetTexture(BaseMapPropId, cardData.frontfaceFrame);
        //_frontFacePropBlock.SetTexture(FrameMaskPropId, frameMask);
        _frontFacePropBlock.SetColor(GlintColorPropId, glintColor);
        _frontFacePropBlock.SetFloat(GlintStrengthPropId, glintStrength);
        _frontFacePropBlock.SetFloat(MoveSpeedXPropId, moveSpeedX);
        _frontFacePropBlock.SetFloat(MoveSpeedYPropId, moveSpeedY);
        _frontFacePropBlock.SetFloat(ScalePropId, scale);
        frontFaceRenderer.SetPropertyBlock(_frontFacePropBlock);

		characterArtRenderer.GetPropertyBlock(_characterArtPropBlock);
		_characterArtPropBlock.SetTexture(BaseMapPropId, cardData.characterArt);
        characterArtRenderer.SetPropertyBlock(_characterArtPropBlock);

		//backFaceRenderer.GetPropertyBlock(_backFacePropBlock);
        //_backFacePropBlock.SetTexture(BaseMapPropId, cardData.backfaceForeground);
        //backFaceRenderer.SetPropertyBlock(_backFacePropBlock);

    }

	private void OnValidate()
	{
		if (frontFaceRenderer != null
            //&& backFaceRenderer != null
            && characterArtRenderer != null)
		{
            // assign prop blocks if null
            _frontFacePropBlock ??= new MaterialPropertyBlock();
            _characterArtPropBlock ??= new MaterialPropertyBlock();
            //_backFacePropBlock ??= new MaterialPropertyBlock();
			InitializeCard(cardData);
		}
	}
}
