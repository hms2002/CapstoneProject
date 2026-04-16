using CapstonePresentation;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(Animator))]
public class ShadowFog : MonoBehaviour
{
    private enum FogPhase
    {
        Starting,
        Idle,
        Ending
    }

    private const string PlayerTag = "Player";
    private const int BaseLayerIndex = 0;

    [Header("Timing")]
    [SerializeField] [Min(0f)] private float idleDuration = 3f;
    [SerializeField] [Min(0.01f)] private float fogDebuffDuration = 3f;
    [SerializeField] [Min(0.01f)] private float endFallbackDestroyDelay = 0.35f;

    [Header("Animator")]
    [SerializeField] private string startStateName = "FogStartAnim";
    [SerializeField] private string idleStateName = "FogIdleAnim";
    [SerializeField] private string endStateName = "FogEndAnim";
    [SerializeField] private string endTriggerName = "FogEndAnim";

    private Animator animator;
    private Collider2D triggerZone;
    private FogPhase phase = FogPhase.Starting;
    private float idleEndTime;
    private float endFallbackDestroyTime = -1f;
    private bool hasEndTrigger;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        triggerZone = GetComponent<Collider2D>();
        if (triggerZone != null)
            triggerZone.isTrigger = true;

        hasEndTrigger = HasAnimatorTrigger(endTriggerName);
    }

    private void Update()
    {
        switch (phase)
        {
            case FogPhase.Starting:
                TryEnterIdlePhase();
                break;

            case FogPhase.Idle:
                if (Time.time >= idleEndTime)
                    BeginEndPhase();
                break;

            case FogPhase.Ending:
                TryFinishEndPhase();
                break;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleTouch(other);
    }

    private void HandleTouch(Collider2D other)
    {
        if (phase != FogPhase.Idle || other == null)
            return;

        GameObject targetObject = UnityGAS.CombatTargetResolver2D.ResolveDamageTarget(other);
        if (targetObject == null || !targetObject.CompareTag(PlayerTag))
            return;

        FogSightLock sightLock = targetObject.GetComponent<FogSightLock>();
        if (sightLock == null)
            sightLock = targetObject.AddComponent<FogSightLock>();

        sightLock.ApplyFog(fogDebuffDuration);
    }

    private void TryEnterIdlePhase()
    {
        if (animator == null)
        {
            EnterIdlePhase();
            return;
        }

        if (IsAnimatorInState(idleStateName) || (!IsAnimatorInState(startStateName) && !animator.IsInTransition(BaseLayerIndex)))
            EnterIdlePhase();
    }

    private void EnterIdlePhase()
    {
        phase = FogPhase.Idle;
        idleEndTime = Time.time + idleDuration;
    }

    private void BeginEndPhase()
    {
        phase = FogPhase.Ending;

        if (triggerZone != null)
            triggerZone.enabled = false;

        if (animator != null)
        {
            if (hasEndTrigger)
                animator.SetTrigger(endTriggerName);
            else if (!string.IsNullOrWhiteSpace(endStateName))
                animator.Play(endStateName, BaseLayerIndex, 0f);
        }

        endFallbackDestroyTime = Time.time + endFallbackDestroyDelay;
    }

    private void TryFinishEndPhase()
    {
        if (animator == null || string.IsNullOrWhiteSpace(endStateName))
        {
            if (Time.time >= endFallbackDestroyTime)
                PresentationSpawnService.Release(gameObject);
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        if (stateInfo.IsName(endStateName) &&
            !animator.IsInTransition(BaseLayerIndex) &&
            stateInfo.normalizedTime >= 1f)
        {
            PresentationSpawnService.Release(gameObject);
            return;
        }

        if (Time.time >= endFallbackDestroyTime && !stateInfo.IsName(endStateName))
            PresentationSpawnService.Release(gameObject);
    }

    private bool HasAnimatorTrigger(string triggerName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(triggerName))
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.type == AnimatorControllerParameterType.Trigger &&
                parameter.name == triggerName)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsAnimatorInState(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        return stateInfo.IsName(stateName) && !animator.IsInTransition(BaseLayerIndex);
    }
}
