using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Optional weapon-owned dash hook. Dash remains a global ability; weapons opt in
    /// by exposing this interface only while their runtime state is active.
    /// </summary>
    public interface IWeaponDashAugment
    {
        void ModifyDash(ref float duration, ref float distance);

        void HandleDashStarted(
            AbilitySystem system,
            AbilitySpec spec,
            AbilityDefinition dashAbility,
            Vector2 direction,
            Vector2 startPosition,
            float duration,
            float distance);

        void HandleDashFinished(
            AbilitySystem system,
            AbilitySpec spec,
            AbilityDefinition dashAbility,
            Vector2 direction,
            Vector2 startPosition,
            Vector2 endPosition,
            bool cancelled);
    }
}
