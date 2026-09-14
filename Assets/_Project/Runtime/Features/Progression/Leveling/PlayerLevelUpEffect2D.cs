using UnityEngine;

/// <summary>Plays authored player VFX on an actual level gain, independently of HUD visibility.</summary>
[DisallowMultipleComponent]
public sealed class PlayerLevelUpEffect2D : MonoBehaviour
{
    [SerializeField] private ParticleSystem levelUpEffectPrefab;
    [SerializeField] private SpriteRenderer playerRenderer;

    private void OnEnable()
    {
        RunLevelProgression.ExperienceGranted += HandleExperienceGranted;
    }

    private void OnDisable()
    {
        RunLevelProgression.ExperienceGranted -= HandleExperienceGranted;
    }

    private void HandleExperienceGranted(LevelProgressionGrantResult result)
    {
        if (result.LevelsGained <= 0 || PlayerRuntimeRegistry.GetPlayerTransform() != transform)
            return;

        // One pulse per grant, even when one pickup grants several levels.
        Vector3 localCenter = playerRenderer != null
            ? transform.InverseTransformPoint(playerRenderer.bounds.center) : Vector3.zero;
        PlayerHealParticlePlayback.PlayAttached(levelUpEffectPrefab, transform, localCenter);
    }
}
