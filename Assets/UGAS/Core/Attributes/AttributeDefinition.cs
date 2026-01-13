using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "NewAttribute", menuName = "GAS/Attribute Definition")]
    public class AttributeDefinition : ScriptableObject
    {
        [Header("Info")]
        public string attributeName = "Health";
        [TextArea] public string description = "Attribute description.";

        [Header("Values")]
        public float defaultBaseValue = 100f;
        public float minValue = 0f;
        public float maxValue = 100f;

        [Header("Regeneration")]
        public bool hasRegeneration = false;
        public float regenerationRate = 5f; // Per second
        public float regenerationDelay = 2f; // After taking damage

        [Header("Display")]
        public Color displayColor = Color.red;
        public Sprite icon;

        private void OnValidate()
        {
            if (minValue > maxValue) minValue = maxValue;
            if (defaultBaseValue > maxValue) defaultBaseValue = maxValue;
            if (defaultBaseValue < minValue) defaultBaseValue = minValue;
        }
    }
}