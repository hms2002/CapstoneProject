using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>Authored monster HUD: HP projection plus left-aligned stack/time status slots.</summary>
[DisallowMultipleComponent]
public sealed class MonsterStackStatusWorldView : MonoBehaviour
{
    // Presentation-only membership: final routing observes every actor after its normal LateUpdate.
    private static readonly List<MonsterStackStatusWorldView> enabledMeshViews = new(128);
    private static readonly Vector3[] worldCorners = new Vector3[4];
    private static readonly int gradientScaleId = Shader.PropertyToID("_GradientScale");
    private static readonly int bitmapPaddingId = Shader.PropertyToID("_Padding");
    [Serializable]
    private sealed class StatusSlot
    {
        public string statusId;
        public RectTransform root;
        public Transform icon;
        public TMP_Text valueText;
        public float width;
        [NonSerialized] public IMonsterStatusSource source;
        [NonSerialized] public float pulseUntil;
        [NonSerialized] public HudDisplayTextCache displayText = new();
        public void Pulse() => pulseUntil = Time.time + 0.09f;
        public void Bind(IMonsterStatusSource next)
        {
            if (ReferenceEquals(source, next)) return;
            if (source != null) source.PulseRequested -= Pulse;
            source = next;
            pulseUntil = 0f;
            (displayText ??= new HudDisplayTextCache()).Clear();
            if (source != null) source.PulseRequested += Pulse;
        }
    }

    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private RectTransform healthBar;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damageTrail;
    [SerializeField] private MonsterHealthBarMeshView meshHealthBar;
    [SerializeField] private Canvas statusCanvas;
    [SerializeField] private RectTransform staggerBar;
    [SerializeField] private Image staggerFill;
    [SerializeField, Min(0f)] private float damageTrailDelay = 0.3f;
    [SerializeField, Min(0.01f)] private float damageTrailDuration = 0.5f;
    [SerializeField] private RectTransform statusRow;
    [SerializeField] private AttributeDefinition healthAttribute;
    [SerializeField] private AttributeDefinition maxHealthAttribute;
    [SerializeField] private float worldScale = 0.014f;
    [SerializeField] private float statusGap = 5f;
    [SerializeField] private StatusSlot[] slots;
    private Enemy enemy;
    private AttributeSet attributes;
    private MonsterSizeProfile sizeProfile;
    private MonsterStatusRuntime statuses;
    private StaggerGaugeSystem staggerGauge;
    private GameplayEffectRunner effectRunner;
    private bool healthInitialized;
    private float healthRatio;
    private float trailRatio;
    private float trailStartRatio;
    private float lastDamageTime;
    private bool presentationVisible;
    private bool anyActiveStatus;
    private bool hasProjectionBounds;
    private Rect projectionBounds;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        sizeProfile = GetComponentInParent<MonsterSizeProfile>();
        if (sizeProfile == null) return;
        attributes = sizeProfile.GetComponent<AttributeSet>();
        statuses = sizeProfile.GetComponent<MonsterStatusRuntime>();
        staggerGauge = sizeProfile.GetComponent<StaggerGaugeSystem>();
        effectRunner = sizeProfile.GetComponent<GameplayEffectRunner>();
    }

    private void OnEnable()
    {
        healthInitialized = false;
        if (attributes != null) attributes.OnAttributeChanged += OnAttributeChanged;
        if (enemy != null) enemy.DeathStarted += OnDeath;
        if (staggerGauge != null) staggerGauge.OnGaugeChanged += OnStaggerGaugeChanged;
        RefreshHealth();
        if (UsesMeshHealth && !enabledMeshViews.Contains(this))
        {
            if (enabledMeshViews.Count == 0) Canvas.preWillRenderCanvases += ResolveHealthProjection;
            enabledMeshViews.Add(this);
        }
        RefreshStagger();
    }

    private void OnDisable()
    {
        UnregisterProjection();
        presentationVisible = anyActiveStatus = false;
        healthInitialized = false;
        if (attributes != null) attributes.OnAttributeChanged -= OnAttributeChanged;
        if (enemy != null) enemy.DeathStarted -= OnDeath;
        if (staggerGauge != null) staggerGauge.OnGaugeChanged -= OnStaggerGaugeChanged;
        if (slots != null) foreach (var slot in slots) slot.Bind(null);
        if (meshHealthBar != null)
        {
            meshHealthBar.SetLegacyProjection(false);
            meshHealthBar.Hide();
        }
        if (UsesMeshHealth && statusCanvas != null) statusCanvas.enabled = false;
    }

    private void OnDestroy() => UnregisterProjection();

    private void UnregisterProjection()
    {
        if (enabledMeshViews.Remove(this) && enabledMeshViews.Count == 0)
            Canvas.preWillRenderCanvases -= ResolveHealthProjection;
    }

    private bool UsesMeshHealth => meshHealthBar != null && meshHealthBar.IsConfigured;

    private void OnDeath(Enemy _) => visualRoot.gameObject.SetActive(false);
    private void OnStaggerGaugeChanged(float _, float __) => RefreshStagger();
    private void OnAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (attribute == healthAttribute || attribute == maxHealthAttribute)
            RefreshHealth(attribute == healthAttribute && newValue < oldValue);
        if (staggerGauge != null && attribute == staggerGauge.maxGaugeAttribute)
            RefreshStagger();
    }

    private void Start()
    {
        // All AttributeSet/appearance initialization has completed.
        RefreshHealth();
        RefreshStagger();
    }

    private void RefreshStagger()
    {
        if (staggerBar == null) return;

        bool show = staggerGauge != null && staggerGauge.enabled && staggerGauge.HasHealthScaledGauge && sizeProfile != null && sizeProfile.ShowHealthBar;
        if (staggerBar.gameObject.activeSelf != show) staggerBar.gameObject.SetActive(show);
        if (!show || attributes == null || staggerGauge.maxGaugeAttribute == null || staggerGauge.currentGaugeAttribute == null)
            return;

        float maximum = attributes.GetAttributeValue(staggerGauge.maxGaugeAttribute);
        float ratio = maximum > 0f
            ? 1f - Mathf.Clamp01(attributes.GetAttributeValue(staggerGauge.currentGaugeAttribute) / maximum)
            : 0f;
        GameplayEffect groggyEffect = staggerGauge.staggeredEffect;
        if (effectRunner != null && groggyEffect != null && groggyEffect.duration > 0f)
        {
            float remaining = effectRunner.GetRemainingTime(groggyEffect, sizeProfile.gameObject);
            if (remaining > 0.001f)
                ratio = 1f - Mathf.Clamp01(remaining / groggyEffect.duration);
        }
        SetBarRatio(staggerFill, ratio);
    }

    private void RefreshHealth(bool damaged = false)
    {
        if (attributes == null || healthFill == null) return;
        float maximum = attributes.GetAttributeValue(maxHealthAttribute);
        float ratio = maximum > 0f
            ? Mathf.Clamp01(attributes.GetAttributeValue(healthAttribute) / maximum) : 0f;
        ApplyHealthRatio(ratio, damaged, Time.time);
    }

    private void ApplyHealthRatio(float ratio, bool damaged, float now)
    {
        ratio = Mathf.Clamp01(ratio);
        if (!healthInitialized || (!damaged && !Mathf.Approximately(ratio, healthRatio)))
        {
            // Initial bind, healing and maximum-HP rescaling do not leave damage feedback behind.
            trailRatio = trailStartRatio = ratio;
        }
        else if (damaged && ratio < healthRatio)
        {
            UpdateHealthTrail(now);
            trailStartRatio = trailRatio = Mathf.Max(trailRatio, healthRatio);
            lastDamageTime = now;
        }
        healthInitialized = true;
        healthRatio = ratio;
        SetBarRatio(healthFill, healthRatio);
        SetBarRatio(damageTrail, trailRatio);
    }

    private void UpdateHealthTrail(float now)
    {
        if (!healthInitialized) return;
        if (trailRatio > healthRatio)
        {
            float elapsed = now - lastDamageTime - damageTrailDelay;
            if (elapsed >= 0f)
                trailRatio = Mathf.Lerp(trailStartRatio, healthRatio,
                    Mathf.Clamp01(elapsed / Mathf.Max(0.01f, damageTrailDuration)));
        }
        SetBarRatio(damageTrail, trailRatio);
    }

    private static void SetBarRatio(Image bar, float ratio)
    {
        if (bar == null) return;
        Vector2 anchorMax = new Vector2(ratio, 1f);
        if (bar.rectTransform.anchorMax != anchorMax) bar.rectTransform.anchorMax = anchorMax;
        bool visible = ratio > 0f;
        if (bar.gameObject.activeSelf != visible) bar.gameObject.SetActive(visible);
    }

    private void LateUpdate()
    {
        presentationVisible = anyActiveStatus = false;
        if (sizeProfile == null || sizeProfile.HudAnchor == null || visualRoot == null) return;
        bool visible = sizeProfile.isActiveAndEnabled && (enemy == null || (enemy.isActiveAndEnabled && !enemy.IsDead));
        visualRoot.gameObject.SetActive(visible);
        if (!visible)
        {
            meshHealthBar?.Hide();
            return;
        }
        presentationVisible = true;
        UpdateHealthTrail(Time.time);

        // The anchor follows the body. The HUD itself remains upright at a fixed world scale.
        transform.SetPositionAndRotation(sizeProfile.HudAnchor.position, Quaternion.identity);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(SafeScale(parentScale.x), SafeScale(parentScale.y), SafeScale(parentScale.z));
        float width = sizeProfile.HealthBarWidth;
        bool showHealthBar = sizeProfile.ShowHealthBar;
        if (!healthBar.rect.width.Equals(width))
            healthBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        if (healthBar.gameObject.activeSelf != showHealthBar)
            healthBar.gameObject.SetActive(showHealthBar);
        bool showStagger = staggerGauge != null && staggerGauge.enabled && staggerGauge.HasHealthScaledGauge && showHealthBar;
        if (staggerBar != null)
        {
            if (staggerBar.gameObject.activeSelf != showStagger) staggerBar.gameObject.SetActive(showStagger);
            if (showStagger)
            {
                if (!staggerBar.rect.width.Equals(width))
                    staggerBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
                RefreshStagger();
            }
        }
        Vector2 rowPosition = new Vector2(-width * 0.5f, showStagger ? -24f : showHealthBar ? -15f : 0f);
        if (!statusRow.anchoredPosition.Equals(rowPosition)) statusRow.anchoredPosition = rowPosition;
        float x = 0f;
        bool anyStatus = false;
        if (slots != null) foreach (var slot in slots)
        {
            IMonsterStatusSource source = null;
            bool active = statuses != null && statuses.TryGetActive(slot.statusId, out source);
            slot.Bind(source);
            slot.root.gameObject.SetActive(active);
            if (!active) continue;
            anyStatus = true;
            Vector2 slotPosition = new Vector2(x, 0f);
            if (slot.root.anchoredPosition != slotPosition) slot.root.anchoredPosition = slotPosition;
            slot.valueText.color = source.DisplayColor;
            slot.valueText.text = source.ValueKind == MonsterStatusValueKind.Seconds
                ? slot.displayText.Decimal(Mathf.Ceil(source.DisplayValue * 10f) / 10f, "s", CultureInfo.InvariantCulture)
                : slot.displayText.Integer(Mathf.RoundToInt(source.DisplayValue), CultureInfo.InvariantCulture);
            slot.icon.localScale = Time.time < slot.pulseUntil ? Vector3.one * 1.2f : Vector3.one;
            x += slot.width + statusGap;
        }
        anyActiveStatus = anyStatus;
    }

    private static void ResolveHealthProjection()
    {
        bool overlap = false;
        for (int i = 0; i < enabledMeshViews.Count; i++)
        {
            MonsterStackStatusWorldView view = enabledMeshViews[i];
            view.hasProjectionBounds = view.isActiveAndEnabled && view.UsesMeshHealth &&
                view.presentationVisible && view.visualRoot.gameObject.activeInHierarchy && view.TryGetProjectionBounds(out view.projectionBounds);
        }
        for (int i = 0; i < enabledMeshViews.Count && !overlap; i++)
        {
            if (!enabledMeshViews[i].hasProjectionBounds) continue;
            for (int j = i + 1; j < enabledMeshViews.Count; j++)
            {
                if (!enabledMeshViews[j].hasProjectionBounds) continue;
                // Z is intentionally ignored: a false positive only restores the authored sorting path.
                if (enabledMeshViews[i].projectionBounds.Overlaps(enabledMeshViews[j].projectionBounds))
                { overlap = true; break; }
            }
        }
        for (int i = 0; i < enabledMeshViews.Count; i++)
        {
            MonsterStackStatusWorldView view = enabledMeshViews[i];
            if (!view.UsesMeshHealth) continue;
            bool visible = view.presentationVisible && view.visualRoot != null && view.visualRoot.gameObject.activeInHierarchy;
            bool showHealth = visible && view.healthBar != null && view.healthBar.gameObject.activeInHierarchy;
            view.meshHealthBar.SetLegacyProjection(overlap && showHealth);
            if (visible)
            {
                Canvas canvas = view.statusCanvas;
                view.meshHealthBar.Project(view.healthBar.rect.width, view.healthBar.rect.height, view.healthRatio, view.trailRatio,
                    canvas != null ? canvas.sortingLayerID : SortingLayer.NameToID("UI"),
                    canvas != null ? canvas.sortingOrder : 10, showHealth);
            }
            else view.meshHealthBar.Hide();
            bool showStagger = view.staggerBar != null && view.staggerBar.gameObject.activeInHierarchy;
            bool canvasEnabled = visible && (view.anyActiveStatus || showStagger || (overlap && showHealth));
            if (view.statusCanvas != null && view.statusCanvas.enabled != canvasEnabled) view.statusCanvas.enabled = canvasEnabled;
        }
    }

    private bool TryGetProjectionBounds(out Rect bounds)
    {
        bounds = default;
        bool hasBounds = false;
        if (healthBar != null && healthBar.gameObject.activeInHierarchy)
            IncludeRect(healthBar, healthBar.rect, ref bounds, ref hasBounds);
        if (staggerBar != null && staggerBar.gameObject.activeInHierarchy)
            IncludeRect(staggerBar, staggerBar.rect, ref bounds, ref hasBounds);
        if (slots != null) foreach (StatusSlot slot in slots)
        {
            if (slot.root == null || !slot.root.gameObject.activeInHierarchy) continue;
            if (slot.icon is RectTransform icon && icon.gameObject.activeInHierarchy)
                IncludeRect(icon, icon.rect, ref bounds, ref hasBounds);
            TMP_Text text = slot.valueText;
            if (text == null || !text.enabled || !text.gameObject.activeInHierarchy) continue;
            Rect textRect = text.rectTransform.rect;
            // Counts/timers can overflow their authored rectangle. Enclose preferred layout and SDF padding
            // in every direction, avoiding any dependency on the previous frame's generated text mesh.
            float padding = Mathf.Abs(text.fontSize) + (text.extraPadding ? 4f : 0f);
            Material material = text.fontSharedMaterial;
            // Classic TMP SDF padding is capped by its atlas gradient scale. Read that upper bound
            // directly: ShaderUtilities.GetPadding also allocates shaderKeywords and mutates ratios.
            if (material != null && material.HasProperty(gradientScaleId)) padding += Mathf.Abs(material.GetFloat(gradientScaleId)) + 1.25f;
            else if (material != null && material.HasProperty(bitmapPaddingId)) padding += Mathf.Abs(material.GetFloat(bitmapPaddingId)) + 1f;
            Vector4 margin = text.margin;
            float horizontal = Mathf.Max(0f, text.preferredWidth - textRect.width) + padding + Mathf.Abs(margin.x) + Mathf.Abs(margin.z);
            float vertical = Mathf.Max(0f, text.preferredHeight - textRect.height) + padding + Mathf.Abs(margin.y) + Mathf.Abs(margin.w);
            textRect.xMin -= horizontal; textRect.xMax += horizontal;
            textRect.yMin -= vertical; textRect.yMax += vertical;
            IncludeRect(text.rectTransform, textRect, ref bounds, ref hasBounds);
        }
        return hasBounds;
    }

    private static void IncludeRect(RectTransform transform, Rect rect, ref Rect bounds, ref bool hasBounds)
    {
        worldCorners[0] = transform.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0f));
        worldCorners[1] = transform.TransformPoint(new Vector3(rect.xMin, rect.yMax, 0f));
        worldCorners[2] = transform.TransformPoint(new Vector3(rect.xMax, rect.yMax, 0f));
        worldCorners[3] = transform.TransformPoint(new Vector3(rect.xMax, rect.yMin, 0f));
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 corner = worldCorners[i];
            if (!hasBounds)
            {
                bounds = Rect.MinMaxRect(corner.x, corner.y, corner.x, corner.y);
                hasBounds = true;
            }
            else bounds = Rect.MinMaxRect(Mathf.Min(bounds.xMin, corner.x), Mathf.Min(bounds.yMin, corner.y),
                Mathf.Max(bounds.xMax, corner.x), Mathf.Max(bounds.yMax, corner.y));
        }
    }

    private float SafeScale(float parentScale) => Mathf.Abs(parentScale) > 0.0001f ? worldScale / parentScale : worldScale;
}
