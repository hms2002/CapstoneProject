using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum MouseCursorDomain
{
    Combat = 0,
    Inventory = 1,
    NpcUi = 2,
    SystemUi = 3,
    Encyclopedia = 4
}

public enum MouseCursorVariant
{
    Default = 0,
    Interactable = 1,
    Pressed = 2,
    Dragging = 3,
    InteractablePressed = 4
}

// 책임: 커서 스프라이트, hotspot, 커서 픽셀 배율 설정을 보관한다.
[Serializable]
public sealed class MouseCursorSpriteDefinition
{
    public Sprite sprite;
    public Vector2 hotspotPixels;
    [Min(0.1f)] public float scale = 1f;
}

// 책임: 일반 커서 도메인의 variant별 커서 스프라이트 설정을 보관한다.
[Serializable]
public sealed class MouseCursorDomainDefinition
{
    public MouseCursorSpriteDefinition defaultCursor = new MouseCursorSpriteDefinition();
    public MouseCursorSpriteDefinition interactableCursor = new MouseCursorSpriteDefinition();
    public MouseCursorSpriteDefinition pressedCursor = new MouseCursorSpriteDefinition();
    public MouseCursorSpriteDefinition draggingCursor = new MouseCursorSpriteDefinition();

    public MouseCursorSpriteDefinition GetDefinition(MouseCursorVariant variant)
    {
        return variant switch
        {
            MouseCursorVariant.Interactable => interactableCursor,
            MouseCursorVariant.Pressed => pressedCursor,
            MouseCursorVariant.InteractablePressed => pressedCursor,
            MouseCursorVariant.Dragging => draggingCursor,
            _ => defaultCursor
        };
    }
}

// 책임: 도감 UI 전용 커서 도메인의 variant별 커서 스프라이트 설정을 보관한다.
[Serializable]
public sealed class MouseCursorEncyclopediaDomainDefinition
{
    public MouseCursorSpriteDefinition defaultCursor = new MouseCursorSpriteDefinition();
    public MouseCursorSpriteDefinition defaultPressedCursor = new MouseCursorSpriteDefinition();
    public MouseCursorSpriteDefinition itemSlotCursor = new MouseCursorSpriteDefinition();
    public MouseCursorSpriteDefinition itemSlotPressedCursor = new MouseCursorSpriteDefinition();

    public MouseCursorSpriteDefinition GetDefinition(MouseCursorVariant variant)
    {
        return variant switch
        {
            MouseCursorVariant.Interactable => itemSlotCursor,
            MouseCursorVariant.Pressed => defaultPressedCursor,
            MouseCursorVariant.InteractablePressed => itemSlotPressedCursor,
            _ => defaultCursor
        };
    }
}

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
// Responsibility: resolves the active cursor domain/variant and presents it through Unity's cursor API, independently of UI sorting.
public sealed class MouseCursorService : MonoBehaviour, IMouseCursorBackend
{
    private const string DefaultThemeResourcePath = "DefaultMouseCursorTheme";
    private const int DialogueDomainPriority = 50;

    private sealed class DomainRequest
    {
        public UnityEngine.Object owner;
        public MouseCursorDomain domain;
        public int priority;
        public long order;
    }

    // 책임: 커서 상태를 요청한 owner 객체의 생존 여부를 추적한다.
    private sealed class OwnerFlag
    {
        public UnityEngine.Object owner;
    }

    public static MouseCursorService Instance { get; private set; }
    private static bool bootstrapCreationRequested;

    [Header("Theme")]
    [SerializeField] private MouseCursorTheme themeOverride;

