using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Projects scene-owned objectives and mapped glyphs above the player using authored UI.</summary>
public sealed class PrototypeTutorialPromptView : MonoBehaviour
{
    [SerializeField] private PrototypeTutorialUpgrade tutorial;
    [SerializeField] private TMP_Text prompt;
    [SerializeField] private Image dashGlyph;
    [SerializeField] private Image[] additionalMovementGlyphs = new Image[3];
    [SerializeField] private Sprite[] highlightedKeyGlyphs = System.Array.Empty<Sprite>();
    [SerializeField] private Vector3 playerOffset = new Vector3(0f, 1.5f, 0f);
    private static readonly InputActionId[] MovementActions =
        { InputActionId.MoveUp, InputActionId.MoveLeft, InputActionId.MoveDown, InputActionId.MoveRight };
    private Canvas canvas;
    private readonly System.Collections.Generic.Dictionary<string, Sprite> highlightedGlyphLookup = new();
    private float promptStartedAt;
    private int visibleStage = -1;
    private InputActionId visibleAction;
    private bool highlightPulse;

    private void Awake()
    {
        canvas = GetComponent<Canvas>();
        foreach (Sprite glyph in highlightedKeyGlyphs)
            if (glyph != null) highlightedGlyphLookup[glyph.name] = glyph;
        if (prompt == null) return;
        prompt.rectTransform.localScale *= 1.5f;
        prompt.rectTransform.pivot = new Vector2(0.5f, 0.5f);
        prompt.fontStyle |= FontStyles.Bold;
        prompt.richText = true;
        prompt.enableAutoSizing = false;
        prompt.fontSize *= 1.1f;
        prompt.textWrappingMode = TextWrappingModes.NoWrap;
        prompt.alignment = TextAlignmentOptions.MidlineLeft;
    }
    private void OnEnable() => Refresh();
    private void LateUpdate() => Refresh();
    private void OnDisable()
    {
        visibleStage = -1;
        PlayerOverheadPromptLayout.Remove(this);
        if (prompt != null) prompt.gameObject.SetActive(false);
    }

    private void Refresh()
    {
        if (prompt == null) return;
        Transform player = PlayerRuntimeRegistry.GetPlayerTransform();
        Camera camera = Camera.main;
        bool visible = tutorial != null && tutorial.isActiveAndEnabled &&
            !tutorial.IsOpeningCinematic &&
            (tutorial.Stage == 0 || (tutorial.Stage == 1 && tutorial.IsDashPromptVisible) ||
             tutorial.Stage == 2 || tutorial.Stage == 3 || tutorial.Stage == 4) &&
            player != null && camera != null && (UIManager.Instance == null || !UIManager.Instance.HasActivePopup());
        Vector3 screenPoint = visible ? camera.WorldToScreenPoint(player.position + playerOffset) : Vector3.zero;
        visible &= screenPoint.z > 0f;
        prompt.gameObject.SetActive(visible);
        if (!visible)
        {
            visibleStage = -1;
            PlayerOverheadPromptLayout.Remove(this);
            return;
        }

        if (canvas == null) canvas = GetComponent<Canvas>();
        RectTransform parent = prompt.rectTransform.parent as RectTransform;
        Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (parent != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPoint, uiCamera, out Vector3 position))
            prompt.rectTransform.position = position;

