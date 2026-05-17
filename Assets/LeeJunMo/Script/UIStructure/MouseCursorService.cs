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
    SystemUi = 3
}

public enum MouseCursorVariant
{
    Default = 0,
    Interactable = 1,
    Pressed = 2,
    Dragging = 3
}

[Serializable]
public sealed class MouseCursorSpriteDefinition
{
    public Sprite sprite;
    public Vector2 hotspotPixels;
    [Min(0.1f)] public float scale = 1f;
}

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
            MouseCursorVariant.Dragging => draggingCursor,
            _ => defaultCursor
        };
    }
}

[DefaultExecutionOrder(1000)]
[DisallowMultipleComponent]
public sealed class MouseCursorService : MonoBehaviour
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

    private sealed class OwnerFlag
    {
        public UnityEngine.Object owner;
    }

    public static MouseCursorService Instance { get; private set; }
    private static bool bootstrapCreationRequested;

    [Header("Theme")]
    [SerializeField] private MouseCursorTheme themeOverride;

    [Header("Authoring")]
    [SerializeField] private Canvas authoredCursorCanvas;
    [SerializeField] private RectTransform authoredCursorRect;
    [SerializeField] private Image authoredCursorImage;

    [Header("Software Cursor Fallback")]
    [SerializeField] private bool hideSystemCursorWhileSpriteActive = true;
    [SerializeField] private int overlaySortingOrder = short.MaxValue;

    private readonly Dictionary<int, DomainRequest> domainRequests = new Dictionary<int, DomainRequest>();
    private readonly Dictionary<int, OwnerFlag> interactableOwners = new Dictionary<int, OwnerFlag>();
    private readonly Dictionary<int, OwnerFlag> draggingOwners = new Dictionary<int, OwnerFlag>();
    private readonly Dictionary<int, Texture2D> generatedCursorTextures = new Dictionary<int, Texture2D>();
    private readonly HashSet<int> unreadableSpriteWarnings = new HashSet<int>();
    private readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>(8);

    private Canvas cursorCanvas;
    private RectTransform cursorRect;
    private Image cursorImage;
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
        MarkPersistent();
        EnsureThemeLoaded();
    }

    private void LateUpdate()
    {
        EnsureThemeLoaded();
        PruneDeadOwners();
        ApplyResolvedCursor();
        UpdateCursorPosition();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;

        RestoreSystemCursor();
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

    private void EnsureRuntimePresentation()
    {
        if (cursorCanvas != null && cursorRect != null && cursorImage != null)
            return;

        if (TryBindAuthoredRuntimePresentation())
            return;

        Transform canvasTransform = transform.Find("MouseCursorCanvas");
        if (canvasTransform == null)
        {
            RuntimePresentationFallbackAudit.Record(
                this,
                "Mouse cursor canvas fallback",
                "an authored cursor canvas/image under the cursor service prefab or global UI root");

            GameObject canvasObject = new GameObject("MouseCursorCanvas", typeof(RectTransform), typeof(Canvas));
            canvasTransform = canvasObject.transform;
            canvasTransform.SetParent(transform, false);
        }

        Canvas canvas = canvasTransform.GetComponent<Canvas>();
        if (canvas == null)
            canvas = canvasTransform.gameObject.AddComponent<Canvas>();

        cursorCanvas = canvas;
        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.overrideSorting = true;
        cursorCanvas.sortingOrder = overlaySortingOrder;

        Transform imageTransform = canvasTransform.Find("CursorImage");
        if (imageTransform == null)
        {
            GameObject imageObject = new GameObject("CursorImage", typeof(RectTransform), typeof(Image));
            imageTransform = imageObject.transform;
            imageTransform.SetParent(canvasTransform, false);
        }

        cursorRect = imageTransform as RectTransform;
        cursorImage = imageTransform.GetComponent<Image>();
        if (cursorImage != null)
            cursorImage.raycastTarget = false;

        if (cursorRect != null)
        {
            cursorRect.anchorMin = Vector2.zero;
            cursorRect.anchorMax = Vector2.zero;
            cursorRect.anchoredPosition = Vector2.zero;
        }
    }

    private bool TryBindAuthoredRuntimePresentation()
    {
        if (authoredCursorCanvas == null || authoredCursorImage == null)
            return false;

        cursorCanvas = authoredCursorCanvas;
        cursorImage = authoredCursorImage;
        cursorRect = authoredCursorRect != null ? authoredCursorRect : authoredCursorImage.rectTransform;
        if (cursorRect == null)
            return false;

        cursorCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        cursorCanvas.overrideSorting = true;
        cursorCanvas.sortingOrder = overlaySortingOrder;
        cursorImage.raycastTarget = false;
        cursorRect.anchorMin = Vector2.zero;
        cursorRect.anchorMax = Vector2.zero;
        cursorRect.anchoredPosition = Vector2.zero;
        return true;
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
            Debug.LogWarning(
                $"[MouseCursorService] Default mouse cursor theme could not be loaded from Resources/{DefaultThemeResourcePath}.",
                this);
        }
    }

    private void PruneDeadOwners()
    {
        PruneDeadDomainRequests();
        PruneDeadOwnerFlags(interactableOwners);
        PruneDeadOwnerFlags(draggingOwners);
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
        currentDomain = ResolveDomain();
        currentVariant = ResolveVariant();

        MouseCursorSpriteDefinition definition = ResolveDefinition(currentDomain, currentVariant);
        if (definition == null || definition.sprite == null)
        {
            HideSoftwareCursor();
            RestoreSystemCursor();
            return;
        }

        if (TryApplyHardwareCursor(definition))
        {
            HideSoftwareCursor();
            Cursor.visible = true;
            return;
        }

        ApplySoftwareCursor(definition);
    }

    private MouseCursorDomain ResolveDomain()
    {
        MouseCursorDomain resolved = MouseCursorDomain.Combat;
        int highestPriority = int.MinValue;
        long latestOrder = long.MinValue;

        if (DialogueService.Instance != null && DialogueService.Instance.IsPlaying)
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

        if (IsAnyMouseButtonPressed())
            return MouseCursorVariant.Pressed;

        if (HasAnyOwner(interactableOwners))
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
        MouseCursorSpriteDefinition definition = GetDomainDefinition(domain)?.GetDefinition(variant);
        if (HasSprite(definition))
            return definition;

        if (variant != MouseCursorVariant.Default)
        {
            definition = GetDomainDefinition(domain)?.GetDefinition(MouseCursorVariant.Default);
            if (HasSprite(definition))
                return definition;
        }

        if (domain != MouseCursorDomain.Combat)
        {
            definition = GetDomainDefinition(MouseCursorDomain.Combat)?.GetDefinition(variant);
            if (HasSprite(definition))
                return definition;

            if (variant != MouseCursorVariant.Default)
            {
                definition = GetDomainDefinition(MouseCursorDomain.Combat)?.GetDefinition(MouseCursorVariant.Default);
                if (HasSprite(definition))
                    return definition;
            }
        }

        return null;
    }

    private MouseCursorDomainDefinition GetDomainDefinition(MouseCursorDomain domain)
    {
        MouseCursorTheme theme = themeOverride != null ? themeOverride : loadedTheme;
        return theme != null ? theme.GetDomainDefinition(domain) : null;
    }

    private static bool HasSprite(MouseCursorSpriteDefinition definition)
    {
        return definition != null && definition.sprite != null;
    }

    private bool TryApplyHardwareCursor(MouseCursorSpriteDefinition definition)
    {
        if (!TryResolveCursorTexture(definition, out Texture2D texture, out Vector2 hotspot))
            return false;

        if (appliedCursorTexture == texture && appliedCursorHotspot == hotspot)
            return true;

        Cursor.SetCursor(texture, hotspot, CursorMode.Auto);
        appliedCursorTexture = texture;
        appliedCursorHotspot = hotspot;
        return true;
    }

    private bool TryResolveCursorTexture(MouseCursorSpriteDefinition definition, out Texture2D texture, out Vector2 hotspot)
    {
        texture = null;
        hotspot = definition.hotspotPixels;

        Sprite sprite = definition.sprite;
        if (sprite == null)
            return false;

        Texture2D sourceTexture = sprite.texture;
        if (sourceTexture == null)
            return false;

        Rect rect = sprite.rect;
        bool usesFullTexture = Mathf.Approximately(rect.x, 0f) &&
                               Mathf.Approximately(rect.y, 0f) &&
                               Mathf.Approximately(rect.width, sourceTexture.width) &&
                               Mathf.Approximately(rect.height, sourceTexture.height);
        if (usesFullTexture)
        {
            texture = sourceTexture;
            return true;
        }

        int spriteId = sprite.GetInstanceID();
        if (generatedCursorTextures.TryGetValue(spriteId, out Texture2D cachedTexture) && cachedTexture != null)
        {
            texture = cachedTexture;
            return true;
        }

        if (!sourceTexture.isReadable)
        {
            if (unreadableSpriteWarnings.Add(spriteId))
            {
                Debug.LogWarning(
                    $"[MouseCursorService] Sprite '{sprite.name}' uses only a sub-rect of a non-readable texture. Falling back to software cursor rendering.",
                    this);
            }

            return false;
        }

        int width = Mathf.RoundToInt(rect.width);
        int height = Mathf.RoundToInt(rect.height);
        if (width <= 0 || height <= 0)
            return false;

        Color[] pixels = sourceTexture.GetPixels(
            Mathf.RoundToInt(rect.x),
            Mathf.RoundToInt(rect.y),
            width,
            height);

        Texture2D generatedTexture = new Texture2D(width, height, TextureFormat.RGBA32, mipChain: false);
        generatedTexture.name = $"{sprite.name}_CursorTexture";
        generatedTexture.filterMode = FilterMode.Point;
        generatedTexture.wrapMode = TextureWrapMode.Clamp;
        generatedTexture.SetPixels(pixels);
        generatedTexture.Apply(updateMipmaps: false, makeNoLongerReadable: false);

        generatedCursorTextures[spriteId] = generatedTexture;
        texture = generatedTexture;
        return true;
    }

    private void ApplySoftwareCursor(MouseCursorSpriteDefinition definition)
    {
        EnsureRuntimePresentation();
        if (cursorImage == null || cursorRect == null)
            return;

        Sprite sprite = definition.sprite;
        if (sprite == null)
            return;

        cursorImage.sprite = sprite;
        cursorImage.enabled = true;
        cursorImage.SetNativeSize();
        cursorRect.localScale = Vector3.one * Mathf.Max(0.1f, definition.scale);
        cursorRect.pivot = ResolvePivot(sprite, definition.hotspotPixels);
        cursorRect.SetAsLastSibling();

        if (cursorCanvas != null)
            cursorCanvas.enabled = true;

        Cursor.visible = !hideSystemCursorWhileSpriteActive;
        appliedCursorTexture = null;
        appliedCursorHotspot = new Vector2(float.MinValue, float.MinValue);
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
    }

    private void UpdateCursorPosition()
    {
        if (cursorCanvas == null || cursorRect == null || cursorImage == null || !cursorImage.enabled)
            return;

        cursorRect.position = Input.mousePosition;
    }

    private void HideSoftwareCursor()
    {
        if (cursorCanvas != null)
            cursorCanvas.enabled = false;

        if (cursorImage != null)
            cursorImage.enabled = false;
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
        Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        Cursor.visible = true;
    }

    private void ReleaseGeneratedTextures()
    {
        foreach (Texture2D texture in generatedCursorTextures.Values)
        {
            if (texture != null)
                Destroy(texture);
        }

        generatedCursorTextures.Clear();
        unreadableSpriteWarnings.Clear();
    }

    private static Vector2 ResolvePivot(Sprite sprite, Vector2 hotspotPixels)
    {
        if (sprite == null)
            return new Vector2(0f, 1f);

        Rect rect = sprite.rect;
        if (rect.width <= 0f || rect.height <= 0f)
            return new Vector2(0f, 1f);

        float pivotX = Mathf.Clamp01(hotspotPixels.x / rect.width);
        float pivotY = Mathf.Clamp01(1f - (hotspotPixels.y / rect.height));
        return new Vector2(pivotX, pivotY);
    }
}
