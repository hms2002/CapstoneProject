using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class DemonKingPrimitiveVisual : MonoBehaviour
{
    private const int SquareTextureSize = 16;
    private const int CircleTextureSize = 64;
    private const float VisualZ = -0.05f;
    private const int ProjectileSortingOrder = 1;

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

    public static void ApplyProjectileSorting(SpriteRenderer renderer, int sortingOrder = ProjectileSortingOrder)
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
        ApplyProjectileSorting(renderer);

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

public static class DemonKingPatternVfx
{
    private const string ExplosionVfxPath = "DemonKing/Vfx/DemonKingExplosionVfx";
    private const string ImpactVfxPath = "DemonKing/Vfx/DemonKingImpactVfx";
    private const string StabVfxPath = "DemonKing/Vfx/DemonKingStabVfx";
    private const string SlashVfxPath = "DemonKing/Vfx/DarkLordSlashVfx";
    private const string GroggyReleaseVfxPath = "DemonKing/Vfx/DarkLordGroggyReleaseVfx";
    private const string EgoSwordAttackVfxPath = "DemonKing/Vfx/EgoSwordAttackVfx";
    private const string EgoSwordAuraVfxPath = "DemonKing/Vfx/EgoSwordAttackAuraVfx";

    private const int DefaultSortingOrder = 1;

    public static DemonKingAnimationClipVisual SpawnExplosion(Vector2 center, float diameter)
    {
        return DemonKingAnimationClipVisual.SpawnOneShot(
            ExplosionVfxPath,
            center,
            new Vector2(diameter, diameter),
            0f,
            "DemonKing_ExplosionVfx",
            DefaultSortingOrder);
    }

    public static void SpawnExplosionOrFallbackCircle(
        Vector2 center,
        float diameter,
        Color fallbackColor,
        string fallbackName)
    {
        if (SpawnExplosion(center, diameter) == null)
            DemonKingPrimitiveVisual.SpawnCircle(center, diameter, 0.12f, fallbackColor, fallbackName);
    }

    public static DemonKingAnimationClipVisual SpawnImpact(Vector2 center, float diameter)
    {
        return DemonKingAnimationClipVisual.SpawnOneShot(
            ImpactVfxPath,
            center,
            new Vector2(diameter, diameter),
            0f,
            "DemonKing_ImpactVfx",
            DefaultSortingOrder);
    }

    public static DemonKingAnimationClipVisual SpawnStab(Vector2 start, Vector2 direction, float distance, float hitWidth)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float safeDistance = Mathf.Max(0.1f, distance);
        Vector2 center = start + safeDirection * (safeDistance * 0.5f);
        Vector2 size = new(safeDistance, Mathf.Max(0.8f, hitWidth * 1.8f));

        return DemonKingAnimationClipVisual.SpawnOneShot(
            StabVfxPath,
            center,
            size,
            DemonKingCombatUtil.RotationDeg(safeDirection),
            "DemonKing_StabVfx",
            DefaultSortingOrder);
    }

    public static DemonKingAnimationClipVisual SpawnSlash(Vector2 origin, Vector2 direction, float radius)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float safeRadius = Mathf.Max(0.1f, radius);
        Vector2 center = origin + safeDirection * (safeRadius * 0.5f);
        Vector2 size = new(safeRadius, safeRadius * 2f);

        return DemonKingAnimationClipVisual.SpawnOneShot(
            SlashVfxPath,
            center,
            size,
            DemonKingCombatUtil.RotationDeg(safeDirection),
            "DarkLord_SlashVfx",
            DefaultSortingOrder);
    }

    public static DemonKingAnimationClipVisual SpawnGroggyRelease(Vector2 center, float diameter)
    {
        return DemonKingAnimationClipVisual.SpawnOneShot(
            GroggyReleaseVfxPath,
            center,
            new Vector2(diameter, diameter),
            0f,
            "DarkLord_GroggyReleaseVfx",
            DefaultSortingOrder);
    }

    public static DemonKingAnimationClipVisual SpawnEgoSwordAura(Transform parent, float diameter)
    {
        return DemonKingAnimationClipVisual.SpawnAttachedStartThenLoop(
            EgoSwordAuraVfxPath,
            parent,
            new Vector3(0f, 0f, -0.05f),
            new Vector2(diameter, diameter),
            0f,
            "EgoSword_AttackAuraVfx",
            DefaultSortingOrder);
    }

    public static DemonKingAnimationClipVisual SpawnEgoSwordAttack(Transform parent, float diameter)
    {
        return DemonKingAnimationClipVisual.SpawnAttachedOneShot(
            EgoSwordAttackVfxPath,
            parent,
            new Vector3(0f, 0f, -0.06f),
            new Vector2(diameter, diameter),
            0f,
            "EgoSword_AttackVfx",
            DefaultSortingOrder);
    }
}