    // Retained for serialized asset compatibility; Canvas rendering is no longer used.
    [SerializeField, HideInInspector] private Canvas authoredCursorCanvas;
    [SerializeField, HideInInspector] private RectTransform authoredCursorRect;
    [SerializeField, HideInInspector] private Image authoredCursorImage;
    [SerializeField, HideInInspector] private bool hideSystemCursorWhileSpriteActive = true;
    [SerializeField, HideInInspector] private bool preferHardwareCursorWhenAvailable = true;
    [SerializeField, HideInInspector] private bool preferHardwareCursorInExclusiveFullscreen = true;
    [SerializeField, HideInInspector] private bool keepSystemCursorVisibleWhenUsingSoftwareCursor;
    [SerializeField, HideInInspector] private bool keepSystemCursorVisibleInExclusiveFullscreenFallback;
    [SerializeField, HideInInspector] private int overlaySortingOrder = short.MaxValue;

    [Header("Cursor Pixel Size")]
    [SerializeField] private bool scaleWithScreenHeight = true;
    [SerializeField, Min(1f)] private float referenceScreenHeight = 1080f;
    [SerializeField, Min(0.1f)] private float minResolutionScale = 0.1f;
    [SerializeField, Min(0.1f)] private float maxResolutionScale = 2.5f;

    private readonly Dictionary<int, DomainRequest> domainRequests = new Dictionary<int, DomainRequest>();
    private readonly Dictionary<int, OwnerFlag> interactableOwners = new Dictionary<int, OwnerFlag>();
    private readonly Dictionary<int, OwnerFlag> draggingOwners = new Dictionary<int, OwnerFlag>();
    private readonly Dictionary<int, OwnerFlag> hiddenOwners = new Dictionary<int, OwnerFlag>();
    private sealed class CursorTexture
    {
        public Color32[] pixels;
        public int sourceWidth;
        public int sourceHeight;
        public Texture2D texture;
    }

    // One source readback and one current output size per sprite, bounded across window resizing.
    private readonly Dictionary<int, CursorTexture> generatedCursorTextures = new Dictionary<int, CursorTexture>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(8);

    private EventSystem pointerEventSystem;
    private PointerEventData pointerEventData;
    private long nextOrder;
    private bool isBootstrapInstance;
    private bool defaultThemeLoadAttempted;
    private bool defaultThemeMissingLogged;
    private MouseCursorTheme loadedTheme;
    private Texture2D appliedCursorTexture;
    private Vector2 appliedCursorHotspot = new Vector2(float.MinValue, float.MinValue);
    private MouseCursorDomain currentDomain = MouseCursorDomain.Combat;
    private MouseCursorVariant currentVariant = MouseCursorVariant.Default;
    private int lastScreenWidth = -1;
    private int lastScreenHeight = -1;
    private FullScreenMode lastFullScreenMode;
    private bool hasCapturedDisplayState;
    private bool forceCursorTextureReapply;
    private bool cursorReleasedByEscape;
    private bool cursorDiagnosticPending;
    private bool cursorDiagnosticSettling;
    private float cursorDiagnosticDeadline;

