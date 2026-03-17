using System;
using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "AttributeCatalog", menuName = "GAS/Stats/Attribute Catalog")]
    public sealed class AttributeCatalogSO : ScriptableObject
    {
        [SerializeField] private AttributeDefinition[] attributes = Array.Empty<AttributeDefinition>();

        public AttributeDefinition[] Attributes => attributes;
    }
}