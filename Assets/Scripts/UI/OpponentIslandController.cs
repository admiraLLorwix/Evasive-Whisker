using UnityEngine;

public class OpponentIslandController : MonoBehaviour
{

    [SerializeField]
    public Texture2D islandSourceImage;
    [SerializeField]
    public MeshRenderer islandRenderer;

	private static readonly int BaseMapPropId = Shader.PropertyToID("_BaseMap");
    private MaterialPropertyBlock _islandPropBlock;

    private void Initialize()
    {
        if (islandSourceImage == null) return;
        islandRenderer.GetPropertyBlock(_islandPropBlock);
        _islandPropBlock.SetTexture(BaseMapPropId, islandSourceImage);
        islandRenderer.SetPropertyBlock(_islandPropBlock);
    }

	void Awake()
    {
        _islandPropBlock = new MaterialPropertyBlock();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        if (islandSourceImage != null) Initialize();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

	private void OnValidate()
	{
		if (islandRenderer != null)
        {
            _islandPropBlock ??= new MaterialPropertyBlock();
            Initialize();
        }
	}
}