        InputBindingService bindings = InputBindingService.EnsureInstance();
        string fallback = string.Empty;
        bool movement = tutorial.Stage == 0;
        if (visibleStage != tutorial.Stage || visibleAction != tutorial.PromptAction)
        {
            visibleStage = tutorial.Stage;
            visibleAction = tutorial.PromptAction;
            promptStartedAt = Time.unscaledTime;
        }
        highlightPulse = Mathf.FloorToInt((Time.unscaledTime - promptStartedAt) / 0.3f) % 2 == 1;
        const float gap = 4f;
        float glyphWidth = movement ? 132f : 88f;
        float glyphCenter = -gap - glyphWidth * 0.5f;
        float singleGlyphWidth = SetGlyph(dashGlyph, movement ? MovementActions[0] : tutorial.PromptAction,
            new Vector2(glyphCenter, movement ? 24f : 0f), movement, bindings, ref fallback);
        if (!movement) glyphWidth = singleGlyphWidth;
        for (int i = 0; i < additionalMovementGlyphs.Length; i++)
        {
            Image image = additionalMovementGlyphs[i];
            if (image == null) continue;
            image.gameObject.SetActive(movement && i < 3);
            if (movement && i < 3) SetGlyph(image, MovementActions[i + 1],
                new Vector2(glyphCenter + (i - 1) * 46f, -24f), true, bindings, ref fallback);
        }
        prompt.text = (string.IsNullOrEmpty(fallback) ? tutorial.PromptInstruction : fallback + " " + tutorial.PromptInstruction)
            .Replace('\n', ' ').Replace('\r', ' ')
            .Replace("길게", "<color=#FF4444>길게</color>");
        prompt.rectTransform.sizeDelta = new Vector2(prompt.preferredWidth, movement ? 92f : 70f);
        // Center the complete glyph-and-caption row over the player.
        prompt.rectTransform.position += prompt.rectTransform.TransformVector(Vector3.right * ((glyphWidth + gap) * 0.5f));
        PlayerOverheadPromptLayout.Place(this, prompt.rectTransform, canvas, PlayerOverheadPromptLayout.Tutorial);
    }

    private float SetGlyph(Image image, InputActionId action, Vector2 position, bool movement,
        InputBindingService bindings, ref string fallback)
    {
        InputGlyphPresentation glyph = bindings.GetBindingGlyph(action);
        if (glyph.Key == KeyCode.None) glyph = bindings.GetBindingGlyph(action, true);
        if (movement && !InputActionQuery.IsKeyPressed(glyph.Key))
        {
            InputGlyphPresentation secondary = bindings.GetBindingGlyph(action, true);
            if (secondary.Key != KeyCode.None && InputActionQuery.IsKeyPressed(secondary.Key)) glyph = secondary;
        }
        // Match the visible aspect-fitted width instead of reserving an empty 88-wide box.
        float width = movement ? 40f : glyph.HasIcon
            ? Mathf.Min(88f, 44f * glyph.Icon.rect.width / glyph.Icon.rect.height) : 0f;
        if (!movement) position.x += (88f - width) * 0.5f;
        if (image != null)
        {
            bool highlighted = movement ? InputActionQuery.IsKeyPressed(glyph.Key) : highlightPulse;
            image.sprite = highlighted && glyph.HasIcon && highlightedGlyphLookup.TryGetValue(glyph.Icon.name, out Sprite point)
                ? point : glyph.Icon;
            image.gameObject.SetActive(glyph.HasIcon);
            image.rectTransform.anchorMin = image.rectTransform.anchorMax = new Vector2(0f, 0.5f);
            image.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            image.rectTransform.anchoredPosition = position;
            image.rectTransform.sizeDelta = new Vector2(width, 44f);
        }
        if (!glyph.HasIcon) fallback += (fallback.Length > 0 ? " " : string.Empty) + glyph.DisplayLabel;
        return width;
    }
}

// Layout only: presenters retain visibility, authored references and gameplay ownership.
internal static class PlayerOverheadPromptLayout
{
    internal const int Tutorial = 0;
    internal const int WeaponSwap = 1;
    internal const int LevelUp = 2;

    private struct Entry
    {
        internal MonoBehaviour Owner;
        internal RectTransform Rect;
        internal Canvas Canvas;
        internal Vector3 BasePosition;
    }

    private static readonly Entry[] Entries = new Entry[3];
    private static readonly Vector3[] Corners = new Vector3[4];

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => System.Array.Clear(Entries, 0, Entries.Length);

    internal static void Place(MonoBehaviour owner, RectTransform rect, Canvas canvas, int priority)
    {
        Entries[priority] = new Entry { Owner = owner, Rect = rect, Canvas = canvas, BasePosition = rect.position };
        Reflow();
    }

    internal static void Remove(MonoBehaviour owner)
    {
        for (int i = 0; i < Entries.Length; i++)
            if (ReferenceEquals(Entries[i].Owner, owner)) Entries[i] = default;
        Reflow();
    }

    private static void Reflow()
    {
        float previousTop = float.NegativeInfinity;
        for (int i = 0; i < Entries.Length; i++)
        {
            Entry entry = Entries[i];
            if (entry.Owner == null || !entry.Owner.isActiveAndEnabled || entry.Rect == null ||
                !entry.Rect.gameObject.activeInHierarchy || entry.Canvas == null || !entry.Canvas.isActiveAndEnabled)
            {
                Entries[i] = default;
                continue;
            }

            Canvas root = entry.Canvas.rootCanvas;
            Camera camera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            entry.Rect.position = entry.BasePosition;
            entry.Rect.GetWorldCorners(Corners);
            float bottom = float.PositiveInfinity;
            float top = float.NegativeInfinity;
            for (int corner = 0; corner < Corners.Length; corner++)
            {
                float y = RectTransformUtility.WorldToScreenPoint(camera, Corners[corner]).y;
                bottom = Mathf.Min(bottom, y);
                top = Mathf.Max(top, y);
            }

            float lift = Mathf.Max(0f, previousTop + 12f - bottom);
            if (lift > 0f && entry.Rect.parent is RectTransform parent)
            {
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(camera, entry.BasePosition);
                screen.y += lift;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screen, camera, out Vector3 position))
                    entry.Rect.position = position;
            }
            previousTop = top + lift;
        }
    }
}
