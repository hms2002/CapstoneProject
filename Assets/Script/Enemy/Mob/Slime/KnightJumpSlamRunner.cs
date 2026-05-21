using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Knight 점프 내려찍기 패턴의 실행 흐름, 경고 표시, 공중 충돌 모드, 높이 연출을 조율한다.
/// - 패턴 취소/정리 시 남은 모션과 프레젠테이션 상태를 원복한다.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Knight))]
public class KnightJumpSlamRunner : MonoBehaviour, IMobPatternRunner, IMobPresentationCleanup
{
    [SerializeField] private Knight owner;
    [SerializeField] private MobAbilityCoordinator abilityCoordinator;
    [SerializeField] private AttackTelegraphService telegraphService;
    [Header("Landing VFX")]
    [SerializeField] private GameObject landingEffectPrefab;
    [SerializeField] private Vector3 landingEffectOffset;
    [SerializeField, Min(0.01f)] private float landingEffectScale = 1f;

    private AbilityMotionController2D motionController;
    private CombatHeightState2D heightState;
    private EntityCollisionProfile2D collisionProfile;
    private AttackTelegraphStyle impactStyle;
    private AttackTelegraphView impactWarning;
    private Knight.JumpSlamContext currentContext;
    private bool isRunning;
    private bool cancelRequested;

    public bool IsRunning => isRunning;

    private void Awake()
    {
        if (owner == null)
            owner = GetComponent<Knight>();

        if (abilityCoordinator == null)
            abilityCoordinator = GetComponent<MobAbilityCoordinator>();

        if (telegraphService == null)
            telegraphService = GetComponent<AttackTelegraphService>();

        motionController = GetComponent<AbilityMotionController2D>();
        heightState = GetComponent<CombatHeightState2D>();
        if (heightState == null)
            heightState = gameObject.AddComponent<CombatHeightState2D>();
        collisionProfile = GetComponent<EntityCollisionProfile2D>();

        impactStyle = MakeImpactStyle();
    }

    private void OnDestroy()
    {
        if (impactStyle != null)
            Destroy(impactStyle);
    }

    private void OnDisable()
    {
        HideWarning();
        motionController?.CancelMotion();
        heightState?.SetGrounded();
        collisionProfile?.RestoreDefaultMode();
    }

    /// <summary>나이트의 점프 내려치기 패턴을 실행합니다.</summary>
    public IEnumerator Run(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (owner == null) yield break;
        if (!owner.TryBuildJumpContext(system, spec, initialTarget, out currentContext)) yield break;
        if (abilityCoordinator != null && !abilityCoordinator.TryBeginRunner(this)) yield break;

        isRunning = true;
        cancelRequested = false;

        try
        {
            ShowWarning(currentContext);
            collisionProfile?.SetBodyCollisionMode(EntityCollisionProfile2D.BodyCollisionMode.PassThroughActors);
            owner.PlayJumpAnimation();
            StartJump(currentContext);

            yield return MoveJump(currentContext, spec);

            if (cancelRequested || owner.IsDead) yield break;

            PlayLandingEffect(currentContext);
            owner.ApplyImpactDamage(currentContext);
        }
        finally
        {
            HideWarning();
            motionController?.CancelMotion();
            heightState?.SetGrounded();
            collisionProfile?.RestoreDefaultMode();
            currentContext = default;
            cancelRequested = false;
            isRunning = false;
            abilityCoordinator?.EndRunner(this);
        }
    }

    /// <summary>점프 내려치기 실행을 취소합니다.</summary>
    public void Cancel()
    {
        cancelRequested = true;
        HideWarning();
        motionController?.CancelMotion();
        heightState?.SetGrounded();
        collisionProfile?.RestoreDefaultMode();
    }

    /// <summary>남아 있는 점프 내려치기 경고를 정리합니다.</summary>
    public void CleanupPresentation()
    {
        HideWarning();
    }

    /// <summary>착지 위치에 원형 경고를 표시합니다.</summary>
    private void ShowWarning(Knight.JumpSlamContext context)
    {
        if (telegraphService == null) return;

        AttackTelegraphSpec spec = AttackTelegraphSpec.CreateCircle(
            context.ImpactPos,
            context.ImpactDiameter,
            context.TravelSeconds,
            impactStyle);

        impactWarning = telegraphService.SpawnDetachedView(spec);
    }

