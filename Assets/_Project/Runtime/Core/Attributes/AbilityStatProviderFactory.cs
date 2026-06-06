namespace UnityGAS
{
    public static class AbilityStatProviderFactory
    {
        public static IStatProvider Create(AbilitySystem system)
        {
            if (system == null || system.AttributeSet == null)
                return null;

            var bindings = system.DamageProfile != null
                ? system.DamageProfile.GetStatBindings()
                : null;

            if (bindings == null)
                return null;

            return new AttributeStatProvider(system.AttributeSet, bindings);
        }
    }
}