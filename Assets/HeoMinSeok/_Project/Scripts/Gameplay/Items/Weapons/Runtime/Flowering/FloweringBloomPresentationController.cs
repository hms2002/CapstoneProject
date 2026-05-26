using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

public sealed class FloweringBloomPresentationController : MonoBehaviour
{
    private const int FallbackSortingOrder = 32000;
    private const int SortingOrderOffset = 950;
    private const int RevealMaskTextureSize = 16;

    private static Sprite revealMaskSprite;
    private GameObject owner;
    private FloweringBloomData data;
    private GameObject overlayRoot;
    private RectTransform overlayRect;
    private Canvas overlayCanvas;
    private AffectionGradientBorderGraphic borderGraphic;
    private FloweringWorldDimOverlay worldDimOverlay;
    private FloweringEyeFlashPlayer eyeFlashPlayer;
    private FloweringPlayerPixelOutline playerOutline;
    private GameObject playerCutInRoot;
    private SpriteRenderer playerCutInRenderer;
    private readonly List<WeaponSpriteEntry> weaponSpriteEntries = new();
    private readonly List<WeaponRevealOverlay> weaponRevealOverlays = new();
    private readonly List<GameObject> weaponRevealParticleObjects = new();
    private readonly List<GameObject> finalShakeParticleObjects = new();
    private readonly List<PlayerTintEntry> playerTintEntries = new();
    private CinemachineCamera zoomCamera;
    private Coroutine weaponRevealCoroutine;
    private Coroutine playerCutInCoroutine;
    private bool hasCachedCameraLens;
    private bool cachedLensOrthographic;
    private bool weaponBloomSpriteApplied;
    private bool playerCutInApplied;
    private bool eyeFlashStartedDuringCutIn;
    private float cachedOrthographicSize;
    private float cachedFieldOfView;
    private float borderReveal;
    private Color playerCutInStartColor = Color.white;

    private struct WeaponSpriteEntry
    {
        public SpriteRenderer Renderer;
        public Sprite OriginalSprite;
    }

    private struct WeaponRevealOverlay
    {
        public GameObject Root;
        public SpriteRenderer Renderer;
        public SpriteMask Mask;
        public Vector2 Direction;
        public Vector2 Perpendicular;
        public float MinDirection;
        public float MaxDirection;
        public float MinPerpendicular;
        public float MaxPerpendicular;
    }

    private struct PlayerTintEntry
    {
        public SpriteRenderer Renderer;
        public Color OriginalColor;
        public bool OriginalEnabled;
    }

    public void Initialize(GameObject ownerObject, FloweringBloomData bloomData)
    {
        owner = ownerObject;
        data = bloomData;
    }

    public IEnumerator PlayCutIn(AbilitySystem system, AbilitySpec spec, FloweringBloomData bloomData)
    {
        data = bloomData;
        EnsureOverlay();
        EnsureWorldDim();
        StartWeaponRevealOnAnimationEvent(system, spec);
        StartPlayerCutInVisual();
        SetWorldDimAlpha(0f);
        SetBorderIntensity(1f);
        SetBorderReveal(0f);
        CacheCameraLens();

        PlayShake(system, data.OpeningShakeAmplitude, "Flowering Bloom open");
        AbilityAudioRouter.PlayOneShotAtPosition(
            data.CutInOpenSound,
            system,
            spec,
            ResolvePlayerCenter(),
            data);

        playerCutInCoroutine = StartCoroutine(PlayPlayerTintInAndEyeFlash(spec));
        Coroutine tintIn = playerCutInCoroutine;
        yield return FadeWorldDimAndCameraZoom(
            system,
            0f,
            data.DimTargetAlpha,
            data.FadeInSeconds,
            zoomIn: true,
            data.ZoomInSeconds,
            spec);
        if (tintIn != null)
            yield return tintIn;

        yield return RevealBorder(data.ScreenBorderRevealSeconds, spec);
        yield return WaitUnscaled(data.HoldSeconds, spec);

        playerCutInCoroutine = StartCoroutine(PlayPlayerTintOut(spec));
        Coroutine tintOut = playerCutInCoroutine;
        yield return FadeWorldDimAndCameraZoom(
            system,
            data.DimTargetAlpha,
            0f,
            data.FadeOutSeconds,
            zoomIn: false,
            data.ZoomOutSeconds,
            spec);
        if (tintOut != null)
            yield return tintOut;

        StopEyeFlash();
        RestorePlayerCutInVisual();
        SpawnFinalShakeParticle();
        AbilityAudioRouter.PlayOneShotAtPosition(
            data.FinalShakeSound,
            system,
            spec,
            ResolvePlayerCenter(),
            data);
        PlayShake(system, data.FinalShakeAmplitude, "Flowering Bloom close");
        SetWorldDimAlpha(0f);
        RestoreCameraZoom();
    }

    public IEnumerator PlayBloomEndTransition(AbilitySpec spec, FloweringBloomData bloomData)
    {
        data = bloomData;
        StopWeaponReveal();
        yield return PlayWeaponReveal(active: false, spec, ResolveAbilitySystem());
    }

    public void BeginActiveBloom(FloweringBloomData bloomData)
    {
        data = bloomData;
        EnsureOverlay();
        ApplyWeaponBloomSprite(true);
        SetWorldDimAlpha(0f);
        SetBorderIntensity(1f);
        SetBorderReveal(1f);
        EnsurePlayerOutline();
    }

    public void Release()
    {
        StopWeaponReveal();
        ClearFinalShakeParticles();
        RestoreWeaponSprite();
        StopEyeFlash();
        RestorePlayerCutInVisual();
        ReleaseWorldDim();
        RestoreCameraZoom();

        if (playerOutline != null)
            playerOutline.Release();

        playerOutline = null;

        if (overlayRoot != null)
            Destroy(overlayRoot);

        overlayRoot = null;
        overlayRect = null;
        overlayCanvas = null;
        borderGraphic = null;
        borderReveal = 0f;
    }

    private void LateUpdate()
    {
        if (overlayRoot != null)
            ApplyOverlayRect();

        if (worldDimOverlay != null)
            worldDimOverlay.Refresh();
    }

