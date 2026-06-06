using System;
using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "AttributeInitProfile", menuName = "GAS/Stats/Attribute Init Profile")]
    public sealed class AttributeInitProfileSO : ScriptableObject
    {
        [Serializable]
        public struct Entry
        {
            public AttributeDefinition attribute;
            public float baseValue;
        }

        [SerializeField] private Entry[] entries = Array.Empty<Entry>();

        public Entry[] Entries => entries;
    }
}