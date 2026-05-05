using UnityEngine;

[DisallowMultipleComponent]
public sealed class DemonKingPrimitiveVisual : MonoBehaviour
{
    private const int SquareTextureSize = 16;
    private const int CircleTextureSize = 64;
    private const float VisualZ = -0.05f;

    private static Sprite squareSprite;
    private static Sprite circleSprite;
    private static int projectileSortingLayerId = int.MinValue;

    private float remainingLifetime;
    private bool hasLifetime;

    public static DemonKingPrimitiveVisual SpawnSquare(
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        float duration,
        Color color,
        string name = "DemonKing_SquareVisual")
    {
        return Spawn(EnsureSquareSprite(), center, size, rotationDeg, duration, color, name);
    }

    public static DemonKingPrimitiveVisual SpawnCircle(
        Vector2 center,
        float diameter,
        float duration,
        Color color,
        string name = "DemonKing_CircleVisual")
    {
        return Spawn(EnsureCircleSprite(), center, new Vector2(diameter, diameter), 0f, duration, color, name);
    }

    public static Sprite GetCircleSprite()
    {
        return EnsureCircleSprite();
    }

    public static Sprite GetSquareSprite()
    {
        return EnsureSquareSprite();
    }

    public static void ApplyProjectileSorting(SpriteRenderer renderer, int sortingOrder = 1000)
    {
        if (renderer == null)
            return;

        int sortingLayerId = ResolveProjectileSortingLayerId();
        if (sortingLayerId != 0)
            renderer.sortingLayerID = sortingLayerId;
        else
            renderer.sortingLayerName = "Projectile";

        renderer.sortingOrder = sortingOrder;
    }

    public void UpdateGeometry(Vector2 center, Vector2 size, float rotationDeg)
    {
        transform.position = new Vector3(center.x, center.y, VisualZ);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
        transform.localScale = new Vector3(Mathf.Max(0.01f, size.x), Mathf.Max(0.01f, size.y), 1f);
    }

    private static DemonKingPrimitiveVisual Spawn(
        Sprite sprite,
        Vector2 center,
        Vector2 size,
        float rotationDeg,
        float duration,
        Color color,
        string name)
    {
        GameObject visualObject = new(name);
        SpriteRenderer renderer = visualObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        ApplyProjectileSorting(renderer, 1000);

        DemonKingPrimitiveVisual visual = visualObject.AddComponent<DemonKingPrimitiveVisual>();
        visual.UpdateGeometry(center, size, rotationDeg);
        visual.SetLifetime(duration);
        return visual;
    }

    private static int ResolveProjectileSortingLayerId()
    {
        if (projectileSortingLayerId != int.MinValue)
            return projectileSortingLayerId;

        projectileSortingLayerId = SortingLayer.NameToID("Projectile");
        return projectileSortingLayerId;
    }

    private void SetLifetime(float duration)
    {
        hasLifetime = duration > 0f;
        remainingLifetime = duration;
    }

    private void Update()
    {
        if (!hasLifetime)
            return;

        remainingLifetime -= Time.deltaTime;
        if (remainingLifetime <= 0f)
            Destroy(gameObject);
    }

    private static Sprite EnsureSquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        Texture2D texture = new(SquareTextureSize, SquareTextureSize, TextureFormat.RGBA32, false);
        Color32 white = new(255, 255, 255, 255);
        Color32[] pixels = new Color32[SquareTextureSize * SquareTextureSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = white;

        texture.SetPixels32(pixels);
        texture.Apply();
        texture.name = "DemonKing_DefaultSquare";
        squareSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, SquareTextureSize, SquareTextureSize),
            new Vector2(0.5f, 0.5f),
            SquareTextureSize);
        squareSprite.name = "DemonKing_DefaultSquare";
        return squareSprite;
    }

    private static Sprite EnsureCircleSprite()
    {
        if (circleSprite != null)
            return circleSprite;

        Texture2D texture = new(CircleTextureSize, CircleTextureSize, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[CircleTextureSize * CircleTextureSize];
        Vector2 center = new((CircleTextureSize - 1) * 0.5f, (CircleTextureSize - 1) * 0.5f);
        float radius = CircleTextureSize * 0.5f - 1f;
        float radiusSqr = radius * radius;

        for (int y = 0; y < CircleTextureSize; y++)
        {
            for (int x = 0; x < CircleTextureSize; x++)
            {
                Vector2 delta = new(x, y);
                bool inside = (delta - center).sqrMagnitude <= radiusSqr;
                pixels[y * CircleTextureSize + x] = inside
                    ? new Color32(255, 255, 255, 255)
                    : new Color32(255, 255, 255, 0);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply();
        texture.name = "DemonKing_DefaultCircle";
        circleSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, CircleTextureSize, CircleTextureSize),
            new Vector2(0.5f, 0.5f),
            CircleTextureSize);
        circleSprite.name = "DemonKing_DefaultCircle";
        return circleSprite;
    }
}
