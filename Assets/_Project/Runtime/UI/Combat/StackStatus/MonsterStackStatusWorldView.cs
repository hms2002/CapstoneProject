using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityGAS;

/// <summary>Authored monster HUD: HP projection plus left-aligned stack/time status slots.</summary>
[DisallowMultipleComponent]
public sealed class MonsterStackStatusWorldView : MonoBehaviour
{
    [Serializable]
    private sealed class StatusSlot
    {
        public string statusId;
        public RectTransform root;
        public RectTransform icon;
        public TMP_Text valueText;
        public float width;
        [NonSerialized] public IMonsterStatusSource source;
        [NonSerialized] public float pulseUntil;
        public void Pulse() => pulseUntil = Time.time + 0.09f;
        public void Bind(IMonsterStatusSource next)
        {
            if (ReferenceEquals(source, next)) return;
            if (source != null) source.PulseRequested -= Pulse;
            source = next;
            pulseUntil = 0f;
            if (source != null) source.PulseRequested += Pulse;
        }
    }

    [SerializeField] private RectTransform visualRoot;
    [SerializeField] private RectTransform healthBar;
    [SerializeField] private Image healthFill;
    [SerializeField] private Image damageTrail;
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
    private bool healthInitialized;
    private float healthRatio;
    private float trailRatio;
    private float trailStartRatio;
    private float lastDamageTime;

    private void Awake()
    {
        enemy = GetComponentInParent<Enemy>();
        sizeProfile = GetComponentInParent<MonsterSizeProfile>();
        if (sizeProfile == null) return;
        attributes = sizeProfile.GetComponent<AttributeSet>();
        statuses = sizeProfile.GetComponent<MonsterStatusRuntime>();
    }

    private void OnEnable()
    {
        healthInitialized = false;
        if (attributes != null) attributes.OnAttributeChanged += OnAttributeChanged;
        if (enemy != null) enemy.DeathStarted += OnDeath;
        RefreshHealth();
    }

    private void OnDisable()
    {
        healthInitialized = false;
        if (attributes != null) attributes.OnAttributeChanged -= OnAttributeChanged;
        if (enemy != null) enemy.DeathStarted -= OnDeath;
        if (slots != null) foreach (var slot in slots) slot.Bind(null);
    }

    private void OnDeath(Enemy _) => visualRoot.gameObject.SetActive(false);
    private void OnAttributeChanged(AttributeDefinition attribute, float oldValue, float newValue)
    {
        if (attribute == healthAttribute || attribute == maxHealthAttribute)
            RefreshHealth(attribute == healthAttribute && newValue < oldValue);
    }

    private void Start() => RefreshHealth(); // All AttributeSet/appearance initialization has completed.

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
        bar.rectTransform.anchorMax = new Vector2(ratio, 1f);
        bar.gameObject.SetActive(ratio > 0f);
    }

    private void LateUpdate()
    {
        if (sizeProfile == null || sizeProfile.HudAnchor == null || visualRoot == null) return;
        bool visible = sizeProfile.isActiveAndEnabled && (enemy == null || (enemy.isActiveAndEnabled && !enemy.IsDead));
        visualRoot.gameObject.SetActive(visible);
        if (!visible) return;
        UpdateHealthTrail(Time.time);

        // The anchor follows the body. The HUD itself remains upright at a fixed world scale.
        transform.SetPositionAndRotation(sizeProfile.HudAnchor.position, Quaternion.identity);
        Vector3 parentScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
        transform.localScale = new Vector3(SafeScale(parentScale.x), SafeScale(parentScale.y), SafeScale(parentScale.z));
        float width = sizeProfile.HealthBarWidth;
        healthBar.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        healthBar.gameObject.SetActive(sizeProfile.ShowHealthBar);
        statusRow.anchoredPosition = new Vector2(-width * 0.5f, sizeProfile.ShowHealthBar ? -15f : 0f);
        float x = 0f;
        if (slots == null) return;
        foreach (var slot in slots)
        {
            IMonsterStatusSource source = null;
            bool active = statuses != null && statuses.TryGetActive(slot.statusId, out source);
            slot.Bind(source);
            slot.root.gameObject.SetActive(active);
            if (!active) continue;
            slot.root.anchoredPosition = new Vector2(x, 0f);
            slot.valueText.color = source.DisplayColor;
            slot.valueText.text = source.ValueKind == MonsterStatusValueKind.Seconds
                ? (Mathf.Ceil(source.DisplayValue * 10f) / 10f).ToString("0.0", CultureInfo.InvariantCulture) + "s"
                : Mathf.RoundToInt(source.DisplayValue).ToString(CultureInfo.InvariantCulture);
            slot.icon.localScale = Time.time < slot.pulseUntil ? Vector3.one * 1.2f : Vector3.one;
            x += slot.width + statusGap;
        }
    }

    private float SafeScale(float parentScale) => Mathf.Abs(parentScale) > 0.0001f ? worldScale / parentScale : worldScale;
}
