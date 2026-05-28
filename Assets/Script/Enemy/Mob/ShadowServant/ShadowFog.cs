using CapstonePresentation;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D), typeof(Animator))]
public class ShadowFog : MonoBehaviour
{
    // 이 클래스의 책임:
    // - 그림자 안개 구역의 수명과 애니메이션 단계를 관리한다.
    // - 플레이어 접촉 시 공통 버프/디버프 적용기를 통해 시야 제한 GE를 적용하고, 시야 연출도 함께 갱신한다.

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

    [Header("Debuff")]
    [SerializeField] private CombatBuffDebuffApplicationDefinition restrictedVisionDebuffDefinition;
    [SerializeField] private bool logFogSightStatusFlow = true;

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
    private string sourceKey;

    private void Awake()
    {
        animator = GetComponent<Animator>();
        triggerZone = GetComponent<Collider2D>();
        if (triggerZone != null)
            triggerZone.isTrigger = true;

        hasEndTrigger = HasAnimatorTrigger(endTriggerName);
        sourceKey = $"enemy.shadowFog.{GetInstanceID()}";
    }

    private void OnEnable()
    {
        ResetForSpawn();
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
        PlayerInteractor2D player = targetObject != null
            ? targetObject.GetComponent<PlayerInteractor2D>()
            : null;

        if (logFogSightStatusFlow)
        {
            Debug.Log(
                $"[ShadowFog] HandleTouch. other={other.name}, targetObject={(targetObject != null ? targetObject.name : "null")}, " +
                $"playerRoot={(player != null ? player.name : "null")}, definition={(restrictedVisionDebuffDefinition != null && restrictedVisionDebuffDefinition.StatusHudDefinition != null ? restrictedVisionDebuffDefinition.StatusHudDefinition.StatusId : "null")}",
                this);
        }

        if (player == null)
        {
            if (logFogSightStatusFlow)
                Debug.LogWarning("[ShadowFog] Skipped fog application because no PlayerInteractor2D root was found.", this);
            return;
        }

        GameObject playerRoot = player.gameObject;
        if (!playerRoot.CompareTag(PlayerTag))
        {
            if (logFogSightStatusFlow)
                Debug.LogWarning($"[ShadowFog] Skipped fog application because player root tag was '{playerRoot.tag}', expected '{PlayerTag}'.", this);
            return;
        }

        RestrictedVisionVisualController sightLock = playerRoot.GetComponent<RestrictedVisionVisualController>();
        if (sightLock == null)
        {
            sightLock = playerRoot.AddComponent<RestrictedVisionVisualController>();
            if (logFogSightStatusFlow)
                Debug.Log($"[ShadowFog] Added RestrictedVisionVisualController to player root '{playerRoot.name}'.", this);
        }

        sightLock.ApplyFog(fogDebuffDuration);

        if (restrictedVisionDebuffDefinition == null)
        {
            if (logFogSightStatusFlow)
            {
                Debug.LogWarning(
                    "[ShadowFog] Skipped debuff apply because restrictedVisionDebuffDefinition was not assigned.",
                    this);
            }
            return;
        }

        CombatBuffDebuffApplier debuffApplier = CombatBuffDebuffApplier.GetOrAdd(playerRoot);
        if (debuffApplier == null)
        {
            if (logFogSightStatusFlow)
                Debug.LogWarning("[ShadowFog] Skipped debuff apply because CombatBuffDebuffApplier was missing.", this);
            return;
        }

        bool applied = debuffApplier.ApplyFromSource(gameObject, playerRoot, restrictedVisionDebuffDefinition, sourceKey, fogDebuffDuration);

        if (logFogSightStatusFlow)
        {
            Debug.Log(
                $"[ShadowFog] Applied fog debuff to '{playerRoot.name}' for {fogDebuffDuration:0.00}s. " +
                $"sourceKey={sourceKey}, applyResult={applied}",
                this);
        }
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
                ReleaseToPool();
            return;
        }

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(BaseLayerIndex);
        if (stateInfo.IsName(endStateName) &&
            !animator.IsInTransition(BaseLayerIndex) &&
            stateInfo.normalizedTime >= 1f)
        {
            ReleaseToPool();
            return;
        }

        if (Time.time >= endFallbackDestroyTime && !stateInfo.IsName(endStateName))
            ReleaseToPool();
    }

    /// <summary>
    /// 책임 : 풀에서 재사용된 안개가 이전 수명 종료 상태를 물고 즉시 사라지지 않도록 런타임 상태를 초기화한다.
    /// </summary>
    private void ResetForSpawn()
    {
        phase = FogPhase.Starting;
        idleEndTime = 0f;
        endFallbackDestroyTime = -1f;

        if (triggerZone != null)
            triggerZone.enabled = true;

        if (animator == null)
            return;

        animator.ResetTrigger(endTriggerName);
        if (!string.IsNullOrWhiteSpace(startStateName))
            animator.Play(startStateName, BaseLayerIndex, 0f);

        animator.Update(0f);
    }

    /// <summary>
    /// 책임 : 안개 수명이 끝난 오브젝트를 공통 프레젠테이션 풀로 반환한다.
    /// </summary>
    private void ReleaseToPool()
    {
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
