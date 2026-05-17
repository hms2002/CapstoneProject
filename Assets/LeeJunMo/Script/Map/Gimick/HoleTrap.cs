using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityGAS;

public class HoleTrap : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float trapDamage = 10f;
    [SerializeField] private float trapDuration = 1.0f;

    [Header("GAS References")]
    [SerializeField] private GE_Damage_Spec damageEffect;
    [SerializeField] private GameplayEffect fallingEffect;

    [Header("Ignore Settings")]
    [Tooltip("이 태그가 있으면 함정이 발동하지 않습니다 (예: Action.Dash)")]
    [SerializeField] private GameplayTag ignoreTag;

    private bool isTriggered = false;

    private void Start()
    {
        DOTween.Init();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        CheckAndActivateTrap(collision);
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        CheckAndActivateTrap(collision);
    }

    private void CheckAndActivateTrap(Collider2D collision)
    {
        if (isTriggered) return;

        if (!TryBuildFallContext(collision, out PitFallContext context))
            return;

        StartCoroutine(ApplyTrapRoutine(context));
    }

    private bool TryBuildFallContext(Collider2D collision, out PitFallContext context)
    {
        context = default;

        if (!PitFallTarget.TryCreatePlayer(collision, out PitFallTarget target))
            return false;

        if (ignoreTag != null)
        {
            if (target.AbilitySystem.TagSystem.HasTag(ignoreTag))
                return false;
        }

        Vector3 fallCenter = PitFallPositionResolver.ResolveFallCenter(target.Transform.position, gameObject);
        Vector3 respawnPosition = target.SafetyTracker.GetRespawnPosition();

        context = new PitFallContext(
            target.AbilitySystem,
            target.SafetyTracker,
            target.Transform,
            gameObject,
            damageEffect,
            fallingEffect,
            trapDamage,
            trapDuration,
            fallCenter,
            respawnPosition,
            this);

        return context.IsValid;
    }

    private IEnumerator ApplyTrapRoutine(PitFallContext context)
    {
        isTriggered = true;

        yield return PitFallExecutor.Execute(context);

        isTriggered = false;
    }
}