    private void EnsureOverlay()
    {
        if (overlayRoot != null)
        {
            ConfigureBorderGraphic();
            return;
        }

        Canvas targetCanvas = ResolveTargetCanvas();
        overlayRoot = new GameObject("[FloweringBloomOverlay]", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        overlayRect = overlayRoot.transform as RectTransform;

        if (targetCanvas != null)
            overlayRoot.transform.SetParent(targetCanvas.transform, false);

        overlayCanvas = overlayRoot.GetComponent<Canvas>();
        ConfigureCanvas(targetCanvas);

        CanvasGroup canvasGroup = overlayRoot.GetComponent<CanvasGroup>();
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject borderObject = new("GradientBorder", typeof(RectTransform), typeof(CanvasRenderer), typeof(AffectionGradientBorderGraphic));
        borderObject.transform.SetParent(overlayRoot.transform, false);
        ApplyStretchRect(borderObject.transform as RectTransform);

        borderGraphic = borderObject.GetComponent<AffectionGradientBorderGraphic>();
        borderGraphic.raycastTarget = false;
        TryDisableTransparentCull(borderGraphic);
        ConfigureBorderGraphic();

        ApplyOverlayRect();
    }

    private void ConfigureCanvas(Canvas targetCanvas)
    {
        if (overlayCanvas == null)
            return;

        if (targetCanvas != null)
        {
            overlayCanvas.renderMode = targetCanvas.renderMode;
            overlayCanvas.worldCamera = targetCanvas.worldCamera;
            overlayCanvas.planeDistance = targetCanvas.planeDistance;
            overlayCanvas.sortingLayerID = targetCanvas.sortingLayerID;
            overlayCanvas.sortingOrder = targetCanvas.sortingOrder + SortingOrderOffset;
        }
        else
        {
            overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            overlayCanvas.sortingOrder = FallbackSortingOrder;
        }

        overlayCanvas.overrideSorting = true;
    }

    private void ConfigureBorderGraphic()
    {
        if (borderGraphic == null)
            return;

        if (data != null && data.ScreenBorderMaterial != null)
            borderGraphic.material = data.ScreenBorderMaterial;

        Color color = data != null ? data.BloomColor : Color.red;
        borderGraphic.color = color;
        borderGraphic.ConfigureShape(data != null ? data.ScreenBorderThicknessRatio : 0.2f, 0f);
        borderGraphic.RevealProgress = borderReveal;
    }

    private static Canvas ResolveTargetCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Canvas selected = null;
        int selectedOrder = int.MinValue;

        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas == null || !canvas.isActiveAndEnabled || !canvas.isRootCanvas)
                continue;

            if (canvas.renderMode == RenderMode.WorldSpace)
                continue;