    /// <summary>현재 착지 경고를 숨깁니다.</summary>
    private void HideWarning()
    {
        if (impactWarning == null) return;

        impactWarning.HideImmediate();
        Destroy(impactWarning.gameObject);
        impactWarning = null;
    }

    /// <summary>목표 위치를 향한 점프 이동을 시작합니다.</summary>
    private void StartJump(Knight.JumpSlamContext context)
    {
        Vector2 delta = context.ImpactPos - context.StartPos;
        if (motionController == null || delta.sqrMagnitude <= 0.0001f)
        {
            transform.position = context.ImpactPos;
            return;
        }

        motionController.StartLunge(
            context.StartPos,
            delta.normalized,
            delta.magnitude,
            context.HorizontalTravelSeconds,
            context.TravelEaseOutPower);
    }

    /// <summary>점프 높이를 갱신하면서 착지 시간까지 기다립니다.</summary>
    private IEnumerator MoveJump(Knight.JumpSlamContext context, AbilitySpec spec)
    {
        float duration = Mathf.Max(0.01f, context.TravelSeconds);
        float elapsed = 0f;
        bool hasStartedSlam = false;
        float slamTriggerTime = Mathf.Max(0f, duration - Mathf.Max(0.01f, context.LandingDropSeconds));

        heightState?.SetAirborne(0f, context.AirborneBodyHeight);

        while (elapsed < duration)
        {
            if (cancelRequested || owner.IsDead || IsCancelled(spec)) yield break;

            if (!hasStartedSlam && elapsed >= slamTriggerTime)
            {
                hasStartedSlam = true;
                owner.PlaySlamAnimation();
            }

            float normalized = Mathf.Clamp01(elapsed / duration);
            float height = owner.GetJumpHeight(normalized);
            height *= owner.GetDropScale(elapsed, duration);
            heightState?.SetAirborne(context.AirborneVisualHeight * height, context.AirborneBodyHeight);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!hasStartedSlam)
            owner.PlaySlamAnimation();

        heightState?.SetAirborne(0f, context.AirborneBodyHeight);
    }

    /// <summary>착지 지점에 일회성 먼지 이펙트를 생성한다.</summary>
    private void PlayLandingEffect(Knight.JumpSlamContext context)
    {
        if (landingEffectPrefab == null)
            return;

        Vector3 spawnPosition = new Vector3(context.ImpactPos.x, context.ImpactPos.y, transform.position.z) + landingEffectOffset;
        GameObject effect = Instantiate(landingEffectPrefab, spawnPosition, Quaternion.identity);
        effect.transform.localScale *= landingEffectScale;

        ParticleSystem[] particles = effect.GetComponentsInChildren<ParticleSystem>(true);
        for (int i = 0; i < particles.Length; i++)
            particles[i].Play(true);
    }

    /// <summary>어빌리티 취소 여부를 확인합니다.</summary>
    private static bool IsCancelled(AbilitySpec spec)
    {
        return spec != null && spec.Token != null && spec.Token.IsCancelled;
    }

    /// <summary>나이트 착지 경고 스타일을 만듭니다.</summary>
    private AttackTelegraphStyle MakeImpactStyle()
    {
        AttackTelegraphStyle style = ScriptableObject.CreateInstance<AttackTelegraphStyle>();
        style.fillColorStart = new Color(1f, 0f, 0f, 0.16f);
        style.fillColorEnd = new Color(1f, 0f, 0f, 0.34f);
        style.borderColorStart = new Color(1f, 0.25f, 0.25f, 0.95f);
        style.borderColorEnd = new Color(1f, 0.25f, 0.25f, 0.95f);
        style.progressCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        style.blinkStartNormalized = 0.72f;
        style.blinkFrequency = 5f;
        style.blinkAlphaMin = 0.45f;
        style.scaleFillWithProgress = false;
        style.fillScaleStart = 1f;
        style.fillScaleEnd = 1f;
        return style;
    }
}