[DisallowMultipleComponent]
public sealed class DemonKingAnimationClipVisual : MonoBehaviour
{
    private const float VisualZ = -0.06f;
    private const string OneShotStateName = "Play";
    private const string StartStateName = "Start";
    private const string IdleStateName = "Idle";

    private static readonly System.Collections.Generic.Dictionary<string, GameObject> PrefabCache = new();
    private static readonly System.Collections.Generic.HashSet<string> MissingPrefabWarnings = new();

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Coroutine playbackRoutine;

    public bool IsPlaying { get; private set; }

    public static DemonKingAnimationClipVisual SpawnOneShot(
        string resourcePath,
        Vector2 center,
        Vector2 targetSize,
        float rotationDeg,
        string name,
        int sortingOrder)
    {
        DemonKingAnimationClipVisual visual = InstantiateVisual(
            resourcePath,
            null,
            new Vector3(center.x, center.y, VisualZ),
            rotationDeg,
            name,
            sortingOrder);
        if (visual == null)
            return null;

        visual.PlayOneShot(targetSize);
        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnAttachedOneShot(
        string resourcePath,
        Transform parent,
        Vector3 localPosition,
        Vector2 targetSize,
        float localRotationDeg,
        string name,
        int sortingOrder)
    {
        if (parent == null)
            return null;

        DemonKingAnimationClipVisual visual = InstantiateVisual(
            resourcePath,
            parent,
            localPosition,
            localRotationDeg,
            name,
            sortingOrder);
        if (visual == null)
            return null;

        visual.PlayOneShot(targetSize);
        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnAttachedStartThenLoop(
        string resourcePath,
        Transform parent,
        Vector3 localPosition,
        Vector2 targetSize,
        float localRotationDeg,
        string name,
        int sortingOrder)
    {
        if (parent == null)
            return null;

        DemonKingAnimationClipVisual visual = InstantiateVisual(
            resourcePath,
            parent,
            localPosition,
            localRotationDeg,
            name,
            sortingOrder);
        if (visual == null)
            return null;

        visual.PlayStartThenLoop(targetSize);
        return visual;
    }

    public void StopAndRelease()
    {
        StopPlayback();
        Destroy(gameObject);
    }

    private static DemonKingAnimationClipVisual InstantiateVisual(
        string resourcePath,
        Transform parent,
        Vector3 position,
        float rotationDeg,
        string name,
        int sortingOrder)
    {
        GameObject prefab = LoadPrefab(resourcePath);
        if (prefab == null)
            return null;

        GameObject visualObject = Instantiate(prefab);
        visualObject.name = string.IsNullOrWhiteSpace(name) ? prefab.name : name;
        if (parent != null)
        {
            visualObject.transform.SetParent(parent, false);
            visualObject.transform.localPosition = position;
            visualObject.transform.localRotation = Quaternion.Euler(0f, 0f, rotationDeg);
        }
        else
        {
            visualObject.transform.position = position;
            visualObject.transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
        }

        DemonKingAnimationClipVisual visual = visualObject.GetComponent<DemonKingAnimationClipVisual>();
        if (visual == null)
            visual = visualObject.AddComponent<DemonKingAnimationClipVisual>();

        visual.CacheRuntimeReferences();
        visual.ApplySorting(sortingOrder);
        return visual;
    }

    private void PlayOneShot(Vector2 targetSize)
    {
        StopPlayback();
        IsPlaying = true;
        PlayAnimatorState(OneShotStateName);
        ApplyTargetScale(targetSize);
        playbackRoutine = StartCoroutine(CoDestroyAfterSeconds(ResolveLongestClipLength()));
    }

    private void PlayStartThenLoop(Vector2 targetSize)
    {
        StopPlayback();
        IsPlaying = true;
        playbackRoutine = StartCoroutine(CoPlayStartThenLoop(targetSize));
    }

    private IEnumerator CoDestroyAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));
        IsPlaying = false;
        playbackRoutine = null;
        Destroy(gameObject);
    }

    private IEnumerator CoPlayStartThenLoop(Vector2 targetSize)
    {
        PlayAnimatorState(StartStateName);
        ApplyTargetScale(targetSize);

        float startLength = ResolveClipLength(StartStateName);
        if (startLength > 0f)
            yield return new WaitForSeconds(startLength);

        if (!PlayAnimatorState(IdleStateName))
        {
            IsPlaying = false;
            yield break;
        }

        ApplyTargetScale(targetSize);
        playbackRoutine = null;
    }

    private void StopPlayback()
    {
        if (playbackRoutine != null)
        {
            StopCoroutine(playbackRoutine);
            playbackRoutine = null;
        }

        IsPlaying = false;
    }

    private void ApplyTargetScale(Vector2 targetSize)
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null || targetSize.x <= 0f || targetSize.y <= 0f)
            return;

        Vector2 sourceSize = spriteRenderer.sprite.bounds.size;
        float scaleX = sourceSize.x > 0f ? targetSize.x / sourceSize.x : 1f;
        float scaleY = sourceSize.y > 0f ? targetSize.y / sourceSize.y : 1f;
        transform.localScale = new Vector3(Mathf.Max(0.01f, scaleX), Mathf.Max(0.01f, scaleY), 1f);
    }

    private void OnDisable()
    {
        StopPlayback();
    }

    private void CacheRuntimeReferences()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);

        if (animator == null)
            animator = GetComponentInChildren<Animator>(true);

        if (animator != null)
        {
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.enabled = true;
        }
    }

    private void ApplySorting(int sortingOrder)
    {
        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            DemonKingPrimitiveVisual.ApplyProjectileSorting(renderers[i], sortingOrder);
    }

    private bool PlayAnimatorState(string stateName)
    {
        CacheRuntimeReferences();
        if (animator == null || animator.runtimeAnimatorController == null)
            return false;

        animator.Play(stateName, 0, 0f);
        animator.Update(0f);
        return true;
    }

    private float ResolveLongestClipLength()
    {
        AnimationClip[] clips = ResolveClips();
        float length = 0.01f;
        for (int i = 0; i < clips.Length; i++)
            length = Mathf.Max(length, clips[i] != null ? clips[i].length : 0f);

        return length;
    }

    private float ResolveClipLength(string nameFragment)
    {
        AnimationClip[] clips = ResolveClips();
        for (int i = 0; i < clips.Length; i++)
        {
            AnimationClip clip = clips[i];
            if (clip != null && clip.name.IndexOf(nameFragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return Mathf.Max(0.01f, clip.length);
        }

        return 0f;
    }

    private AnimationClip[] ResolveClips()
    {
        CacheRuntimeReferences();
        return animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips
            : System.Array.Empty<AnimationClip>();
    }

    private static GameObject LoadPrefab(string resourcePath)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        if (PrefabCache.TryGetValue(resourcePath, out GameObject cachedPrefab))
            return cachedPrefab;

        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        PrefabCache[resourcePath] = prefab;
        if (prefab == null && MissingPrefabWarnings.Add(resourcePath))
            Debug.LogWarning($"DemonKing VFX prefab not found at Resources/{resourcePath}.");

        return prefab;
    }
}
