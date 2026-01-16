namespace UnityGAS
{
    public interface IAbilityCleanup
    {
        void OnAbilityFinished(AbilitySystem system, AbilitySpec spec, UnityEngine.GameObject target);
    }
}