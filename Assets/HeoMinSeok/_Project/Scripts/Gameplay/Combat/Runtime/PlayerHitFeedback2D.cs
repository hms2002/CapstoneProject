using System.Collections;
using UnityEngine;
using CapstoneAudio;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 플레이어 피격 연출을 "피격 진입 -> 피격 중 -> 종료" 단계로 관리한다.
    /// - 무적, 공격 차단, 스킬 차단, 애니메이션, 카메라 쉐이크, 플래시를 제어한다.
    /// - 이동은 막지 않고, 현재 행동은 피격 진입 시 우선 취소한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PlayerHitFeedback2D : MonoBehaviour, IHitFeedbackReceiver2D
    {
        private const string DefaultDeadTagResourcePath = "Tags/State.Dead";
        private static GameplayTag s_defaultDeadTag;

        [Header("Hit Timing")]
        [SerializeField] private float hitEnterSeconds = 0.30f;
        [SerializeField] private float hitActiveSeconds = 0.40f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hitEnterTrigger = "HitEnter";
        [SerializeField] private string hitLoopBool = "IsHit";

        [Tooltip("이동 시작 시 피격 중 루프 연출을 해제한다.")]
        [SerializeField] private bool clearHitLoopWhenMoving = true;

        [Header("Flash")]
        [SerializeField] private SpriteHitFlashController hitFlashController;

        [Header("Camera Shake")]
        [SerializeField] private CameraShakeHook hitCameraShake = CameraShakeHook.Create(
            amplitude: 0.10f,
            amplitudeMultiplier: 10f,
            maxAmplitude: 0f,
            minIntervalSeconds: 0.02f);
        [HideInInspector, SerializeField] private float defaultShake = 0.10f;
        [HideInInspector, SerializeField, Min(0f)] private float cameraShakeForceMultiplier = 10f;
        [HideInInspector, SerializeField, Min(0f)] private float cameraShakeMinIntervalSeconds = 0.02f;
        [HideInInspector, SerializeField] private bool legacyCameraShakeMigrated;

        [Header("Audio")]
        [SerializeField] private SoundRef playerHitSound;

        [Header("Movement")]
        [Tooltip("선택: 이동 여부를 읽어 피격 루프를 중간 해제한다.")]
        [SerializeField] private MonoBehaviour movementStateProviderSource;

        [Header("Tag Policy")]
        [Tooltip("피격 전체 구간 동안 부여할 무적 태그")]
        [SerializeField] private GameplayTag invulnerableTag;

        [Tooltip("피격 진입 구간 동안만 부여할 공격 차단 태그")]
        [SerializeField] private GameplayTag attackingBlockedTag;

        [Tooltip("피격 진입 구간 동안만 부여할 스킬 차단 태그")]
        [SerializeField] private GameplayTag skillBlockedTag;

        [Tooltip("선택: 피격 상태 자체를 설명하는 태그")]
        [SerializeField] private GameplayTag hitReactStateTag;

        [Header("Skill Exception")]
        [Tooltip("이 태그가 현재 붙어 있으면 기본적으로 피격 연출 진입을 생략한다. 보통 State.Skill")]
        [SerializeField] private GameplayTag activeSkillTag;

        [Tooltip("스킬 중이어도 피격 연출을 허용하는 예외 태그. 필요 없으면 비워 둔다.")]
        [SerializeField] private GameplayTag allowHitReactDuringSkillTag;

        [Header("Immunity Tags (Optional)")]
        [Tooltip("이 태그가 있으면 피격 연출 전체를 무시한다.")]
        [SerializeField] private GameplayTag hitReactImmuneTag;

        [Tooltip("이 태그가 있으면 현재 행동 취소를 하지 않는다.")]
        [SerializeField] private GameplayTag cancelImmuneTag;

        private TagSystem _tags;
        private AbilitySystem _abilitySystem;
        private IMovementStateProvider _movementStateProvider;

        private int _hitEnterTriggerHash;
        private int _hitLoopBoolHash;

        private Coroutine _reactionRoutine;

        /// <summary>
        /// 책임 :
        /// - 플레이어가 피격되었을 때 공격측 impact sound 대신 우선 재생할 전용 피격 사운드를 제공한다.
        /// - 전투 오디오 라우터가 플레이어 타깃 여부를 판별할 때 참조하는 데이터 소켓 역할을 한다.
        /// </summary>
        public SoundRef PlayerHitSound => playerHitSound;

        private void Awake()
        {
            MigrateLegacyCameraShakeIfNeeded();
            _tags = GetComponent<TagSystem>();
            _abilitySystem = GetComponent<AbilitySystem>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (hitFlashController == null)
                hitFlashController = GetComponentInChildren<SpriteHitFlashController>();

            if (movementStateProviderSource != null)
                _movementStateProvider = movementStateProviderSource as IMovementStateProvider;
            else
                _movementStateProvider = GetComponent<IMovementStateProvider>();

            _hitEnterTriggerHash = string.IsNullOrWhiteSpace(hitEnterTrigger) ? 0 : Animator.StringToHash(hitEnterTrigger);
            _hitLoopBoolHash = string.IsNullOrWhiteSpace(hitLoopBool) ? 0 : Animator.StringToHash(hitLoopBool);
        }

        private void OnValidate()
        {
            MigrateLegacyCameraShakeIfNeeded();
        }

        /// <summary>
        /// 책임 :
        /// - 데미지 적용 후 전달된 피격 피드백을 받아 예외 규칙을 검사하고,
        ///   피격 상태 머신을 시작한다.
        /// </summary>
        public void OnHitFeedback(HitFeedbackPayload payload)
        {
            if (ShouldIgnoreHitReaction())
                return;

            if (ShouldIgnoreBecauseOfSkillState())
                return;

            if (_reactionRoutine != null)
            {
                StopCoroutine(_reactionRoutine);
                _reactionRoutine = null;
            }

            ClearReactionTags();
            SetHitLoop(false);

            _reactionRoutine = StartCoroutine(CoHitReaction(payload));
        }

        /// <summary>
        /// 책임 :
        /// - 외부에서 강제로 피격 상태를 끊어야 할 때 연출/태그를 정리한다.
        /// </summary>
        public void ForceEndReaction()
        {
            if (_reactionRoutine != null)
            {
                StopCoroutine(_reactionRoutine);
                _reactionRoutine = null;
            }

            if (hitFlashController != null)
                hitFlashController.StopFlash();

            SetHitLoop(false);
            ClearReactionTags();
        }

        /// <summary>
        /// 책임 :
        /// - 피격 진입 -> 피격 중 -> 종료 흐름을 수행한다.
        /// </summary>
        private IEnumerator CoHitReaction(HitFeedbackPayload payload)
        {
            float shake = payload.CameraShake > 0f ? payload.CameraShake : defaultShake;

            // 1) 피격 진입
            AddReactionTags(
                addInvulnerable: true,
                addAttackBlock: true,
                addSkillBlock: true,
                addHitState: true);

            CancelCurrentActionIfAllowed();

            PlayHitEnterAnimation();

            if (hitFlashController != null)
                hitFlashController.PlayFlash();

            if (shake > 0f)
            {
                hitCameraShake.TryPlayOverrideAmplitude(
                    shake,
                    gameObject,
                    ResolveShakeDirection(payload.Causer),
                    nameof(PlayerHitFeedback2D));
            }

            yield return new WaitForSeconds(hitEnterSeconds);

            // 2) 피격 중
            RemoveTagSafe(attackingBlockedTag);
            RemoveTagSafe(skillBlockedTag);
            SetHitLoop(true);

            float elapsed = 0f;
            while (elapsed < hitActiveSeconds)
            {
                elapsed += Time.deltaTime;

                if (clearHitLoopWhenMoving &&
                    _movementStateProvider != null &&
                    _movementStateProvider.IsMoving)
                {
                    SetHitLoop(false);
                }

                yield return null;
            }

            // 3) 종료
            SetHitLoop(false);
            ClearReactionTags();
            _reactionRoutine = null;
        }

        /// <summary>
        /// 책임 :
        /// - 현재 스킬 사용 중이면 피격 연출을 생략할지 판단한다.
        /// - 일부 스킬은 allowHitReactDuringSkillTag로 예외 허용할 수 있다.
        /// </summary>
        private bool ShouldIgnoreBecauseOfSkillState()
        {
            if (_tags == null || activeSkillTag == null)
                return false;

            if (!_tags.HasTag(activeSkillTag))
                return false;

            bool allowDuringSkill =
                allowHitReactDuringSkillTag != null &&
                _tags.HasTag(allowHitReactDuringSkillTag);

            return !allowDuringSkill;
        }

        /// <summary>
        /// 책임 :
        /// - 피격 연출 전체를 무시해야 하는 면역 상태인지 검사한다.
        /// </summary>
        private bool ShouldIgnoreHitReaction()
        {
            if (IsDeadState())
                return true;

            return hitReactImmuneTag != null &&
                   _tags != null &&
                   _tags.HasTag(hitReactImmuneTag);
        }

        /// <summary>
        /// 책임 :
        /// - 플레이어가 이미 사망 상태면 추가 피격 연출을 시작하지 않도록 차단한다.
        /// - 사망 이후 남은 후속 타격이 들어와도 피격 애니메이션/플래시가 다시 시작되지 않게 막는다.
        /// </summary>
        private bool IsDeadState()
        {
            if (_tags == null)
                return false;

            if (s_defaultDeadTag == null)
                s_defaultDeadTag = Resources.Load<GameplayTag>(DefaultDeadTagResourcePath);

            return s_defaultDeadTag != null && _tags.HasTag(s_defaultDeadTag);
        }

        /// <summary>
        /// 책임 :
        /// - 현재 캐스팅/실행 중인 Ability를 강제 취소해
        ///   "행동 중 맞으면 피격 우선" 규칙을 반영한다.
        /// </summary>
        private void CancelCurrentActionIfAllowed()
        {
            if (_abilitySystem == null)
                return;

            bool cancelImmune =
                cancelImmuneTag != null &&
                _tags != null &&
                _tags.HasTag(cancelImmuneTag);

            if (cancelImmune)
                return;

            _abilitySystem.CancelCasting(force: true);
            _abilitySystem.CancelExecution(force: true);
        }

        /// <summary>
        /// 책임 :
        /// - 피격 진입 애니메이션을 재생한다.
        /// </summary>
        private void PlayHitEnterAnimation()
        {
            if (animator == null)
                return;

            if (_hitLoopBoolHash != 0)
                animator.SetBool(_hitLoopBoolHash, false);

            if (_hitEnterTriggerHash != 0)
                animator.SetTrigger(_hitEnterTriggerHash);
        }

        /// <summary>
        /// 책임 :
        /// - 피격 중 루프 애니메이션 Bool을 제어한다.
        /// </summary>
        private void SetHitLoop(bool value)
        {
            if (animator == null || _hitLoopBoolHash == 0)
                return;

            animator.SetBool(_hitLoopBoolHash, value);
        }

        /// <summary>
        /// 책임 :
        /// - 피격 진입 시 필요한 태그를 현재 규칙에 맞게 부여한다.
        /// </summary>
        private void AddReactionTags(
            bool addInvulnerable,
            bool addAttackBlock,
            bool addSkillBlock,
            bool addHitState)
        {
            if (addInvulnerable) AddTagSafe(invulnerableTag);
            if (addAttackBlock) AddTagSafe(attackingBlockedTag);
            if (addSkillBlock) AddTagSafe(skillBlockedTag);
            if (addHitState) AddTagSafe(hitReactStateTag);
        }

        /// <summary>
        /// 책임 :
        /// - 피격 종료 시 남아 있을 수 있는 태그를 모두 정리한다.
        /// </summary>
        private void ClearReactionTags()
        {
            RemoveTagSafe(invulnerableTag);
            RemoveTagSafe(attackingBlockedTag);
            RemoveTagSafe(skillBlockedTag);
            RemoveTagSafe(hitReactStateTag);
        }

        /// <summary>
        /// 책임 :
        /// - TagSystem이 있을 때만 안전하게 태그를 추가한다.
        /// </summary>
        private void AddTagSafe(GameplayTag tag)
        {
            if (tag == null || _tags == null)
                return;

            _tags.AddTag(tag);
        }

        /// <summary>
        /// 책임 :
        /// - TagSystem이 있을 때만 안전하게 태그를 제거한다.
        /// </summary>
        private void RemoveTagSafe(GameplayTag tag)
        {
            if (tag == null || _tags == null)
                return;

            _tags.RemoveTag(tag);
        }

        /// <summary>
        /// 책임 :
        /// - 피격 가해자 기준으로 카메라 흔들림 방향 벡터를 계산한다.
        /// - 가해자가 없으면 위쪽 기본 방향으로 impulse를 보낸다.
        /// </summary>
        private void MigrateLegacyCameraShakeIfNeeded()
        {
            if (legacyCameraShakeMigrated)
                return;

            hitCameraShake = CameraShakeHook.Create(
                amplitude: defaultShake,
                amplitudeMultiplier: cameraShakeForceMultiplier,
                maxAmplitude: 0f,
                minIntervalSeconds: cameraShakeMinIntervalSeconds);
            legacyCameraShakeMigrated = true;
        }

        private Vector3 ResolveShakeDirection(GameObject causer)
        {
            if (causer != null)
            {
                Vector3 delta = transform.position - causer.transform.position;
                delta.z = 0f;

                if (delta.sqrMagnitude > 0.0001f)
                    return delta.normalized;
            }

            return Vector3.up;
        }

        /// <summary>
        /// 책임 :
        /// - 현재 MainCamera에서 Cinemachine impulse source를 찾고, 없으면 카메라에 자동으로 추가한다.
        /// - 피격 연출은 플레이어가 아니라 실제 출력 카메라 기준으로 흔들림을 내보내도록 한다.
        /// </summary>
    }
}
