using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class RadialGlowGenerator : MonoBehaviour
{
    [SerializeField] private int textureSize = 256;
    [SerializeField] private float glowRadius = 0.5f;
    private void Awake()
    {
        GenerateGlow();
    }

    private void GenerateGlow()
    {
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[textureSize * textureSize];

        Vector2 center = new Vector2(textureSize / 2f, textureSize / 2f);
        float maxDistance = textureSize * glowRadius;

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float alpha = Mathf.Clamp01(1f - (dist / maxDistance));

                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                alpha = Mathf.Pow(alpha, 2f);
                pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();

        Sprite glowSprite = Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f));

        Image img = GetComponent<Image>();
        img.sprite = glowSprite;
        img.type = Image.Type.Simple;
        img.preserveAspect = true;
        img.raycastTarget = false;
    }
}