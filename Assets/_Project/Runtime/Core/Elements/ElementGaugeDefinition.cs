using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Combat/Element Gauge Definition")]
public sealed class ElementGaugeDefinition : ScriptableObject
{
    [Header("Identity")]
    public GameplayTag elementTag;

    [Header("Attribute Bindings")]
    [Tooltip("이 속성 게이지의 최대치를 읽어올 Attribute")]
    public AttributeDefinition maxGaugeAttribute;

    [Tooltip("이 속성 게이지의 저항값(0~1 등)을 읽어올 Attribute. 없으면 0으로 간주")]
    public AttributeDefinition resistanceAttribute;

    [Header("Triggered Effect")]
    [Tooltip("게이지가 최대치에 도달했을 때 적용할 GameplayEffect")]
    public GameplayEffect triggerEffect;

    [Header("UI")]
    public Sprite icon;

    [Header("VFX")]
    public GameObject triggerVfxPrefab;
    public GameObject sustainVfxPrefab;

    [Header("Visual")]
    public bool useTint = false;
    public Color tintColor = Color.white;
}