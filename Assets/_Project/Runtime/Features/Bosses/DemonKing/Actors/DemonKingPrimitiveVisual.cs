using System;
using System.Collections;
using CapstonePresentation;
using UnityEngine;
using UnityGAS;

// 책임: 마왕 패턴에서 런타임 생성되는 단순 사각/원형 경고 및 타격 비주얼을 표시한다.
[DisallowMultipleComponent]
public sealed class DemonKingPrimitiveVisual : MonoBehaviour
{
    private const int SquareTextureSize = 16;
    private const int CircleTextureSize = 64;
    private const float VisualZ = -0.05f;
    private const int ProjectileSortingOrder = 1;
    private const int EntitySortingOrder = 0;

    private static Sprite squareSprite;
    private static Sprite circleSprite;
    private static int projectileSortingLayerId = int.MinValue;
    private static int entitySortingLayerId = int.MinValue;

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

    public static void ApplyEntitySorting(SpriteRenderer renderer, int sortingOrder = EntitySortingOrder)
    {
        if (renderer == null)
            return;

        int sortingLayerId = ResolveEntitySortingLayerId();
        if (sortingLayerId != 0)
            renderer.sortingLayerID = sortingLayerId;
        else
            renderer.sortingLayerName = "Entity";

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

    private static int ResolveEntitySortingLayerId()
    {
        if (entitySortingLayerId != int.MinValue)
            return entitySortingLayerId;

        entitySortingLayerId = SortingLayer.NameToID("Entity");
        return entitySortingLayerId;
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

// 책임: groggy 반격 등 마왕 패턴 중 월드 암전 오버레이의 알파와 수명을 관리한다.
[DisallowMultipleComponent]
public sealed class DemonKingWorldDimmingOverlay : MonoBehaviour
{
    private const int OverlaySortingOrder = -1;
    private const float OrthographicPadding = 1.2f;
    private const float FallbackWorldSize = 64f;
    private const float OverlayZ = -0.05f;

    private SpriteRenderer overlayRenderer;
    private Camera targetCamera;
    private Transform fallbackAnchor;
    private bool released;
    private float alpha;

    public float Alpha => alpha;

    public static DemonKingWorldDimmingOverlay Begin(DemonKingController owner, float initialAlpha)
    {
        GameObject overlayObject = new("DemonKing_GroggyCounterWorldDim");
        DemonKingWorldDimmingOverlay overlay = overlayObject.AddComponent<DemonKingWorldDimmingOverlay>();
        overlay.Initialize(owner, initialAlpha);
        return overlay;
    }

    public void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
        if (overlayRenderer != null)
            overlayRenderer.color = new Color(0f, 0f, 0f, alpha);
    }

    public void Release()
    {
        if (released)
            return;

        released = true;
        Destroy(gameObject);
    }

    private void Initialize(DemonKingController owner, float initialAlpha)
    {
        fallbackAnchor = owner != null ? owner.transform : null;
        targetCamera = Camera.main != null ? Camera.main : UnityEngine.Object.FindAnyObjectByType<Camera>();

        overlayRenderer = gameObject.AddComponent<SpriteRenderer>();
        overlayRenderer.sprite = DemonKingPrimitiveVisual.GetSquareSprite();
        overlayRenderer.maskInteraction = SpriteMaskInteraction.None;
        DemonKingPrimitiveVisual.ApplyEntitySorting(overlayRenderer, OverlaySortingOrder);

        SetAlpha(initialAlpha);
        FollowCamera();
    }

    private void LateUpdate()
    {
        FollowCamera();
    }

    private void FollowCamera()
    {
        Vector3 center = targetCamera != null
            ? targetCamera.transform.position
            : fallbackAnchor != null
                ? fallbackAnchor.position
                : Vector3.zero;

        transform.position = new Vector3(center.x, center.y, OverlayZ);
        transform.rotation = Quaternion.identity;

        if (targetCamera != null && targetCamera.orthographic)
        {
            float height = Mathf.Max(0.01f, targetCamera.orthographicSize * 2f * OrthographicPadding);
            float width = Mathf.Max(0.01f, height * targetCamera.aspect);
            transform.localScale = new Vector3(width, height, 1f);
            return;
        }

        transform.localScale = new Vector3(FallbackWorldSize, FallbackWorldSize, 1f);
    }
}

// 책임: 마왕 패턴에서 사용하는 임시 VFX 프리팹과 기본 효과를 생성한다.
public static class DemonKingPatternVfx
{
    private const string ExplosionVfxPath = "DemonKing/Vfx/DemonKingExplosionVfx";
    private const string ImpactVfxPath = "DemonKing/Vfx/DemonKingImpactVfx";
    private const string StabVfxPath = "DemonKing/Vfx/DemonKingStabVfx";
    private const string SlashVfxPath = "DemonKing/Vfx/DarkLordSlashVfx";
    private const string GroggyReleaseVfxPath = "DemonKing/Vfx/DarkLordGroggyReleaseVfx";
    private const string EyeFlashVfxPath = "DemonKing/Vfx/DemonKingEyeLightVfx";
    private const string EgoSwordAttackVfxPath = "DemonKing/Vfx/EgoSwordAttackVfx";
    private const string DarkLordExplosion2VfxPath = "DemonKing/Vfx/DarkLordExplosion2Vfx";
    private const string DarkLordFragmentVfxPath = "DemonKing/Vfx/DarkLordFragmentVfx";
    private const string DemonChargeEffectVfxPath = "DemonKing/Vfx/DemonChargeEffectVfx";
    private const string SwordSpinVfxPath = "DemonKing/Vfx/SwordSpin4FrameVfx";
    private const string HighArcDebrisVfxPath = "DemonKing/Vfx/PF_ExplosionDebrisBounce_HighArc";

    private const string IdleStateName = "Idle";
    private const string LoopStateName = "Loop";
    private const string DisappearStateName = "Disappear";

    private const int DefaultSortingOrder = 1;
    private const float StabOriginalVisualLength = 4f;
    private const float StabForwardOffsetRatio = 0.35f;
    private const float FragmentHoldSeconds = 2f;
    private const float FragmentFadeSeconds = 0.5f;

    private static GameObject highArcDebrisPrefab;
    private static bool highArcDebrisMissingLogged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        highArcDebrisPrefab = null;
        highArcDebrisMissingLogged = false;
    }

    public static DemonKingAnimationClipVisual SpawnExplosion(Vector2 center, float diameter)
    {
        return SpawnExplosion(center, new Vector2(diameter, diameter));
    }

    public static DemonKingAnimationClipVisual SpawnExplosion(Vector2 center, Vector2 targetSize)
    {
        SpawnHighArcDebris(center);
        return DemonKingAnimationClipVisual.SpawnOneShot(
            ExplosionVfxPath,
            center,
            targetSize,
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

    public static bool TryPlayCircleTimedHit(
        DemonKingAnimationClipVisual visual,
        DemonKingController owner,
        float diameter,
        CombatHitPayload payload,
        SharedHitRegistry2D sharedHitRegistry,
        Action onHitWindowOpened)
    {
        if (visual == null || owner == null)
            return false;

        return visual.TryPlayTimedCircleHit(
            Mathf.Max(0.01f, diameter),
            owner.TargetMask,
            payload,
            sharedHitRegistry,
            onHitWindowOpened);
    }

    public static bool TryPlayTimedSignal(
        DemonKingAnimationClipVisual visual,
        Action onHitWindowOpened)
    {
        return visual != null && visual.TryPlayTimedSignal(onHitWindowOpened);
    }

    public static DemonKingAnimationClipVisual SpawnDarkLordExplosion2(Vector2 center, float diameter)
    {
        return SpawnDarkLordExplosion2(center, new Vector2(diameter, diameter));
    }

    public static DemonKingAnimationClipVisual SpawnDarkLordExplosion2(Vector2 center, Vector2 targetSize)
    {
        SpawnHighArcDebris(center);
        return DemonKingAnimationClipVisual.SpawnOneShot(
            DarkLordExplosion2VfxPath,
            center,
            targetSize,
            0f,
            "DarkLord_Explosion2Vfx",
            DefaultSortingOrder);
    }

    public static void SpawnDarkLordExplosion2OrFallbackCircle(
        Vector2 center,
        float diameter,
        Color fallbackColor,
        string fallbackName)
    {
        if (SpawnDarkLordExplosion2(center, diameter) == null)
            DemonKingPrimitiveVisual.SpawnCircle(center, diameter, 0.12f, fallbackColor, fallbackName);
    }

    public static DemonKingAnimationClipVisual SpawnImpact(Vector2 center, float diameter, bool leaveFragment = true)
    {
        _ = diameter;
        return SpawnImpact(center, Vector2.zero, leaveFragment);
    }

    public static DemonKingAnimationClipVisual SpawnImpact(Vector2 center, Vector2 targetSize, bool leaveFragment = true)
    {
        SpawnHighArcDebris(center);
        DemonKingAnimationClipVisual impact = DemonKingAnimationClipVisual.SpawnOneShot(
            ImpactVfxPath,
            center,
            targetSize,
            0f,
            "DemonKing_ImpactVfx",
            DefaultSortingOrder);
        if (leaveFragment)
            SpawnTimedFragment(center);
        return impact;
    }

    public static DemonKingAnimationClipVisual SpawnCueOneShot(
        DemonKingVfxCueRef cue,
        Vector2 center,
        float fallbackDiameter,
        Vector2 direction,
        string name = "DemonKing_CueVfx")
    {
        if (cue.PrefabOverride != null)
        {
            SpawnPresentationCompanionsForCue(cue, center);
            Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
            DemonKingAnimationClipVisual overrideVisual = DemonKingAnimationClipVisual.SpawnOneShot(
                cue.PrefabOverride,
                center,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                ResolveCueRotationDeg(cue, safeDirection),
                name,
                DefaultSortingOrder);
            ApplyCueFlip(overrideVisual, cue);
            return overrideVisual;
        }

        DemonKingAnimationClipVisual visual = cue.FallbackKind switch
        {
            DemonKingBuiltInVfxKind.Explosion => SpawnExplosion(center, cue.ResolveScaledTargetSize(fallbackDiameter)),
            DemonKingBuiltInVfxKind.DarkLordExplosion2 => SpawnDarkLordExplosion2(center, cue.ResolveScaledTargetSize(fallbackDiameter)),
            DemonKingBuiltInVfxKind.Impact => SpawnImpact(center, cue.ResolveOptionalScaledTargetSize(fallbackDiameter), cue.LeaveFragment),
            DemonKingBuiltInVfxKind.GroggyRelease => SpawnGroggyRelease(center, cue.ResolveScaledTargetSize(fallbackDiameter)),
            DemonKingBuiltInVfxKind.EyeFlash => DemonKingAnimationClipVisual.SpawnOneShot(
                EyeFlashVfxPath,
                center,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                cue.RotationOffsetDeg,
                name,
                DefaultSortingOrder),
            DemonKingBuiltInVfxKind.Stab => DemonKingAnimationClipVisual.SpawnOneShot(
                StabVfxPath,
                center,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                DemonKingCombatUtil.RotationDeg(direction) + cue.RotationOffsetDeg,
                name,
                DefaultSortingOrder),
            DemonKingBuiltInVfxKind.Slash => DemonKingAnimationClipVisual.SpawnOneShot(
                SlashVfxPath,
                center,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                DemonKingCombatUtil.RotationDeg(direction) + cue.RotationOffsetDeg,
                name,
                DefaultSortingOrder),
            DemonKingBuiltInVfxKind.ChargeDisappear => SpawnChargeDisappear(center, direction),
            DemonKingBuiltInVfxKind.EgoSwordAttack => DemonKingAnimationClipVisual.SpawnOneShot(
                EgoSwordAttackVfxPath,
                center,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                DemonKingCombatUtil.RotationDeg(direction) + cue.RotationOffsetDeg,
                name,
                DefaultSortingOrder),
            DemonKingBuiltInVfxKind.HomingStock => DemonKingAnimationClipVisual.SpawnOneShot(
                "DemonKing/Vfx/HomingMagicBaltStockVfx",
                center,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                cue.RotationOffsetDeg,
                name,
                DefaultSortingOrder),
            DemonKingBuiltInVfxKind.HomingProjectile => DemonKingAnimationClipVisual.SpawnOneShot(
                "DemonKing/Vfx/HomingMagicBaltProjectileVfx",
                center,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                DemonKingCombatUtil.RotationDeg(direction) + cue.RotationOffsetDeg,
                name,
                DefaultSortingOrder),
            _ => null
        };
        ApplyCueFlip(visual, cue);
        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnCueAttachedOneShot(
        DemonKingVfxCueRef cue,
        Transform parent,
        Vector3 localOffset,
        float fallbackDiameter,
        Vector2 direction,
        string name = "DemonKing_AttachedCueVfx")
    {
        if (parent == null)
            return null;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        if (cue.PrefabOverride != null)
        {
            SpawnPresentationCompanionsForCue(cue, parent.TransformPoint(localOffset));
            DemonKingAnimationClipVisual overrideVisual = DemonKingAnimationClipVisual.SpawnAttachedOneShot(
                cue.PrefabOverride,
                parent,
                localOffset,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                ResolveCueRotationDeg(cue, safeDirection),
                name,
                DefaultSortingOrder);
            ApplyCueFlip(overrideVisual, cue);
            return overrideVisual;
        }

        DemonKingAnimationClipVisual visual = cue.FallbackKind switch
        {
            DemonKingBuiltInVfxKind.EgoSwordAttack => SpawnEgoSwordAttack(parent, fallbackDiameter),
            DemonKingBuiltInVfxKind.Stab => SpawnAttachedStab(parent, safeDirection, localOffset),
            DemonKingBuiltInVfxKind.EyeFlash => SpawnEyeFlash(parent, localOffset, cue.ResolveScaledTargetSize(fallbackDiameter)),
            _ => SpawnCueOneShot(
                cue,
                parent.TransformPoint(localOffset),
                fallbackDiameter,
                safeDirection,
                name)
        };
        ApplyCueFlip(visual, cue);
        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnCueFollowingLoop(
        DemonKingVfxCueRef cue,
        Transform target,
        Vector3 localOffset,
        float fallbackDiameter,
        Vector2 direction,
        string name = "DemonKing_FollowingCueVfx")
    {
        if (target == null)
            return null;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        if (cue.PrefabOverride != null)
        {
            DemonKingAnimationClipVisual overrideVisual = DemonKingAnimationClipVisual.SpawnFollowingLoop(
                cue.PrefabOverride,
                target,
                localOffset,
                cue.ResolveScaledTargetSize(fallbackDiameter),
                ResolveCueRotationDeg(cue, safeDirection),
                name,
                DefaultSortingOrder,
                inheritRotation: false,
                LoopStateName);
            ApplyCueFlip(overrideVisual, cue);
            return overrideVisual;
        }

        DemonKingAnimationClipVisual visual = cue.FallbackKind switch
        {
            DemonKingBuiltInVfxKind.ChargeLoop => SpawnChargeLoop(target, safeDirection, localOffset),
            DemonKingBuiltInVfxKind.SwordSpin => SpawnSwordSpinLoop(target, localOffset),
            _ => null
        };
        ApplyCueFlip(visual, cue);
        return visual;
    }

    private static void ApplyCueFlip(DemonKingAnimationClipVisual visual, DemonKingVfxCueRef cue)
    {
        if (visual != null && cue.FlipX)
            visual.SetSpriteFlipX(true);
    }

    private static float ResolveCueRotationDeg(DemonKingVfxCueRef cue, Vector2 direction)
    {
        if (UsesDirectionlessCircularRotation(cue))
            return cue.RotationOffsetDeg;

        return DemonKingCombatUtil.RotationDeg(direction) + cue.RotationOffsetDeg;
    }

    private static bool UsesDirectionlessCircularRotation(DemonKingVfxCueRef cue)
    {
        return cue.FallbackKind == DemonKingBuiltInVfxKind.Explosion ||
               cue.FallbackKind == DemonKingBuiltInVfxKind.DarkLordExplosion2 ||
               cue.FallbackKind == DemonKingBuiltInVfxKind.Impact;
    }

    public static DemonKingAnimationClipVisual SpawnPersistentFragment(Vector2 center, string name = "DarkLord_FragmentVfx")
    {
        return DemonKingAnimationClipVisual.SpawnLoop(
            DarkLordFragmentVfxPath,
            center,
            Vector2.zero,
            0f,
            name,
            DefaultSortingOrder,
            IdleStateName,
            centerOnSpriteBounds: true);
    }

    public static DemonKingAnimationClipVisual SpawnChargeLoop(
        Transform target,
        Vector2 direction,
        Vector3 localOffset = default)
    {
        if (target == null)
            return null;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        return DemonKingAnimationClipVisual.SpawnFollowingLoop(
            DemonChargeEffectVfxPath,
            target,
            new Vector3(localOffset.x, localOffset.y, -0.08f),
            Vector2.zero,
            DemonKingCombatUtil.RotationDeg(safeDirection),
            "DemonKing_ChargeLoopVfx",
            DefaultSortingOrder,
            inheritRotation: false,
            LoopStateName);
    }

    public static DemonKingAnimationClipVisual SpawnChargeDisappear(Vector2 center, Vector2 direction)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        return DemonKingAnimationClipVisual.SpawnOneShot(
            DemonChargeEffectVfxPath,
            center,
            Vector2.zero,
            DemonKingCombatUtil.RotationDeg(safeDirection),
            "DemonKing_ChargeDisappearVfx",
            DefaultSortingOrder,
            DisappearStateName);
    }

    public static bool TryPlayChargeDisappearFromLoop(
        DemonKingAnimationClipVisual chargeLoop,
        DemonKingVfxCueRef cue,
        float fallbackDiameter,
        Vector2 direction)
    {
        if (chargeLoop == null)
            return false;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        return chargeLoop.TryDetachAndPlayOneShotState(
            DisappearStateName,
            cue.ResolveScaledTargetSize(fallbackDiameter),
            DemonKingCombatUtil.RotationDeg(safeDirection) + cue.RotationOffsetDeg);
    }

    public static DemonKingAnimationClipVisual SpawnSwordSpinLoop(Transform target)
    {
        return SpawnSwordSpinLoop(target, Vector3.zero);
    }

    public static DemonKingAnimationClipVisual SpawnSwordSpinLoop(Transform target, Vector3 localOffset)
    {
        if (target == null)
            return null;

        return DemonKingAnimationClipVisual.SpawnFollowingLoop(
            SwordSpinVfxPath,
            target,
            new Vector3(localOffset.x, localOffset.y, -0.08f),
            Vector2.zero,
            0f,
            "EgoSword_SpinVfx",
            DefaultSortingOrder,
            inheritRotation: false,
            LoopStateName);
    }

    public static DemonKingAnimationClipVisual SpawnAttachedStab(
        Transform parent,
        Vector2 direction,
        Vector3 localOffset = default)
    {
        if (parent == null)
            return null;

        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        Vector3 localPosition = new(
            localOffset.x + safeDirection.x * (StabOriginalVisualLength * StabForwardOffsetRatio),
            localOffset.y + safeDirection.y * (StabOriginalVisualLength * StabForwardOffsetRatio),
            -0.05f);

        return DemonKingAnimationClipVisual.SpawnAttachedOneShot(
            StabVfxPath,
            parent,
            localPosition,
            Vector2.zero,
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

        DemonKingAnimationClipVisual visual = DemonKingAnimationClipVisual.SpawnOneShot(
            SlashVfxPath,
            center,
            size,
            DemonKingCombatUtil.RotationDeg(safeDirection),
            "DarkLord_SlashVfx",
            DefaultSortingOrder);
        visual?.SetSpriteFlipX(true);
        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnGroggyRelease(Vector2 center, float diameter)
    {
        return SpawnGroggyRelease(center, new Vector2(diameter, diameter));
    }

    public static DemonKingAnimationClipVisual SpawnGroggyRelease(Vector2 center, Vector2 targetSize)
    {
        return DemonKingAnimationClipVisual.SpawnOneShot(
            GroggyReleaseVfxPath,
            center,
            targetSize,
            0f,
            "DarkLord_GroggyReleaseVfx",
            DefaultSortingOrder);
    }

    public static DemonKingAnimationClipVisual SpawnEyeFlash(Transform parent, Vector2 localOffset, Vector2 size)
    {
        return DemonKingAnimationClipVisual.SpawnAttachedOneShot(
            EyeFlashVfxPath,
            parent,
            new Vector3(localOffset.x, localOffset.y, -0.07f),
            size,
            0f,
            "DemonKing_EyeLightVfx",
            DefaultSortingOrder,
            warnIfMissing: false);
    }

    public static DemonKingAnimationClipVisual SpawnEgoSwordAttack(Transform parent, float diameter)
    {
        _ = diameter;
        return DemonKingAnimationClipVisual.SpawnAttachedOneShot(
            EgoSwordAttackVfxPath,
            parent,
            new Vector3(0f, 0f, -0.06f),
            Vector2.zero,
            0f,
            "EgoSword_AttackVfx",
            DefaultSortingOrder);
    }

    private static void SpawnTimedFragment(Vector2 center)
    {
        DemonKingAnimationClipVisual fragment = SpawnPersistentFragment(center, "DarkLord_TimedFragmentVfx");
        fragment?.ReleaseAfterDelayAndFade(FragmentHoldSeconds, FragmentFadeSeconds);
    }

    private static void SpawnPresentationCompanionsForCue(DemonKingVfxCueRef cue, Vector2 center)
    {
        switch (cue.FallbackKind)
        {
            case DemonKingBuiltInVfxKind.Explosion:
            case DemonKingBuiltInVfxKind.DarkLordExplosion2:
                SpawnHighArcDebris(center);
                break;
            case DemonKingBuiltInVfxKind.Impact:
                SpawnHighArcDebris(center);
                if (cue.LeaveFragment)
                    SpawnTimedFragment(center);
                break;
        }
    }

    public static void SpawnHighArcDebris(Vector2 center)
    {
        GameObject prefab = ResolveHighArcDebrisPrefab();
        if (prefab == null)
            return;

        SpawnedPresentationHook hook = new()
        {
            prefab = prefab,
            scaleMultiplier = Vector3.one,
            anchorMode = PresentationSpawnAnchorMode.ContextPosition,
            scaleMode = PresentationSpawnScaleMode.None,
            boundsMode = PresentationTargetBoundsMode.RendererAabb,
            lifetimeMode = PresentationLifetimeMode.AutoDetect,
        };
        WorldPresentationContext context = WorldPresentationContext.AtWorld(
            null,
            new Vector3(center.x, center.y, 0f),
            Vector3.up,
            sourceObject: prefab);
        WorldPresentationPlayback.SpawnOneShot(hook, context);
    }

    private static GameObject ResolveHighArcDebrisPrefab()
    {
        if (highArcDebrisPrefab != null)
            return highArcDebrisPrefab;

        highArcDebrisPrefab = Resources.Load<GameObject>(HighArcDebrisVfxPath);
        if (highArcDebrisPrefab == null && !highArcDebrisMissingLogged)
        {
            highArcDebrisMissingLogged = true;
            Debug.LogWarning($"DemonKing debris VFX prefab not found at Resources/{HighArcDebrisVfxPath}.");
        }

        return highArcDebrisPrefab;
    }
}

[DisallowMultipleComponent]
// 책임: 마왕 패턴용 애니메이션 클립 비주얼을 생성하고 재생 수명과 정렬을 관리한다.
public sealed class DemonKingAnimationClipVisual : MonoBehaviour
{
    private const float VisualZ = -0.06f;
    private const float MinimumOneShotLifetimeSeconds = 0.12f;
    private const string OneShotStateName = "Play";

    private static readonly System.Collections.Generic.Dictionary<string, GameObject> PrefabCache = new();
    private static readonly System.Collections.Generic.HashSet<string> MissingPrefabWarnings = new();
    private static readonly System.Collections.Generic.HashSet<string> InvalidPrefabWarnings = new();

    private SpriteRenderer spriteRenderer;
    private Animator animator;
    private Coroutine playbackRoutine;
    private string sourceResourcePath;
    private Transform followTarget;
    private Vector3 followLocalOffset;
    private bool usesFollowTarget;
    private bool followRotation;
    private float followRotationDeg;

    public bool IsPlaying { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        PrefabCache.Clear();
        MissingPrefabWarnings.Clear();
        InvalidPrefabWarnings.Clear();
    }

    public static DemonKingAnimationClipVisual SpawnOneShot(
        string resourcePath,
        Vector2 center,
        Vector2 targetSize,
        float rotationDeg,
        string name,
        int sortingOrder,
        string stateName = OneShotStateName,
        bool centerOnSpriteBounds = false)
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

        if (!visual.TryPlayOneShot(targetSize, stateName, centerOnSpriteBounds))
            return null;

        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnOneShot(
        GameObject prefab,
        Vector2 center,
        Vector2 targetSize,
        float rotationDeg,
        string name,
        int sortingOrder,
        string stateName = OneShotStateName,
        bool centerOnSpriteBounds = false)
    {
        DemonKingAnimationClipVisual visual = InstantiateVisual(
            prefab,
            null,
            new Vector3(center.x, center.y, VisualZ),
            rotationDeg,
            name,
            sortingOrder);
        if (visual == null)
            return null;

        if (!visual.TryPlayOneShot(targetSize, stateName, centerOnSpriteBounds))
            return null;

        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnLoop(
        string resourcePath,
        Vector2 center,
        Vector2 targetSize,
        float rotationDeg,
        string name,
        int sortingOrder,
        string stateName,
        bool centerOnSpriteBounds = false)
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

        if (!visual.TryPlayLoop(targetSize, stateName, centerOnSpriteBounds))
            return null;

        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnFollowingLoop(
        string resourcePath,
        Transform target,
        Vector3 localOffset,
        Vector2 targetSize,
        float rotationDeg,
        string name,
        int sortingOrder,
        bool inheritRotation,
        string stateName)
    {
        if (target == null)
            return null;

        Transform parent = inheritRotation ? target : null;
        Vector3 position = inheritRotation ? localOffset : target.TransformPoint(localOffset);
        DemonKingAnimationClipVisual visual = InstantiateVisual(
            resourcePath,
            parent,
            position,
            rotationDeg,
            name,
            sortingOrder);
        if (visual == null)
            return null;

        if (!visual.TryPlayLoop(targetSize, stateName, centerOnSpriteBounds: false))
            return null;

        visual.followTarget = target;
        visual.followLocalOffset = localOffset;
        visual.usesFollowTarget = true;
        visual.followRotation = inheritRotation;
        visual.followRotationDeg = rotationDeg;
        visual.UpdateFollowTransform();
        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnFollowingLoop(
        GameObject prefab,
        Transform target,
        Vector3 localOffset,
        Vector2 targetSize,
        float rotationDeg,
        string name,
        int sortingOrder,
        bool inheritRotation,
        string stateName)
    {
        if (target == null || prefab == null)
            return null;

        Transform parent = inheritRotation ? target : null;
        Vector3 position = inheritRotation ? localOffset : target.TransformPoint(localOffset);
        DemonKingAnimationClipVisual visual = InstantiateVisual(
            prefab,
            parent,
            position,
            rotationDeg,
            name,
            sortingOrder);
        if (visual == null)
            return null;

        if (!visual.TryPlayLoop(targetSize, stateName, centerOnSpriteBounds: false))
            return null;

        visual.followTarget = target;
        visual.followLocalOffset = localOffset;
        visual.usesFollowTarget = true;
        visual.followRotation = inheritRotation;
        visual.followRotationDeg = rotationDeg;
        visual.UpdateFollowTransform();
        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnAttachedOneShot(
        string resourcePath,
        Transform parent,
        Vector3 localPosition,
        Vector2 targetSize,
        float localRotationDeg,
        string name,
        int sortingOrder,
        bool warnIfMissing = true)
    {
        if (parent == null)
            return null;

        DemonKingAnimationClipVisual visual = InstantiateVisual(
            resourcePath,
            parent,
            localPosition,
            localRotationDeg,
            name,
            sortingOrder,
            warnIfMissing);
        if (visual == null)
            return null;

        if (!visual.TryPlayOneShot(targetSize, OneShotStateName, centerOnSpriteBounds: false))
            return null;

        return visual;
    }

    public static DemonKingAnimationClipVisual SpawnAttachedOneShot(
        GameObject prefab,
        Transform parent,
        Vector3 localPosition,
        Vector2 targetSize,
        float localRotationDeg,
        string name,
        int sortingOrder,
        bool warnIfMissing = true)
    {
        _ = warnIfMissing;
        if (parent == null || prefab == null)
            return null;

        DemonKingAnimationClipVisual visual = InstantiateVisual(
            prefab,
            parent,
            localPosition,
            localRotationDeg,
            name,
            sortingOrder);
        if (visual == null)
            return null;

        if (!visual.TryPlayOneShot(targetSize, OneShotStateName, centerOnSpriteBounds: false))
            return null;

        return visual;
    }

    public void StopAndRelease()
    {
        StopPlayback();
        Destroy(gameObject);
    }

    public bool TryDetachAndPlayOneShotState(string stateName, Vector2 targetSize, float rotationDeg)
    {
        usesFollowTarget = false;
        followTarget = null;
        followRotation = false;
        transform.SetParent(null, true);
        transform.rotation = Quaternion.Euler(0f, 0f, rotationDeg);
        return TryPlayOneShot(targetSize, stateName, centerOnSpriteBounds: false);
    }

    public void ReleaseAfterDelayAndFade(float delaySeconds, float fadeSeconds)
    {
        StopPlayback();
        playbackRoutine = StartCoroutine(CoReleaseAfterDelayAndFade(delaySeconds, fadeSeconds));
    }

    public void FadeAndRelease(float fadeSeconds)
    {
        ReleaseAfterDelayAndFade(0f, fadeSeconds);
    }

    public bool TryPlayTimedCircleHit(
        float worldDiameter,
        LayerMask targetLayers,
        CombatHitPayload payload,
        SharedHitRegistry2D sharedHitRegistry,
        Action onHitWindowOpened)
    {
        ITimedHitEffect2D timedHitEffect = GetComponentInChildren<ITimedHitEffect2D>(true);
        if (timedHitEffect == null)
            return false;

        CircleCollider2D hitCollider = ConfigureCircleHitCollider(worldDiameter);
        timedHitEffect.ConfigureHitCollision(new Collider2D[] { hitCollider }, targetLayers);
        timedHitEffect.Play(
            ResolveLongestClipLength(),
            payload,
            sharedHitRegistry,
            onHitWindowOpened);
        return true;
    }

    public bool TryPlayTimedSignal(Action onHitWindowOpened)
    {
        ITimedHitEffect2D timedHitEffect = GetComponentInChildren<ITimedHitEffect2D>(true);
        if (timedHitEffect == null)
            return false;

        DisableAllHitColliders();
        LayerMask emptyMask = default;
        timedHitEffect.ConfigureHitCollision(Array.Empty<Collider2D>(), emptyMask);
        timedHitEffect.Play(
            ResolveLongestClipLength(),
            null,
            null,
            onHitWindowOpened);
        return true;
    }

    public void SetSpriteFlipX(bool flipX)
    {
        CacheRuntimeReferences();
        if (spriteRenderer != null)
            spriteRenderer.flipX = flipX;
    }

    private static DemonKingAnimationClipVisual InstantiateVisual(
        string resourcePath,
        Transform parent,
        Vector3 position,
        float rotationDeg,
        string name,
        int sortingOrder,
        bool warnIfMissing = true)
    {
        GameObject prefab = LoadPrefab(resourcePath, warnIfMissing);
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
        visual.sourceResourcePath = resourcePath;
        return visual;
    }

    private static DemonKingAnimationClipVisual InstantiateVisual(
        GameObject prefab,
        Transform parent,
        Vector3 position,
        float rotationDeg,
        string name,
        int sortingOrder)
    {
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
        visual.sourceResourcePath = prefab.name;
        return visual;
    }

    private bool TryPlayOneShot(Vector2 targetSize, string stateName, bool centerOnSpriteBounds)
    {
        StopPlayback();
        if (!ValidatePlayable(stateName))
        {
            StopAndRelease();
            return false;
        }

        IsPlaying = true;
        PlayAnimatorState(stateName);
        ApplyTargetScale(targetSize);
        if (centerOnSpriteBounds)
            CenterOnSpriteBounds();
        playbackRoutine = StartCoroutine(CoDestroyAfterSeconds(ResolveLongestClipLength()));
        return true;
    }

    private CircleCollider2D ConfigureCircleHitCollider(float worldDiameter)
    {
        DisableAllHitColliders();
        CircleCollider2D hitCollider = GetComponent<CircleCollider2D>();
        if (hitCollider == null)
            hitCollider = gameObject.AddComponent<CircleCollider2D>();

        hitCollider.isTrigger = true;
        hitCollider.offset = Vector2.zero;
        hitCollider.radius = ResolveLocalCircleRadius(worldDiameter);
        hitCollider.enabled = false;
        return hitCollider;
    }

    private void DisableAllHitColliders()
    {
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            if (colliders[i] != null)
                colliders[i].enabled = false;
        }
    }

    private float ResolveLocalCircleRadius(float worldDiameter)
    {
        float safeWorldDiameter = Mathf.Max(0.01f, worldDiameter);
        Vector3 scale = transform.lossyScale;
        float maxScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y), 0.01f);
        return (safeWorldDiameter * 0.5f) / maxScale;
    }

    private bool TryPlayLoop(Vector2 targetSize, string stateName, bool centerOnSpriteBounds)
    {
        StopPlayback();
        if (!ValidatePlayable(stateName))
        {
            StopAndRelease();
            return false;
        }

        IsPlaying = true;
        PlayAnimatorState(stateName);
        ApplyTargetScale(targetSize);
        if (centerOnSpriteBounds)
            CenterOnSpriteBounds();
        return true;
    }

    private IEnumerator CoDestroyAfterSeconds(float seconds)
    {
        yield return new WaitForSeconds(Mathf.Max(0.01f, seconds));
        IsPlaying = false;
        playbackRoutine = null;
        Destroy(gameObject);
    }

    private IEnumerator CoReleaseAfterDelayAndFade(float delaySeconds, float fadeSeconds)
    {
        float safeDelay = Mathf.Max(0f, delaySeconds);
        if (safeDelay > 0f)
            yield return new WaitForSeconds(safeDelay);

        CacheRuntimeReferences();
        float safeFade = Mathf.Max(0f, fadeSeconds);
        if (spriteRenderer != null && safeFade > 0f)
        {
            Color startColor = spriteRenderer.color;
            float elapsed = 0f;
            while (elapsed < safeFade)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / safeFade);
                Color color = startColor;
                color.a = Mathf.Lerp(startColor.a, 0f, t);
                spriteRenderer.color = color;
                yield return null;
            }
        }

        IsPlaying = false;
        playbackRoutine = null;
        Destroy(gameObject);
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

    private void CenterOnSpriteBounds()
    {
        if (spriteRenderer == null || spriteRenderer.sprite == null)
            return;

        transform.position -= transform.TransformVector(spriteRenderer.sprite.bounds.center);
    }

    private void LateUpdate()
    {
        UpdateFollowTransform();
    }

    private void UpdateFollowTransform()
    {
        if (!usesFollowTarget)
            return;

        if (followTarget == null)
        {
            StopAndRelease();
            return;
        }

        if (followRotation)
            return;

        transform.position = followTarget.TransformPoint(followLocalOffset);
        transform.rotation = Quaternion.Euler(0f, 0f, followRotationDeg);
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
        if (!ValidateAnimatorState(stateName, out int stateHash))
            return false;

        animator.Play(stateHash, 0, 0f);
        animator.Update(0f);
        return true;
    }

    private bool ValidatePlayable(string stateName)
    {
        CacheRuntimeReferences();
        if (spriteRenderer == null)
        {
            WarnInvalid("missing SpriteRenderer");
            return false;
        }

        if (spriteRenderer.sprite == null)
        {
            WarnInvalid("SpriteRenderer has no sprite");
            return false;
        }

        return ValidateAnimatorState(stateName);
    }

    private bool ValidateAnimatorState(string stateName)
    {
        return ValidateAnimatorState(stateName, out _);
    }

    private bool ValidateAnimatorState(string stateName, out int stateHash)
    {
        CacheRuntimeReferences();
        stateHash = 0;
        if (animator == null)
        {
            WarnInvalid("missing Animator");
            return false;
        }

        if (animator.runtimeAnimatorController == null)
        {
            WarnInvalid("Animator has no RuntimeAnimatorController");
            return false;
        }

        if (!TryResolveStateHash(stateName, out stateHash))
        {
            WarnInvalid($"AnimatorController has no state '{stateName}'");
            return false;
        }

        return true;
    }

    private bool TryResolveStateHash(string stateName, out int stateHash)
    {
        stateHash = Animator.StringToHash(stateName);
        if (animator.HasState(0, stateHash))
            return true;

        if (animator.layerCount <= 0)
            return false;

        string layerName = animator.GetLayerName(0);
        if (string.IsNullOrWhiteSpace(layerName))
            return false;

        stateHash = Animator.StringToHash($"{layerName}.{stateName}");
        return animator.HasState(0, stateHash);
    }

    private void WarnInvalid(string reason)
    {
        string resourcePath = string.IsNullOrWhiteSpace(sourceResourcePath) ? gameObject.name : sourceResourcePath;
        string key = $"{resourcePath}:{reason}";
        if (InvalidPrefabWarnings.Add(key))
            Debug.LogWarning($"DemonKing VFX at Resources/{resourcePath} is invalid: {reason}.", this);
    }

    private float ResolveLongestClipLength()
    {
        AnimationClip[] clips = ResolveClips();
        float length = MinimumOneShotLifetimeSeconds;
        for (int i = 0; i < clips.Length; i++)
            length = Mathf.Max(length, clips[i] != null ? clips[i].length : 0f);

        return length;
    }

    private AnimationClip[] ResolveClips()
    {
        CacheRuntimeReferences();
        return animator != null && animator.runtimeAnimatorController != null
            ? animator.runtimeAnimatorController.animationClips
            : System.Array.Empty<AnimationClip>();
    }

    private static GameObject LoadPrefab(string resourcePath, bool warnIfMissing = true)
    {
        if (string.IsNullOrWhiteSpace(resourcePath))
            return null;

        if (PrefabCache.TryGetValue(resourcePath, out GameObject cachedPrefab) && cachedPrefab != null)
            return cachedPrefab;

        GameObject prefab = Resources.Load<GameObject>(resourcePath);
        if (prefab != null)
        {
            PrefabCache[resourcePath] = prefab;
            MissingPrefabWarnings.Remove(resourcePath);
        }
        else
        {
            PrefabCache.Remove(resourcePath);
            if (warnIfMissing && MissingPrefabWarnings.Add(resourcePath))
                Debug.LogWarning($"DemonKing VFX prefab not found at Resources/{resourcePath}.");
        }

        return prefab;
    }
}
