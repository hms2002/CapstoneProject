using UnityGAS;

public static class AttributeDefinitionExtensions
{
    public static bool AllowsModifier(this AttributeDefinition def)
    {
        return def != null && def.MutationPolicy == AttributeMutationPolicy.BaseAndModifier;
    }

    public static bool IsBaseOnly(this AttributeDefinition def)
    {
        return def != null && def.MutationPolicy == AttributeMutationPolicy.BaseOnly;
    }
}