    public MouseCursorDomain CurrentDomain => currentDomain;
    public MouseCursorVariant CurrentVariant => currentVariant;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        bootstrapCreationRequested = true;
        EnsureInstance(markBootstrap: false);
        bootstrapCreationRequested = false;
    }

    public static MouseCursorService EnsureInstance()
    {
        return EnsureInstance(markBootstrap: false);
    }

    private static MouseCursorService EnsureInstance(bool markBootstrap)
    {
        if (Instance != null)
            return Instance;

        MouseCursorService existing = FindFirstObjectByType<MouseCursorService>();
        if (existing != null)
            return existing;

        GameObject root = new GameObject(nameof(MouseCursorService));
        MouseCursorService service = root.AddComponent<MouseCursorService>();
        service.isBootstrapInstance = markBootstrap;
        return service;
    }

    private void Awake()
    {
        if (bootstrapCreationRequested)
            isBootstrapInstance = true;

        if (Instance != null && Instance != this)
        {
            if (Instance.isBootstrapInstance && !isBootstrapInstance)
            {
                Destroy(Instance.gameObject);
                Instance = null;
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        Instance = this;
        MouseCursorPlayback.RegisterBackend(this);
        MarkPersistent();
        EnsureThemeLoaded();
    }

    private void LateUpdate()
    {
        UpdateCursorReleaseIntent(Application.isFocused, Input.GetKeyDown(KeyCode.Escape),
            Input.GetMouseButtonDown(0), Input.mousePosition);
        RefreshCursorConfinement(Application.isFocused);
        EnsureThemeLoaded();
        PruneDeadOwners();
        RefreshDisplayState();
        ApplyResolvedCursor();
        LogPendingCursorDiagnostics();
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        DisableLegacyPresentation();
        forceCursorTextureReapply = true;
        RefreshCursorConfinement(Application.isFocused);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        RefreshCursorConfinement(hasFocus);
        if (hasFocus)
            forceCursorTextureReapply = true;
    }

    private void OnDisable()
    {
        if (Instance != this)
            return;

        Cursor.lockState = CursorLockMode.None;
        RestoreSystemCursor();
    }

    private void UpdateCursorReleaseIntent(bool hasFocus, bool escapePressed, bool pointerPressed, Vector2 pointer)
    {
        if (!hasFocus)
            return;

        // Escape wins if a click occurs in the same frame. Focus/display events retain this choice.
        if (escapePressed)
            cursorReleasedByEscape = true;
        else if (pointerPressed && pointer.x >= 0f && pointer.y >= 0f &&
                 pointer.x < Screen.width && pointer.y < Screen.height)
            cursorReleasedByEscape = false;
    }

    private void RefreshCursorConfinement(bool hasFocus)
    {
        if (Instance != this)
            return;

        CursorLockMode desired = hasFocus && isActiveAndEnabled && !cursorReleasedByEscape
            ? CursorLockMode.Confined
            : CursorLockMode.None;
        // Display changes or focus transitions can reset the OS cursor constraint.
        if (Cursor.lockState != desired)
            Cursor.lockState = desired;
    }

    private void OnDestroy()
    {
        MouseCursorPlayback.UnregisterBackend(this);

        if (Instance == this)
        {
            RestoreSystemCursor();
            Instance = null;
        }

        ReleaseGeneratedTextures();
    }

    private void MarkPersistent()
    {
        Transform persistentRoot = transform.root;
        if (persistentRoot == null)
            return;

        if (persistentRoot.parent != null)
            return;

        DontDestroyOnLoad(persistentRoot.gameObject);
    }

    public void SetDomain(UnityEngine.Object owner, MouseCursorDomain domain, int priority = 0)
    {
        if (owner == null)
            return;

        int ownerId = owner.GetInstanceID();
        if (!domainRequests.TryGetValue(ownerId, out DomainRequest request))
        {
            request = new DomainRequest();
            domainRequests.Add(ownerId, request);
        }

        request.owner = owner;
        request.domain = domain;
        request.priority = priority;
        request.order = ++nextOrder;
    }

    public void ClearDomain(UnityEngine.Object owner)
    {
        if (owner == null)
            return;

        domainRequests.Remove(owner.GetInstanceID());
    }

    public void SetInteractable(UnityEngine.Object owner, bool active)
    {
        SetOwnerFlag(interactableOwners, owner, active);
    }

    public void SetDragging(UnityEngine.Object owner, bool active)
    {
        SetOwnerFlag(draggingOwners, owner, active);
    }

    public void SetHidden(UnityEngine.Object owner, bool hidden)
    {
        SetOwnerFlag(hiddenOwners, owner, hidden);
    }

    public void NotifyDisplayConfigurationChanged()
    {
        LogCursorDiagnostic("display-request-before-cursor-reset");
        ScheduleCursorDiagnostics();
        ClearSystemCursorTexture();
        forceCursorTextureReapply = true;
        Cursor.visible = !HasAnyOwner(hiddenOwners);
    }

    private void SetOwnerFlag(Dictionary<int, OwnerFlag> owners, UnityEngine.Object owner, bool active)
    {
        if (owner == null)
            return;

        int ownerId = owner.GetInstanceID();
        if (!active)
        {
            owners.Remove(ownerId);
            return;
        }

        if (!owners.TryGetValue(ownerId, out OwnerFlag ownerFlag))
        {
            ownerFlag = new OwnerFlag();
            owners.Add(ownerId, ownerFlag);
        }

        ownerFlag.owner = owner;
    }

    private void DisableLegacyPresentation()
    {
        if (authoredCursorCanvas != null)
            authoredCursorCanvas.enabled = false;
        if (authoredCursorImage != null)
            authoredCursorImage.enabled = false;

        // Old authored children may exist without serialized references. Never create replacements.
        Transform legacyCanvas = transform.Find("MouseCursorCanvas");
        if (legacyCanvas != null)
            legacyCanvas.gameObject.SetActive(false);
    }

    private void EnsureThemeLoaded()
    {
        if (themeOverride != null || defaultThemeLoadAttempted)
            return;

        defaultThemeLoadAttempted = true;
        loadedTheme = Resources.Load<MouseCursorTheme>(DefaultThemeResourcePath);
        if (loadedTheme == null && !defaultThemeMissingLogged)
        {
            defaultThemeMissingLogged = true;
            CapstoneDiagnostics.EditorOnlyLog.LogWarning(
                $"[MouseCursorService] Default mouse cursor theme could not be loaded from Resources/{DefaultThemeResourcePath}.",
                this);
        }
    }

    private void PruneDeadOwners()
    {
        PruneDeadDomainRequests();
        PruneDeadOwnerFlags(interactableOwners);
        PruneDeadOwnerFlags(draggingOwners);
        PruneDeadOwnerFlags(hiddenOwners);
    }

    private void PruneDeadDomainRequests()
    {
        if (domainRequests.Count == 0)
            return;

        List<int> deadOwnerIds = null;
        foreach (KeyValuePair<int, DomainRequest> pair in domainRequests)
        {
            if (pair.Value != null && pair.Value.owner != null)
                continue;

            deadOwnerIds ??= new List<int>();
            deadOwnerIds.Add(pair.Key);
        }

        if (deadOwnerIds == null)
            return;

        for (int i = 0; i < deadOwnerIds.Count; i++)
            domainRequests.Remove(deadOwnerIds[i]);
    }

    private static void PruneDeadOwnerFlags(Dictionary<int, OwnerFlag> owners)
    {
        if (owners.Count == 0)
            return;

        List<int> deadOwnerIds = null;
        foreach (KeyValuePair<int, OwnerFlag> pair in owners)
        {
            if (pair.Value != null && pair.Value.owner != null)
                continue;

            deadOwnerIds ??= new List<int>();
            deadOwnerIds.Add(pair.Key);
        }

        if (deadOwnerIds == null)
            return;

        for (int i = 0; i < deadOwnerIds.Count; i++)
            owners.Remove(deadOwnerIds[i]);
    }

    private void ApplyResolvedCursor()
    {
        if (HasAnyOwner(hiddenOwners))
        {
            ClearSystemCursorTexture();
            Cursor.visible = false;
            return;
        }

        currentDomain = ResolveDomain();
        currentVariant = ResolveVariant();

        MouseCursorSpriteDefinition definition = ResolveDefinition(currentDomain, currentVariant);
        if (definition == null || definition.sprite == null)
        {
            RestoreSystemCursor();
            return;
        }

        if (TryApplyNativeCursor(definition))
            Cursor.visible = true;
        else
            RestoreSystemCursor();
    }

    private MouseCursorDomain ResolveDomain()
    {
        MouseCursorDomain resolved = MouseCursorDomain.Combat;
        int highestPriority = int.MinValue;
        long latestOrder = long.MinValue;

        if (DialoguePlayback.IsPlaying)
        {
            resolved = MouseCursorDomain.NpcUi;
            highestPriority = DialogueDomainPriority;
        }

        foreach (DomainRequest request in domainRequests.Values)
        {
            if (request == null || request.owner == null)
                continue;

            if (request.priority < highestPriority)
                continue;

            if (request.priority == highestPriority && request.order <= latestOrder)
                continue;

            highestPriority = request.priority;
            latestOrder = request.order;
            resolved = request.domain;
        }

        return resolved;
    }

    private MouseCursorVariant ResolveVariant()
    {
        if (HasAnyOwner(draggingOwners))
            return MouseCursorVariant.Dragging;

        bool hasInteractableOwner = HasAnyOwner(interactableOwners);
        bool isMousePressed = IsAnyMouseButtonPressed();
        if (hasInteractableOwner && isMousePressed)
            return MouseCursorVariant.InteractablePressed;

        if (isMousePressed)
        {
            if (currentDomain == MouseCursorDomain.SystemUi && IsPointerOverInteractableSystemUi())
                return MouseCursorVariant.InteractablePressed;

            return MouseCursorVariant.Pressed;
        }

        if (hasInteractableOwner)
            return MouseCursorVariant.Interactable;

        if (currentDomain == MouseCursorDomain.SystemUi && IsPointerOverInteractableSystemUi())
            return MouseCursorVariant.Interactable;

        return MouseCursorVariant.Default;
    }

    private static bool HasAnyOwner(Dictionary<int, OwnerFlag> owners)
    {
        foreach (OwnerFlag ownerFlag in owners.Values)
        {
            if (ownerFlag != null && ownerFlag.owner != null)
                return true;
        }

        return false;
    }

    private static bool IsAnyMouseButtonPressed()
    {
        return Input.GetMouseButton(0) || Input.GetMouseButton(1) || Input.GetMouseButton(2);
    }

    private bool IsPointerOverInteractableSystemUi()
    {
        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (pointerEventData == null || pointerEventSystem != eventSystem)
        {
            pointerEventData = new PointerEventData(eventSystem);
            pointerEventSystem = eventSystem;
        }

        pointerEventData.Reset();
        pointerEventData.position = Input.mousePosition;

        uiRaycastResults.Clear();
        eventSystem.RaycastAll(pointerEventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject target = uiRaycastResults[i].gameObject;
            if (target == null)
                continue;

            Selectable selectable = target.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsInteractable() && selectable.IsActive())
                return true;
        }

        return false;
    }

    private MouseCursorSpriteDefinition ResolveDefinition(MouseCursorDomain domain, MouseCursorVariant variant)
    {
        MouseCursorSpriteDefinition definition = GetDefinition(domain, variant);
        if (HasSprite(definition))
            return definition;

        if (variant != MouseCursorVariant.Default)
        {
            definition = GetDefinition(domain, MouseCursorVariant.Default);
            if (HasSprite(definition))
                return definition;
        }

        if (domain != MouseCursorDomain.Combat)
        {
            definition = GetDefinition(MouseCursorDomain.Combat, variant);
            if (HasSprite(definition))
                return definition;

            if (variant != MouseCursorVariant.Default)
            {
                definition = GetDefinition(MouseCursorDomain.Combat, MouseCursorVariant.Default);
                if (HasSprite(definition))
                    return definition;
            }
        }

        return null;
    }

    private MouseCursorSpriteDefinition GetDefinition(MouseCursorDomain domain, MouseCursorVariant variant)
    {
        MouseCursorTheme theme = themeOverride != null ? themeOverride : loadedTheme;
        return theme != null ? theme.GetDefinition(domain, variant) : null;
    }

    private static bool HasSprite(MouseCursorSpriteDefinition definition)
    {
        return definition != null && definition.sprite != null;
    }

    private bool TryApplyNativeCursor(MouseCursorSpriteDefinition definition)
    {
        if (!TryResolveCursorTexture(definition, out Texture2D texture, out Vector2 hotspot))
            return false;

        if (!forceCursorTextureReapply && appliedCursorTexture == texture && appliedCursorHotspot == hotspot)
            return true;

        Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        appliedCursorTexture = texture;
        appliedCursorHotspot = hotspot;
        forceCursorTextureReapply = false;
        return true;
    }

    private bool TryResolveCursorTexture(MouseCursorSpriteDefinition definition, out Texture2D texture, out Vector2 hotspot)
    {
        texture = null;
        hotspot = Vector2.zero;
        Sprite sprite = definition.sprite;
        if (sprite == null || sprite.texture == null)
            return false;

        int spriteId = sprite.GetInstanceID();
        if (!generatedCursorTextures.TryGetValue(spriteId, out CursorTexture cached))
        {
            cached = new CursorTexture
            {
                sourceWidth = Mathf.RoundToInt(sprite.rect.width),
                sourceHeight = Mathf.RoundToInt(sprite.rect.height)
            };
            cached.pixels = ReadSpritePixels(sprite, cached.sourceWidth, cached.sourceHeight);
            generatedCursorTextures.Add(spriteId, cached);
        }

        // Match Image.SetNativeSize on the former default Canvas (100 reference pixels/unit).
        float scale = ResolveCursorScale(definition) * 100f / sprite.pixelsPerUnit;
        int width = Mathf.Max(1, Mathf.RoundToInt(cached.sourceWidth * scale));
        int height = Mathf.Max(1, Mathf.RoundToInt(cached.sourceHeight * scale));
        if (cached.texture == null || cached.texture.width != width || cached.texture.height != height)
        {
            Color32[] pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                int sourceY = Mathf.Min(cached.sourceHeight - 1, (int)((y + 0.5f) * cached.sourceHeight / height));
                for (int x = 0; x < width; x++)
                {
                    int sourceX = Mathf.Min(cached.sourceWidth - 1, (int)((x + 0.5f) * cached.sourceWidth / width));
                    pixels[y * width + x] = cached.pixels[sourceY * cached.sourceWidth + sourceX];
                }
            }

            Texture2D resized = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
            resized.name = $"{sprite.name}_Cursor_{width}x{height}";
            resized.filterMode = FilterMode.Point;
            resized.wrapMode = TextureWrapMode.Clamp;
            resized.SetPixels32(pixels);
            resized.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            if (cached.texture != null)
                Destroy(cached.texture);
            cached.texture = resized;
        }

        texture = cached.texture;
        hotspot = new Vector2(
            Mathf.Clamp(definition.hotspotPixels.x * width / cached.sourceWidth, 0f, width - 1f),
            Mathf.Clamp(definition.hotspotPixels.y * height / cached.sourceHeight, 0f, height - 1f));
        return true;
    }

    private static Color32[] ReadSpritePixels(Sprite sprite, int width, int height)
    {
        // GPU readback also supports non-readable PNG/Aseprite imports without asset migration.
        Texture2D source = sprite.texture;
        RenderTexture previous = RenderTexture.active;
        bool previousSrgbWrite = GL.sRGBWrite;
        RenderTexture temporary = RenderTexture.GetTemporary(
            source.width, source.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        Texture2D readable = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
        try
        {
            GL.sRGBWrite = QualitySettings.activeColorSpace == ColorSpace.Linear;
            Graphics.Blit(source, temporary);
            RenderTexture.active = temporary;
            // Preserve transparent padding and hotspots for trimmed, non-rotated sprites.
            readable.SetPixels32(new Color32[width * height]);
            Vector2 offset = sprite.textureRectOffset;
            readable.ReadPixels(sprite.textureRect, Mathf.RoundToInt(offset.x), Mathf.RoundToInt(offset.y), false);
            readable.Apply(updateMipmaps: false, makeNoLongerReadable: false);
            return readable.GetPixels32();
        }
        finally
        {
            RenderTexture.active = previous;
            GL.sRGBWrite = previousSrgbWrite;
            RenderTexture.ReleaseTemporary(temporary);
            Destroy(readable);
        }
    }

    private void RefreshDisplayState()
    {
        int screenWidth = Screen.width;
        int screenHeight = Screen.height;
        FullScreenMode fullScreenMode = Screen.fullScreenMode;

        if (!hasCapturedDisplayState)
        {
            lastScreenWidth = screenWidth;
            lastScreenHeight = screenHeight;
            lastFullScreenMode = fullScreenMode;
            hasCapturedDisplayState = true;
            ScheduleCursorDiagnostics();
            return;
        }

        if (lastScreenWidth == screenWidth &&
            lastScreenHeight == screenHeight &&
            lastFullScreenMode == fullScreenMode)
        {
            return;
        }

        lastScreenWidth = screenWidth;
        lastScreenHeight = screenHeight;
        lastFullScreenMode = fullScreenMode;
        ScheduleCursorDiagnostics();
        forceCursorTextureReapply = true;
    }

    // Diagnostic snapshots only: do not alter cursor recovery or rendering behavior.
    private void ScheduleCursorDiagnostics()
    {
        cursorDiagnosticPending = true;
        cursorDiagnosticSettling = true;
        cursorDiagnosticDeadline = Time.realtimeSinceStartup + 2f;
    }

    private void LogPendingCursorDiagnostics()
    {
        if (cursorDiagnosticPending)
        {
            cursorDiagnosticPending = false;
            LogCursorDiagnostic("display-observed-after-cursor-update");
        }

        if (cursorDiagnosticSettling && Time.realtimeSinceStartup >= cursorDiagnosticDeadline)
        {
            cursorDiagnosticSettling = false;
            LogCursorDiagnostic("display-stable-2s");
        }
    }

    private void LogCursorDiagnostic(string phase)
    {
        Debug.Log($"[CursorDiagnostic] phase={phase} frame={Time.frameCount} " +
            $"screen={Screen.width}x{Screen.height} mode={Screen.fullScreenMode} " +
            $"focused={Application.isFocused} visible={Cursor.visible} lock={Cursor.lockState} " +
            $"domain={currentDomain}/{currentVariant} hiddenOwners={hiddenOwners.Count} " +
            $"backend=UnityCursorAPI cursorMode=Auto " +
            $"texture={(appliedCursorTexture != null ? appliedCursorTexture.name : "system")} " +
            $"hotspot={appliedCursorHotspot}", this);
    }

    private float ResolveCursorScale(MouseCursorSpriteDefinition definition)
    {
        float authoredScale = definition != null ? Mathf.Max(0.1f, definition.scale) : 1f;
        if (!scaleWithScreenHeight)
            return authoredScale;

        float safeReferenceHeight = Mathf.Max(1f, referenceScreenHeight);
        float resolutionScale = Mathf.Clamp(Screen.height / safeReferenceHeight, minResolutionScale, maxResolutionScale);
        return authoredScale * resolutionScale;
    }

    private void RestoreSystemCursor()
    {
        if (appliedCursorTexture == null && appliedCursorHotspot == new Vector2(float.MinValue, float.MinValue))
        {
            Cursor.visible = true;
            return;
        }

        appliedCursorTexture = null;
        appliedCursorHotspot = new Vector2(float.MinValue, float.MinValue);
        forceCursorTextureReapply = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
    }

    private void ClearSystemCursorTexture()
    {
        if (appliedCursorTexture == null && appliedCursorHotspot == new Vector2(float.MinValue, float.MinValue))
            return;

        appliedCursorTexture = null;
        appliedCursorHotspot = new Vector2(float.MinValue, float.MinValue);
        forceCursorTextureReapply = false;
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void ReleaseGeneratedTextures()
    {
        foreach (CursorTexture cached in generatedCursorTextures.Values)
        {
            if (cached.texture != null)
                Destroy(cached.texture);
        }

        generatedCursorTextures.Clear();
    }
}