            if (selected == null || canvas.sortingOrder > selectedOrder)
            {
                selected = canvas;
                selectedOrder = canvas.sortingOrder;
            }
        }

        return selected;
    }

    private void ApplyOverlayRect()
    {
        if (overlayRect == null)
            return;

        ApplyStretchRect(overlayRect);

        if (borderGraphic != null)
        {
            ApplyStretchRect(borderGraphic.transform as RectTransform);
            borderGraphic.SetVerticesDirty();
        }
    }

    private static void ApplyStretchRect(RectTransform rectTransform)
    {
        if (rectTransform == null)
            return;

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.localScale = Vector3.one;
    }

    private void EnsureWorldDim()
    {
        if (data == null)
            return;

        if (worldDimOverlay == null)
        {
            GameObject dimObject = new("[FloweringWorldDimPanel]");
            worldDimOverlay = dimObject.AddComponent<FloweringWorldDimOverlay>();
        }

        Transform fallbackAnchor = owner != null ? owner.transform : transform;
        worldDimOverlay.Initialize(data, fallbackAnchor);
    }

    private void ReleaseWorldDim()
    {
        if (worldDimOverlay == null)
            return;

        worldDimOverlay.Release();
        worldDimOverlay = null;
    }

    private void SetWorldDimAlpha(float alpha)
    {
        if (worldDimOverlay == null && alpha > 0.001f)
            EnsureWorldDim();

        if (worldDimOverlay != null)
            worldDimOverlay.SetAlpha(alpha);
    }

    private void StartEyeFlash()
    {
        if (owner == null || data == null || data.EyeFlashFrames == null || data.EyeFlashFrames.Length == 0)
            return;

        StopEyeFlash();

        if (eyeFlashPlayer == null)
        {
            GameObject eyeObject = new("[FloweringEyeFlash]");
            eyeObject.transform.SetParent(owner.transform, false);
            eyeFlashPlayer = eyeObject.AddComponent<FloweringEyeFlashPlayer>();
        }

        Transform eyeTransform = eyeFlashPlayer.transform;
        eyeTransform.SetParent(owner.transform, false);
        eyeTransform.localPosition = new Vector3(data.EyeFlashLocalOffset.x, data.EyeFlashLocalOffset.y, -0.05f);
        eyeTransform.localRotation = Quaternion.identity;
        eyeTransform.localScale = Vector3.one * data.EyeFlashScale;

        eyeFlashPlayer.Initialize(
            data.EyeFlashFrames,
            data.EyeFlashFps,
            data.BloomColor,
            data.EyeFlashSortingLayerName,
            data.EyeFlashSortingOrder);
        eyeFlashPlayer.Completed += HandleEyeFlashCompleted;
    }

    private void StopEyeFlash()
    {
        if (eyeFlashPlayer == null)
            return;

        eyeFlashPlayer.Completed -= HandleEyeFlashCompleted;
        Destroy(eyeFlashPlayer.gameObject);
        eyeFlashPlayer = null;
    }

    private void HandleEyeFlashCompleted(FloweringEyeFlashPlayer player)
    {
        if (player != eyeFlashPlayer)
            return;

        StopEyeFlash();
    }

    private void StartPlayerCutInVisual()
    {
        if (playerCutInApplied || owner == null || data == null)
            return;

        SpriteRenderer bodyRenderer = ResolvePlayerBodyRenderer();
        Sprite baseSprite = data.PlayerCutInBaseSprite != null
            ? data.PlayerCutInBaseSprite
            : bodyRenderer != null
                ? bodyRenderer.sprite
                : null;
        if (baseSprite == null)
            return;

        playerTintEntries.Clear();
        CapturePlayerCutInRenderers(bodyRenderer);
        if (playerTintEntries.Count == 0)
            return;

        playerCutInStartColor = bodyRenderer != null ? bodyRenderer.color : Color.white;
        playerCutInStartColor.a = Mathf.Max(0.001f, playerCutInStartColor.a);

        Transform parent = bodyRenderer != null && bodyRenderer.transform.parent != null
            ? bodyRenderer.transform.parent
            : owner.transform;

        playerCutInRoot = new GameObject("[FloweringCutInPlayerSilhouette]");
        playerCutInRoot.transform.SetParent(parent, false);

        if (bodyRenderer != null)
        {
            Vector3 offset = new(data.PlayerCutInLocalOffset.x, data.PlayerCutInLocalOffset.y, 0f);
            playerCutInRoot.transform.localPosition = bodyRenderer.transform.localPosition + offset;
            playerCutInRoot.transform.localRotation = bodyRenderer.transform.localRotation;
            playerCutInRoot.transform.localScale = bodyRenderer.transform.localScale * data.PlayerCutInScale;
        }
        else
        {
            playerCutInRoot.transform.localPosition = new Vector3(data.PlayerCutInLocalOffset.x, data.PlayerCutInLocalOffset.y, 0f);
            playerCutInRoot.transform.localRotation = Quaternion.identity;
            playerCutInRoot.transform.localScale = Vector3.one * data.PlayerCutInScale;
        }

        playerCutInRenderer = playerCutInRoot.AddComponent<SpriteRenderer>();
        playerCutInRenderer.sprite = baseSprite;
        playerCutInRenderer.color = playerCutInStartColor;
        playerCutInRenderer.maskInteraction = SpriteMaskInteraction.None;

        if (bodyRenderer != null)
        {
            playerCutInRenderer.flipX = bodyRenderer.flipX;
            playerCutInRenderer.flipY = bodyRenderer.flipY;
            playerCutInRenderer.sortingLayerID = bodyRenderer.sortingLayerID;
            playerCutInRenderer.sortingOrder = bodyRenderer.sortingOrder;
            playerCutInRenderer.sharedMaterial = bodyRenderer.sharedMaterial;
        }
        else
        {
            playerCutInRenderer.sortingLayerName = data.EyeFlashSortingLayerName;
            playerCutInRenderer.sortingOrder = Mathf.Max(0, data.EyeFlashSortingOrder - 1);
        }

        SetPlayerCutInRenderersVisible(false);
        playerCutInApplied = true;
        eyeFlashStartedDuringCutIn = false;
    }

    private SpriteRenderer ResolvePlayerBodyRenderer()
    {
        if (owner == null)
            return null;

        Transform[] transforms = owner.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform child = transforms[i];
            if (child != null &&
                child.name == "PlayerRender" &&
                child.TryGetComponent(out SpriteRenderer renderer) &&
                renderer.sprite != null)
            {
                return renderer;
            }
        }

        SpriteRenderer[] renderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null || !renderer.gameObject.activeInHierarchy)
                continue;

            if (ShouldSkipPlayerCutInRenderer(renderer))
                continue;

            return renderer;
        }

        return null;
    }

    private void CapturePlayerCutInRenderers(SpriteRenderer preferredRenderer)
    {
        if (preferredRenderer != null)
        {
            AddPlayerCutInRenderer(preferredRenderer);
            return;
        }

        SpriteRenderer[] renderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || renderer.sprite == null || !renderer.gameObject.activeInHierarchy)
                continue;

            if (ShouldSkipPlayerCutInRenderer(renderer))
                continue;

            AddPlayerCutInRenderer(renderer);
        }
    }

    private void AddPlayerCutInRenderer(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        playerTintEntries.Add(new PlayerTintEntry
        {
            Renderer = renderer,
            OriginalColor = renderer.color,
            OriginalEnabled = renderer.enabled
        });
    }

    private bool ShouldSkipPlayerCutInRenderer(SpriteRenderer renderer)
    {
        if (renderer == null)
            return true;

        string objectName = renderer.gameObject.name;
        if (!string.IsNullOrEmpty(objectName) && objectName.StartsWith("[Flowering", System.StringComparison.Ordinal))
            return true;

        if (data == null)
            return false;

        return renderer.sprite == data.WeaponInactiveSprite ||
               renderer.sprite == data.WeaponBloomSprite;
    }

    private void SetPlayerCutInRenderersVisible(bool visible)
    {
        for (int i = 0; i < playerTintEntries.Count; i++)
        {
            PlayerTintEntry entry = playerTintEntries[i];
            if (entry.Renderer == null)
                continue;

            entry.Renderer.enabled = visible ? entry.OriginalEnabled : false;
        }
    }

    private IEnumerator PlayPlayerTintInAndEyeFlash(AbilitySpec spec)
    {
        if (!playerCutInApplied || playerCutInRenderer == null || data == null)
        {
            playerCutInCoroutine = null;
            yield break;
        }

        float seconds = data.FadeInSeconds;
        if (seconds <= 0f)
        {
            SetPlayerCutInTint(1f);
            StartEyeFlashOnce();
            playerCutInCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (spec?.Token != null && spec.Token.IsCancelled)
                break;

            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / seconds);
            float tintRatio = EvaluateNormalizedRange(ratio, data.PlayerTintInStartRatio, data.PlayerTintInEndRatio);
            SetPlayerCutInTint(tintRatio);

            if (!eyeFlashStartedDuringCutIn && ratio >= Mathf.Max(data.PlayerTintInStartRatio, data.PlayerTintInEndRatio))
                StartEyeFlashOnce();

            yield return null;
        }

        SetPlayerCutInTint(1f);
        StartEyeFlashOnce();
        playerCutInCoroutine = null;
    }

    private IEnumerator PlayPlayerTintOut(AbilitySpec spec)
    {
        if (!playerCutInApplied || playerCutInRenderer == null || data == null)
        {
            playerCutInCoroutine = null;
            yield break;
        }

        float seconds = data.FadeOutSeconds;
        if (seconds <= 0f)
        {
            SetPlayerCutInTint(0f);
            playerCutInCoroutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (spec?.Token != null && spec.Token.IsCancelled)
                break;

            elapsed += Time.unscaledDeltaTime;
            float ratio = Mathf.Clamp01(elapsed / seconds);
            float tintRatio = 1f - EvaluateNormalizedRange(ratio, data.PlayerTintOutStartRatio, data.PlayerTintOutEndRatio);
            SetPlayerCutInTint(tintRatio);
            yield return null;
        }

        SetPlayerCutInTint(0f);
        playerCutInCoroutine = null;
    }

    private void StartEyeFlashOnce()
    {
        if (eyeFlashStartedDuringCutIn)
            return;

        eyeFlashStartedDuringCutIn = true;
        StartEyeFlash();
    }

    private void SetPlayerCutInTint(float ratio)
    {
        if (playerCutInRenderer == null || data == null)
            return;

        playerCutInRenderer.color = Color.Lerp(playerCutInStartColor, data.EyeFlashPlayerTint, Mathf.Clamp01(ratio));
    }

    private static float EvaluateNormalizedRange(float ratio, float startRatio, float endRatio)
    {
        float start = Mathf.Clamp01(Mathf.Min(startRatio, endRatio));
        float end = Mathf.Clamp01(Mathf.Max(startRatio, endRatio));
        if (end <= start + 0.0001f)
            return ratio >= end ? 1f : 0f;

        float t = Mathf.Clamp01((Mathf.Clamp01(ratio) - start) / (end - start));
        return Mathf.SmoothStep(0f, 1f, t);
    }

    private void RestorePlayerCutInVisual()
    {
        if (playerCutInCoroutine != null)
            StopCoroutine(playerCutInCoroutine);

        playerCutInCoroutine = null;
        StopEyeFlash();

        for (int i = 0; i < playerTintEntries.Count; i++)
        {
            PlayerTintEntry entry = playerTintEntries[i];
            if (entry.Renderer != null)
            {
                entry.Renderer.color = entry.OriginalColor;
                entry.Renderer.enabled = entry.OriginalEnabled;
            }
        }

        playerTintEntries.Clear();
        playerCutInApplied = false;
        eyeFlashStartedDuringCutIn = false;
        playerCutInRenderer = null;

        if (playerCutInRoot != null)
            Destroy(playerCutInRoot);

        playerCutInRoot = null;
    }

    private void ApplyWeaponBloomSprite(bool active)
    {
        if (!active)
        {
            RestoreWeaponSprite();
            return;
        }

        if (owner == null || data == null || data.WeaponInactiveSprite == null || data.WeaponBloomSprite == null)
            return;

        if (weaponBloomSpriteApplied || weaponRevealCoroutine != null)
            return;

        CaptureWeaponSpriteRenderers();

        if (ResolveWeaponRevealSeconds(active) > 0f)
        {
            StartWeaponReveal(active, null);
            return;
        }

        for (int i = 0; i < weaponSpriteEntries.Count; i++)
        {
            SpriteRenderer renderer = weaponSpriteEntries[i].Renderer;
            if (renderer != null)
                renderer.sprite = data.WeaponBloomSprite;
        }

        weaponBloomSpriteApplied = weaponSpriteEntries.Count > 0;
    }

    private void CaptureWeaponSpriteRenderers()
    {
        weaponSpriteEntries.Clear();
        if (owner == null || data == null)
            return;

        SpriteRenderer[] renderers = owner.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.gameObject.activeInHierarchy)
                continue;

            if (renderer.gameObject.name.StartsWith("[Flowering", System.StringComparison.Ordinal))
                continue;

            if (renderer.sprite != data.WeaponInactiveSprite && renderer.sprite != data.WeaponBloomSprite)
                continue;

            weaponSpriteEntries.Add(new WeaponSpriteEntry
            {
                Renderer = renderer,
                OriginalSprite = renderer.sprite == data.WeaponBloomSprite ? data.WeaponInactiveSprite : renderer.sprite
            });
        }
    }

    private void RestoreWeaponSprite()
    {
        StopWeaponReveal();

        for (int i = 0; i < weaponSpriteEntries.Count; i++)
        {
            WeaponSpriteEntry entry = weaponSpriteEntries[i];
            if (entry.Renderer != null)
                entry.Renderer.sprite = entry.OriginalSprite;
        }

        weaponSpriteEntries.Clear();
        weaponBloomSpriteApplied = false;
    }

    private void StartWeaponReveal(bool active, AbilitySpec spec)
    {
        StopWeaponReveal();
        weaponRevealCoroutine = StartCoroutine(PlayWeaponReveal(active, spec, ResolveAbilitySystem()));
    }

    private void StartWeaponRevealOnAnimationEvent(AbilitySystem system, AbilitySpec spec)
    {
        StopWeaponReveal();
        weaponRevealCoroutine = StartCoroutine(PlayWeaponRevealOnAnimationEvent(system, spec));
    }

    private float ResolveWeaponRevealSeconds(bool active)
    {
        return active ? data.WeaponRevealInSeconds : data.WeaponRevealOutSeconds;
    }

    private IEnumerator PlayWeaponReveal(bool active, AbilitySpec spec, AbilitySystem system = null)
    {
        if (owner == null || data == null || data.WeaponInactiveSprite == null || data.WeaponBloomSprite == null)
        {
            weaponRevealCoroutine = null;
            yield break;
        }

        if (weaponSpriteEntries.Count == 0)
            CaptureWeaponSpriteRenderers();

        float seconds = ResolveWeaponRevealSeconds(active);
        if (weaponSpriteEntries.Count == 0 || seconds <= 0f)
        {
            ApplyWeaponSpriteImmediate(active);
            weaponRevealCoroutine = null;
            yield break;
        }

        CreateWeaponRevealOverlays(active);
        if (weaponRevealOverlays.Count == 0)
        {
            ApplyWeaponSpriteImmediate(active);
            weaponRevealCoroutine = null;
            yield break;
        }

        PlayWeaponRevealSound(active, system, spec);

        float elapsed = 0f;
        SetWeaponRevealProgress(0f);

        while (elapsed < seconds)
        {
            if (spec?.Token != null && spec.Token.IsCancelled)
            {
                weaponRevealCoroutine = null;
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;
            SetWeaponRevealProgress(Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        SetWeaponRevealProgress(1f);
        ApplyWeaponSpriteImmediate(active);
        ClearWeaponRevealOverlays();
        weaponRevealCoroutine = null;
    }

    private IEnumerator PlayWeaponRevealOnAnimationEvent(AbilitySystem system, AbilitySpec spec)
    {
        yield return WaitForWeaponRevealEventOrDelay(system, spec);

        if (spec?.Token != null && spec.Token.IsCancelled)
        {
            weaponRevealCoroutine = null;
            yield break;
        }

        yield return PlayWeaponReveal(active: true, spec, system);
    }

    private IEnumerator WaitForWeaponRevealEventOrDelay(AbilitySystem system, AbilitySpec spec)
    {
        GameplayTag eventTag = data != null ? data.WeaponRevealEventTag : null;
        if (system != null && spec != null && eventTag != null)
        {
            GameplayEventWaiter waiter = system.WaitGameplayEvent(eventTag, spec);
            if (waiter == null)
                yield break;

            float timeout = data != null ? data.WeaponRevealEventTimeout : 0f;
            float elapsed = 0f;
            while (!waiter.Done)
            {
                if (spec.Token != null && spec.Token.IsCancelled)
                {
                    waiter.Cancel();
                    yield break;
                }

                if (timeout > 0f && elapsed >= timeout)
                {
                    waiter.Cancel();
                    yield break;
                }

                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }

            waiter.Cancel();
            yield break;
        }

        float fallbackDelay = data != null ? data.WeaponRevealFallbackDelay : 0f;
        if (fallbackDelay <= 0f)
            yield break;

        float fallbackElapsed = 0f;
        while (fallbackElapsed < fallbackDelay)
        {
            if (spec?.Token != null && spec.Token.IsCancelled)
                yield break;

            fallbackElapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private void ApplyWeaponSpriteImmediate(bool active)
    {
        Sprite targetSprite = active ? data.WeaponBloomSprite : data.WeaponInactiveSprite;
        for (int i = 0; i < weaponSpriteEntries.Count; i++)
        {
            SpriteRenderer renderer = weaponSpriteEntries[i].Renderer;
            if (renderer != null)
                renderer.sprite = targetSprite;
        }

        weaponBloomSpriteApplied = active && weaponSpriteEntries.Count > 0;
    }

    private void CreateWeaponRevealOverlays(bool active)
    {
        ClearWeaponRevealOverlays();

        Sprite overlaySprite = active ? data.WeaponBloomSprite : data.WeaponInactiveSprite;
        Sprite baseSprite = active ? data.WeaponInactiveSprite : data.WeaponBloomSprite;
        Sprite maskSprite = GetRevealMaskSprite();

        for (int i = 0; i < weaponSpriteEntries.Count; i++)
        {
            SpriteRenderer source = weaponSpriteEntries[i].Renderer;
            if (source == null || overlaySprite == null || baseSprite == null)
                continue;

            source.sprite = baseSprite;

            GameObject overlay = new("[FloweringWeaponReveal]");
            overlay.transform.SetParent(source.transform, false);
            overlay.transform.localPosition = Vector3.zero;
            overlay.transform.localRotation = Quaternion.identity;
            overlay.transform.localScale = Vector3.one;

            SpriteRenderer overlayRenderer = overlay.AddComponent<SpriteRenderer>();
            overlayRenderer.sprite = overlaySprite;
            overlayRenderer.color = source.color;
            overlayRenderer.flipX = source.flipX;
            overlayRenderer.flipY = source.flipY;
            overlayRenderer.sortingLayerID = source.sortingLayerID;
            overlayRenderer.sortingOrder = source.sortingOrder + 1;
            overlayRenderer.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            overlayRenderer.sharedMaterial = source.sharedMaterial;

            GameObject maskObject = new("[FloweringWeaponRevealMask]");
            maskObject.transform.SetParent(overlay.transform, false);

            SpriteMask mask = maskObject.AddComponent<SpriteMask>();
            mask.sprite = maskSprite;
            mask.alphaCutoff = data != null ? data.WeaponRevealMaskAlphaCutoff : 0.5f;
            mask.isCustomRangeActive = true;
            mask.frontSortingLayerID = source.sortingLayerID;
            mask.frontSortingOrder = source.sortingOrder + 2;
            mask.backSortingLayerID = source.sortingLayerID;
            mask.backSortingOrder = source.sortingOrder;

            Vector2 direction = ResolveRevealDirection(source);
            Vector2 perpendicular = new(-direction.y, direction.x);
            CalculateRevealBounds(overlaySprite, direction, perpendicular, out float minDirection, out float maxDirection, out float minPerpendicular, out float maxPerpendicular);

            weaponRevealOverlays.Add(new WeaponRevealOverlay
            {
                Root = overlay,
                Renderer = overlayRenderer,
                Mask = mask,
                Direction = direction,
                Perpendicular = perpendicular,
                MinDirection = minDirection,
                MaxDirection = maxDirection,
                MinPerpendicular = minPerpendicular,
                MaxPerpendicular = maxPerpendicular
            });

            SpawnWeaponRevealParticle(source);
        }
    }

    private void SetWeaponRevealProgress(float progress)
    {
        for (int i = 0; i < weaponRevealOverlays.Count; i++)
        {
            WeaponRevealOverlay overlay = weaponRevealOverlays[i];
            if (overlay.Renderer == null || overlay.Mask == null)
                continue;

            ApplyWeaponRevealMask(overlay, progress);
        }
    }

    private Vector2 ResolveRevealDirection(SpriteRenderer source)
    {
        Vector2 direction = data != null
            ? data.WeaponRevealMaskLocalDirection
            : new Vector2(-1f, 1f).normalized;

        if (source != null)
        {
            if (source.flipX)
                direction.x *= -1f;
            if (source.flipY)
                direction.y *= -1f;
        }

        return direction.sqrMagnitude > 0.0001f ? direction.normalized : new Vector2(-1f, 1f).normalized;
    }

    private static void CalculateRevealBounds(
        Sprite sprite,
        Vector2 direction,
        Vector2 perpendicular,
        out float minDirection,
        out float maxDirection,
        out float minPerpendicular,
        out float maxPerpendicular)
    {
        Bounds bounds = sprite.bounds;
        Vector2 min = bounds.min;
        Vector2 max = bounds.max;

        Vector2[] corners =
        {
            new(min.x, min.y),
            new(min.x, max.y),
            new(max.x, min.y),
            new(max.x, max.y)
        };

        minDirection = maxDirection = Vector2.Dot(corners[0], direction);
        minPerpendicular = maxPerpendicular = Vector2.Dot(corners[0], perpendicular);
        for (int i = 1; i < corners.Length; i++)
        {
            float alongDirection = Vector2.Dot(corners[i], direction);
            float alongPerpendicular = Vector2.Dot(corners[i], perpendicular);
            minDirection = Mathf.Min(minDirection, alongDirection);
            maxDirection = Mathf.Max(maxDirection, alongDirection);
            minPerpendicular = Mathf.Min(minPerpendicular, alongPerpendicular);
            maxPerpendicular = Mathf.Max(maxPerpendicular, alongPerpendicular);
        }
    }

    private void ApplyWeaponRevealMask(WeaponRevealOverlay overlay, float progress)
    {
        float padding = data != null ? data.WeaponRevealMaskPadding : 0.1f;
        float widthMultiplier = data != null ? data.WeaponRevealMaskWidthMultiplier : 1.25f;
        float length = Mathf.Max(0.001f, overlay.MaxDirection - overlay.MinDirection);
        float width = Mathf.Max(0.001f, overlay.MaxPerpendicular - overlay.MinPerpendicular);
        float visibleLength = Mathf.Max(0.001f, (length + padding * 2f) * Mathf.Clamp01(progress));
        float centerDirection = overlay.MinDirection - padding + visibleLength * 0.5f;
        float centerPerpendicular = (overlay.MinPerpendicular + overlay.MaxPerpendicular) * 0.5f;
        Vector2 localCenter = overlay.Direction * centerDirection + overlay.Perpendicular * centerPerpendicular;

        Transform maskTransform = overlay.Mask.transform;
        maskTransform.localPosition = new Vector3(localCenter.x, localCenter.y, 0f);
        maskTransform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(overlay.Direction.y, overlay.Direction.x) * Mathf.Rad2Deg);
        maskTransform.localScale = new Vector3(visibleLength, width * widthMultiplier + padding * 2f, 1f);
    }

    private static Sprite GetRevealMaskSprite()
    {
        if (revealMaskSprite != null)
            return revealMaskSprite;

        Texture2D texture = new(RevealMaskTextureSize, RevealMaskTextureSize, TextureFormat.RGBA32, false)
        {
            name = "Flowering_RevealMaskSquare",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[RevealMaskTextureSize * RevealMaskTextureSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        texture.SetPixels32(pixels);
        texture.Apply();

        revealMaskSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, RevealMaskTextureSize, RevealMaskTextureSize),
            new Vector2(0.5f, 0.5f),
            RevealMaskTextureSize);
        revealMaskSprite.name = "Flowering_RevealMaskSquare";
        return revealMaskSprite;
    }

    private void StopWeaponReveal()
    {
        if (weaponRevealCoroutine != null)
            StopCoroutine(weaponRevealCoroutine);

        weaponRevealCoroutine = null;
        ClearWeaponRevealOverlays();
        ClearWeaponRevealParticles();
    }

    private void ClearWeaponRevealOverlays()
    {
        for (int i = 0; i < weaponRevealOverlays.Count; i++)
        {
            GameObject root = weaponRevealOverlays[i].Root;
            if (root != null)
                Destroy(root);
        }

        weaponRevealOverlays.Clear();
    }

    private void SpawnWeaponRevealParticle(SpriteRenderer source)
    {
        if (data == null || source == null || data.WeaponRevealParticlePrefab == null)
            return;

        GameObject particle = Instantiate(data.WeaponRevealParticlePrefab, source.transform.position, source.transform.rotation);
        if (particle == null)
            return;

        particle.transform.SetParent(source.transform, worldPositionStays: true);
        ApplyRendererSortingOrderOffset(particle, data.ParticleSortingOrderOffset);
        weaponRevealParticleObjects.Add(particle);
        Destroy(particle, data.ParticleLifetimeFallback);
    }

    private void SpawnFinalShakeParticle()
    {
        if (data == null || data.FinalShakeParticlePrefab == null)
            return;

        GameObject particle = Instantiate(data.FinalShakeParticlePrefab, ResolvePlayerCenter(), Quaternion.identity);
        if (particle == null)
            return;

        ApplyRendererSortingOrderOffset(particle, data.ParticleSortingOrderOffset);
        finalShakeParticleObjects.Add(particle);
        Destroy(particle, data.ParticleLifetimeFallback);
    }

    private AbilitySystem ResolveAbilitySystem()
    {
        return owner != null ? owner.GetComponent<AbilitySystem>() : null;
    }

    private void PlayWeaponRevealSound(bool active, AbilitySystem system, AbilitySpec spec)
    {
        if (data == null)
            return;

        AbilityAudioRouter.PlayOneShotAtPosition(
            active ? data.WeaponRevealInSound : data.WeaponRevealOutSound,
            system,
            spec,
            ResolveWeaponRevealSoundPosition(),
            data);
    }

    private Vector3 ResolveWeaponRevealSoundPosition()
    {
        for (int i = 0; i < weaponRevealOverlays.Count; i++)
        {
            GameObject root = weaponRevealOverlays[i].Root;
            if (root != null)
                return root.transform.position;
        }

        return ResolvePlayerCenter();
    }

    private Vector3 ResolvePlayerCenter()
    {
        SpriteRenderer bodyRenderer = ResolvePlayerBodyRenderer();
        if (bodyRenderer != null)
            return bodyRenderer.bounds.center;

        return owner != null ? owner.transform.position : transform.position;
    }

    private void ClearWeaponRevealParticles()
    {
        for (int i = 0; i < weaponRevealParticleObjects.Count; i++)
        {
            GameObject particle = weaponRevealParticleObjects[i];
            if (particle != null)
                Destroy(particle);
        }

        weaponRevealParticleObjects.Clear();
    }

    private void ClearFinalShakeParticles()
    {
        for (int i = 0; i < finalShakeParticleObjects.Count; i++)
        {
            GameObject particle = finalShakeParticleObjects[i];
            if (particle != null)
                Destroy(particle);
        }

        finalShakeParticleObjects.Clear();
    }

    private static void ApplyRendererSortingOrderOffset(GameObject root, int sortingOrderOffset)
    {
        if (root == null || sortingOrderOffset == 0)
            return;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            renderers[i].sortingOrder += sortingOrderOffset;
    }

    private void CacheCameraLens()
    {
        zoomCamera = CameraBootstrap.GetPlayerCamera();
        if (zoomCamera == null)
        {
            hasCachedCameraLens = false;
            return;
        }

        cachedLensOrthographic = zoomCamera.Lens.Orthographic;
        cachedOrthographicSize = zoomCamera.Lens.OrthographicSize;
        cachedFieldOfView = zoomCamera.Lens.FieldOfView;
        hasCachedCameraLens = true;
    }

    private bool TryGetCameraZoomValues(
        bool zoomIn,
        out float startOrthographicSize,
        out float targetOrthographicSize,
        out float startFieldOfView,
        out float targetFieldOfView)
    {
        startOrthographicSize = 0f;
        targetOrthographicSize = 0f;
        startFieldOfView = 0f;
        targetFieldOfView = 0f;

        if (data == null)
            return false;

        if (zoomCamera == null || !hasCachedCameraLens)
            CacheCameraLens();

        if (zoomCamera == null || !hasCachedCameraLens)
            return false;

        startOrthographicSize = zoomCamera.Lens.OrthographicSize;
        startFieldOfView = zoomCamera.Lens.FieldOfView;

        float zoomScale = data.CutInZoomScale;
        targetOrthographicSize = zoomIn
            ? Mathf.Max(0.01f, cachedOrthographicSize * zoomScale)
            : cachedOrthographicSize;
        targetFieldOfView = zoomIn
            ? Mathf.Max(0.01f, cachedFieldOfView * zoomScale)
            : cachedFieldOfView;

        return true;
    }

    private void ApplyCameraLens(float orthographicSize, float fieldOfView)
    {
        if (zoomCamera == null)
            return;

        var lens = zoomCamera.Lens;
        if (lens.Orthographic || cachedLensOrthographic)
            lens.OrthographicSize = Mathf.Max(0.01f, orthographicSize);
        else
            lens.FieldOfView = Mathf.Max(0.01f, fieldOfView);

        zoomCamera.Lens = lens;
    }

    private void RestoreCameraZoom()
    {
        if (zoomCamera == null || !hasCachedCameraLens)
            return;

        ApplyCameraLens(cachedOrthographicSize, cachedFieldOfView);
        hasCachedCameraLens = false;
        zoomCamera = null;
    }

    private void SetBorderIntensity(float intensity)
    {
        if (borderGraphic == null)
            return;

        borderGraphic.Intensity = Mathf.Clamp01(intensity);
    }

    private void SetBorderReveal(float reveal)
    {
        borderReveal = Mathf.Clamp01(reveal);
        if (borderGraphic != null)
            borderGraphic.RevealProgress = borderReveal;
    }

    private IEnumerator FadeWorldDimAndCameraZoom(
        AbilitySystem system,
        float dimFrom,
        float dimTo,
        float dimSeconds,
        bool zoomIn,
        float zoomSeconds,
        AbilitySpec spec)
    {
        float resolvedDimSeconds = Mathf.Max(0f, dimSeconds);
        float resolvedZoomSeconds = Mathf.Max(0f, zoomSeconds);
        float totalSeconds = Mathf.Max(resolvedDimSeconds, resolvedZoomSeconds);

        float startOrthographicSize = 0f;
        float startFieldOfView = 0f;
        float targetOrthographicSize = 0f;
        float targetFieldOfView = 0f;
        bool canZoom = TryGetCameraZoomValues(
            zoomIn,
            out startOrthographicSize,
            out targetOrthographicSize,
            out startFieldOfView,
            out targetFieldOfView);

        if (totalSeconds <= 0f)
        {
            SetWorldDimAlpha(dimTo);
            if (canZoom)
                ApplyCameraLens(targetOrthographicSize, targetFieldOfView);
            yield break;
        }

        float elapsed = 0f;
        float shakeElapsed = data != null ? data.ZoomShakeIntervalSeconds : 0.055f;
        while (elapsed < totalSeconds)
        {
            if (spec?.Token != null && spec.Token.IsCancelled)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            if (zoomIn)
                TickZoomShake(system, ref shakeElapsed);

            float dimT = resolvedDimSeconds > 0f ? Mathf.Clamp01(elapsed / resolvedDimSeconds) : 1f;
            SetWorldDimAlpha(Mathf.Lerp(dimFrom, dimTo, dimT));

            if (canZoom)
            {
                float zoomT = resolvedZoomSeconds > 0f ? Mathf.Clamp01(elapsed / resolvedZoomSeconds) : 1f;
                float easedT = Mathf.SmoothStep(0f, 1f, zoomT);
                ApplyCameraLens(
                    Mathf.Lerp(startOrthographicSize, targetOrthographicSize, easedT),
                    Mathf.Lerp(startFieldOfView, targetFieldOfView, easedT));
            }

            yield return null;
        }

        SetWorldDimAlpha(dimTo);
        if (canZoom)
            ApplyCameraLens(targetOrthographicSize, targetFieldOfView);
    }

    private void TickZoomShake(AbilitySystem system, ref float elapsed)
    {
        if (data == null || data.ZoomShakeAmplitude <= 0f)
            return;

        elapsed += Time.unscaledDeltaTime;
        float interval = data.ZoomShakeIntervalSeconds;
        if (elapsed < interval)
            return;

        elapsed = 0f;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);
        PlayShake(system, data.ZoomShakeAmplitude, "Flowering Bloom zoom", direction, interval);
    }

    private IEnumerator RevealBorder(float seconds, AbilitySpec spec)
    {
        SetBorderIntensity(1f);
        if (seconds <= 0f)
        {
            SetBorderReveal(1f);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (spec?.Token != null && spec.Token.IsCancelled)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            SetBorderReveal(Mathf.Clamp01(elapsed / seconds));
            yield return null;
        }

        SetBorderReveal(1f);
    }

    private static IEnumerator WaitUnscaled(float seconds, AbilitySpec spec)
    {
        if (seconds <= 0f)
            yield break;

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            if (spec?.Token != null && spec.Token.IsCancelled)
                yield break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static void PlayShake(AbilitySystem system, float amplitude, string reason)
    {
        PlayShake(system, amplitude, reason, Vector3.up, 0f);
    }

    private static void PlayShake(AbilitySystem system, float amplitude, string reason, Vector3 direction, float minIntervalSeconds)
    {
        if (amplitude <= 0f)
            return;

        CameraShakeService.Play(
            amplitude,
            direction,
            system != null ? system.gameObject : null,
            minIntervalSeconds,
            reason,
            ignoreScreenShakeSetting: true);
    }

    private static void TryDisableTransparentCull(Graphic graphic)
    {
        if (graphic == null)
            return;

        CanvasRenderer renderer = graphic.GetComponent<CanvasRenderer>();
        if (renderer != null)
            renderer.cullTransparentMesh = false;
    }

    private void EnsurePlayerOutline()
    {
        if (owner == null || data == null)
            return;

        if (playerOutline == null)
            playerOutline = owner.GetComponent<FloweringPlayerPixelOutline>();

        if (playerOutline == null)
            playerOutline = owner.AddComponent<FloweringPlayerPixelOutline>();

        playerOutline.Initialize(owner, data.BloomColor, data.OutlinePixels, data.OutlineTopWavePixels, data.OutlineWaveSpeed);
    }
}

public sealed class FloweringWorldDimOverlay : MonoBehaviour
{
    private const int TextureSize = 16;
    private const float FallbackWorldSize = 64f;

    private static Sprite squareSprite;

    private SpriteRenderer overlayRenderer;
    private Camera targetCamera;
    private Transform fallbackAnchor;
    private float cameraPadding = 1.2f;
    private float zOffset = -0.05f;
    private float alpha;
    private bool released;

    public void Initialize(FloweringBloomData data, Transform fallback)
    {
        fallbackAnchor = fallback;
        cameraPadding = data != null ? data.WorldDimCameraPadding : 1.2f;
        zOffset = data != null ? data.WorldDimZ : -0.05f;
        targetCamera = ResolveCamera();

        if (overlayRenderer == null)
        {
            overlayRenderer = gameObject.AddComponent<SpriteRenderer>();
            overlayRenderer.sprite = GetSquareSprite();
            overlayRenderer.maskInteraction = SpriteMaskInteraction.None;
        }

        overlayRenderer.sortingLayerName = data != null ? data.WorldDimSortingLayerName : "Entity";
        overlayRenderer.sortingOrder = data != null ? data.WorldDimSortingOrder : -1;
        SetAlpha(alpha);
        Refresh();
    }

    public void SetAlpha(float value)
    {
        alpha = Mathf.Clamp01(value);
        bool shouldBeActive = alpha > 0.001f;
        if (gameObject.activeSelf != shouldBeActive)
            gameObject.SetActive(shouldBeActive);

        if (overlayRenderer != null)
            overlayRenderer.color = new Color(0f, 0f, 0f, alpha);

        if (shouldBeActive)
            Refresh();
    }

    public void Release()
    {
        if (released)
            return;

        released = true;
        Destroy(gameObject);
    }

    public void Refresh()
    {
        if (overlayRenderer == null)
            return;

        if (targetCamera == null)
            targetCamera = ResolveCamera();

        Vector3 center = targetCamera != null
            ? targetCamera.transform.position
            : fallbackAnchor != null
                ? fallbackAnchor.position
                : Vector3.zero;

        transform.position = new Vector3(center.x, center.y, zOffset);
        transform.rotation = Quaternion.identity;

        if (targetCamera != null && targetCamera.orthographic)
        {
            float height = Mathf.Max(0.01f, targetCamera.orthographicSize * 2f * cameraPadding);
            float width = Mathf.Max(0.01f, height * targetCamera.aspect);
            transform.localScale = new Vector3(width, height, 1f);
            return;
        }

        transform.localScale = new Vector3(FallbackWorldSize, FallbackWorldSize, 1f);
    }

    private void LateUpdate()
    {
        Refresh();
    }

    private static Camera ResolveCamera()
    {
        Camera camera = CameraBootstrap.GetMainCamera();
        if (camera != null)
            return camera;

        if (Camera.main != null)
            return Camera.main;

        return Object.FindAnyObjectByType<Camera>();
    }

    private static Sprite GetSquareSprite()
    {
        if (squareSprite != null)
            return squareSprite;

        Texture2D texture = new(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            name = "Flowering_WorldDimSquare",
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };

        Color32[] pixels = new Color32[TextureSize * TextureSize];
        for (int i = 0; i < pixels.Length; i++)
            pixels[i] = new Color32(255, 255, 255, 255);

        texture.SetPixels32(pixels);
        texture.Apply();

        squareSprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, TextureSize, TextureSize),
            new Vector2(0.5f, 0.5f),
            TextureSize);
        squareSprite.name = "Flowering_WorldDimSquare";
        return squareSprite;
    }
}

public sealed class FloweringEyeFlashPlayer : MonoBehaviour
{
    private readonly List<Sprite> frames = new();
    private SpriteRenderer spriteRenderer;
    private float secondsPerFrame = 1f / 18f;
    private float elapsed;
    private int frameIndex;
    private bool completed;

    public event System.Action<FloweringEyeFlashPlayer> Completed;

    public void Initialize(Sprite[] sourceFrames, float fps, Color color, string sortingLayerName, int sortingOrder)
    {
        frames.Clear();
        if (sourceFrames != null)
        {
            for (int i = 0; i < sourceFrames.Length; i++)
            {
                if (sourceFrames[i] != null)
                    frames.Add(sourceFrames[i]);
            }
        }

        if (spriteRenderer == null)
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

        secondsPerFrame = 1f / Mathf.Max(1f, fps);
        elapsed = 0f;
        frameIndex = 0;
        completed = frames.Count == 0;

        spriteRenderer.color = color;
        spriteRenderer.sortingLayerName = string.IsNullOrWhiteSpace(sortingLayerName) ? "Entity" : sortingLayerName;
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.maskInteraction = SpriteMaskInteraction.None;
        spriteRenderer.enabled = frames.Count > 0;
        spriteRenderer.sprite = frames.Count > 0 ? frames[0] : null;
    }

    private void LateUpdate()
    {
        if (completed || spriteRenderer == null || frames.Count == 0)
            return;

        elapsed += Time.unscaledDeltaTime;
        while (elapsed >= secondsPerFrame)
        {
            elapsed -= secondsPerFrame;
            frameIndex++;
            if (frameIndex >= frames.Count)
            {
                frameIndex = frames.Count - 1;
                completed = true;
                break;
            }
        }

        spriteRenderer.sprite = frames[frameIndex];
        if (completed)
            Completed?.Invoke(this);
    }
}

public sealed class FloweringPlayerPixelOutline : MonoBehaviour
{
    private readonly List<Entry> entries = new();
    private Color outlineColor;
    private float wavePixels;
    private float waveSpeed;
    private bool initialized;

    private struct Entry
    {
        public SpriteRenderer Source;
        public SpriteRenderer Outline;
        public Vector3 BaseLocalOffset;
        public bool IsTop;
        public float Phase;
    }

    public void Initialize(GameObject owner, Color color, float outlinePixels, float topWavePixels, float outlineWaveSpeed)
    {
        Release();

        if (owner == null)
            return;

        outlineColor = color;
        wavePixels = topWavePixels;
        waveSpeed = outlineWaveSpeed;

        SpriteRenderer[] sources = owner.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < sources.Length; i++)
        {
            SpriteRenderer source = sources[i];
            if (source == null || source.sprite == null || source.gameObject.name == "[FloweringOutline]")
                continue;

            AddOutlineSprites(source, outlinePixels);
        }

        initialized = true;
        Refresh();
    }

    public void Release()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            SpriteRenderer outline = entries[i].Outline;
            if (outline != null)
                Destroy(outline.gameObject);
        }

        entries.Clear();
        initialized = false;
    }

    private void LateUpdate()
    {
        if (initialized)
            Refresh();
    }

    private void OnDisable()
    {
        Release();
    }

    private void AddOutlineSprites(SpriteRenderer source, float outlinePixels)
    {
        float ppu = source.sprite != null && source.sprite.pixelsPerUnit > 0f ? source.sprite.pixelsPerUnit : 100f;
        float offset = Mathf.Max(0.1f, outlinePixels) / ppu;
        float topOffset = offset;

        AddEntry(source, new Vector3(-offset, 0f, 0f), false);
        AddEntry(source, new Vector3(offset, 0f, 0f), false);
        AddEntry(source, new Vector3(0f, -offset, 0f), false);
        AddEntry(source, new Vector3(0f, topOffset, 0f), true);
        AddEntry(source, new Vector3(-offset, topOffset, 0f), true);
        AddEntry(source, new Vector3(offset, topOffset, 0f), true);
    }

    private void AddEntry(SpriteRenderer source, Vector3 localOffset, bool isTop)
    {
        GameObject go = new("[FloweringOutline]");
        go.transform.SetParent(source.transform, false);
        go.transform.localPosition = localOffset;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;

        SpriteRenderer outline = go.AddComponent<SpriteRenderer>();
        outline.sprite = source.sprite;
        outline.color = outlineColor;
        outline.sortingLayerID = source.sortingLayerID;
        outline.sortingOrder = source.sortingOrder - 1;
        outline.flipX = source.flipX;
        outline.flipY = source.flipY;
        outline.maskInteraction = source.maskInteraction;

        entries.Add(new Entry
        {
            Source = source,
            Outline = outline,
            BaseLocalOffset = localOffset,
            IsTop = isTop,
            Phase = Random.value * Mathf.PI * 2f
        });
    }

    private void Refresh()
    {
        for (int i = 0; i < entries.Count; i++)
        {
            Entry entry = entries[i];
            if (entry.Source == null || entry.Outline == null)
                continue;

            entry.Outline.enabled = entry.Source.enabled && entry.Source.gameObject.activeInHierarchy;
            entry.Outline.sprite = entry.Source.sprite;
            entry.Outline.color = outlineColor;
            entry.Outline.sortingLayerID = entry.Source.sortingLayerID;
            entry.Outline.sortingOrder = entry.Source.sortingOrder - 1;
            entry.Outline.flipX = entry.Source.flipX;
            entry.Outline.flipY = entry.Source.flipY;
            entry.Outline.maskInteraction = entry.Source.maskInteraction;

            Vector3 localOffset = entry.BaseLocalOffset;
            if (entry.IsTop && entry.Source.sprite != null)
            {
                float ppu = entry.Source.sprite.pixelsPerUnit > 0f ? entry.Source.sprite.pixelsPerUnit : 100f;
                float wave = Mathf.Sin(Time.unscaledTime * waveSpeed + entry.Phase) * (wavePixels / ppu);
                localOffset.x += Mathf.Round(wave * ppu) / ppu;
            }

            entry.Outline.transform.localPosition = localOffset;
        }
    }
}
