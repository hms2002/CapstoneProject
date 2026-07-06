using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityGAS;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(AttributeSet))]
/// <summary>
/// 책임 : 훈련용 허수아비의 체력 감소를 누적/초당 피해 텍스트로 표시한다.
/// </summary>
public sealed class TrainingDummyDamageReadout2D : MonoBehaviour
{
    private const float DpsWindowSeconds = 1f;

    [Header("Damage Source")]
    [SerializeField] private AttributeDefinition healthAttribute;

    [Header("Display")]
    [SerializeField] private TMP_Text displayText;
    [Min(0f)]
    [SerializeField] private float visibleSecondsAfterHit = 5f;
    [SerializeField] private string displayFormat = "피해 {0}/{1}\nDPS {2}/{3}\n누적 {4}";
    [SerializeField] private string numberFormat = "0.#";

    private readonly List<DamageSample> damageSamples = new List<DamageSample>();
    private AttributeSet attributeSet;
    private int firstSampleIndex;
    private float lastDamage;
    private float maxDamage;
    private float currentDps;
    private float maxDps;
    private float totalDamage;
    private float lastHitTime = float.NegativeInfinity;

    private void Awake()
    {
        attributeSet = GetComponent<AttributeSet>();
        SetDisplayAlpha(0f);
        RefreshText();
    }

    private void OnEnable()
    {
        if (attributeSet != null)
        {
            attributeSet.OnAttributeChanged += OnAttributeChanged;
        }
    }

    private void OnDisable()
    {
        if (attributeSet != null)
        {
            attributeSet.OnAttributeChanged -= OnAttributeChanged;
        }
    }

    private void Update()
    {
        if (displayText == null || float.IsNegativeInfinity(lastHitTime))
        {
            return;
        }

        float now = Time.time;
        PruneDamageSamples(now);
        currentDps = CalculateCurrentDps();
        RefreshText();

        float alpha = now - lastHitTime >= visibleSecondsAfterHit ? 0f : 1f;
        SetDisplayAlpha(alpha);
    }

    private void OnAttributeChanged(AttributeDefinition attr, float oldValue, float newValue)
    {
        if (healthAttribute == null || attr != healthAttribute || newValue >= oldValue)
        {
            return;
        }

        RecordDamage(oldValue - newValue);
    }

    private void RecordDamage(float damage)
    {
        float now = Time.time;

        lastDamage = damage;
        maxDamage = Mathf.Max(maxDamage, damage);
        totalDamage += damage;
        lastHitTime = now;

        damageSamples.Add(new DamageSample(now, damage));
        PruneDamageSamples(now);
        currentDps = CalculateCurrentDps();
        maxDps = Mathf.Max(maxDps, currentDps);

        RefreshText();
        SetDisplayAlpha(1f);
    }

    private void PruneDamageSamples(float now)
    {
        float cutoff = now - DpsWindowSeconds;
        while (firstSampleIndex < damageSamples.Count && damageSamples[firstSampleIndex].Time < cutoff)
        {
            firstSampleIndex++;
        }

        if (firstSampleIndex > 32 && firstSampleIndex * 2 > damageSamples.Count)
        {
            damageSamples.RemoveRange(0, firstSampleIndex);
            firstSampleIndex = 0;
        }
    }

    private float CalculateCurrentDps()
    {
        float sum = 0f;
        for (int i = firstSampleIndex; i < damageSamples.Count; i++)
        {
            sum += damageSamples[i].Amount;
        }

        return sum;
    }

    private void RefreshText()
    {
        if (displayText == null)
        {
            return;
        }

        displayText.text = string.Format(
            CultureInfo.InvariantCulture,
            displayFormat,
            FormatNumber(lastDamage),
            FormatNumber(maxDamage),
            FormatNumber(currentDps),
            FormatNumber(maxDps),
            FormatNumber(totalDamage));
    }

    private string FormatNumber(float value)
    {
        return value.ToString(numberFormat, CultureInfo.InvariantCulture);
    }

    private void SetDisplayAlpha(float alpha)
    {
        if (displayText == null)
        {
            return;
        }

        Color color = displayText.color;
        color.a = alpha;
        displayText.color = color;
    }

    private readonly struct DamageSample
    {
        public DamageSample(float time, float amount)
        {
            Time = time;
            Amount = amount;
        }

        public float Time { get; }
        public float Amount { get; }
    }
}
