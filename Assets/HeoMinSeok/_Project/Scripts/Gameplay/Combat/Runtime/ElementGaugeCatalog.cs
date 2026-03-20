using UnityEngine;

[CreateAssetMenu(menuName = "Combat/Element Gauge Catalog")]
public sealed class ElementGaugeCatalog : ScriptableObject
{
    public ElementGaugeDefinition[] definitions;
}