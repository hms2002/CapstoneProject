using System.Collections;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 일반 몬스터 피격 연출을 관리한다.
    /// - 무적 없이 재피격 가능하며, 재피격 시 연출을 처음부터 다시 시작한다.
    /// - 필요 시 행동 차단 태그를 잠시 부여해 몬스터 행동을 멈춘다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MonsterHitFeedback2D : MonoBehaviour, IHitFeedbackReceiver2D
    {
        private const string DefaultDeadTagResourcePath = "Tags/State.Dead";
        private static GameplayTag s_defaultDeadTag;

        [Header("Timing")]
        [SerializeField] private float hitEnterSeconds = 0.20f;
        [SerializeField] private float hitActiveSeconds = 0.40f;

        [Header("Animation")]
        [SerializeField] private Animator animator;
        [SerializeField] private string hitEnterTrigger = "HitEnter";
        [SerializeField] private string hitLoopBool = "IsHit";

        [Header("Flash")]
        [SerializeField] private SpriteHitFlashController hitFlashController;

        [Header("Optional Tags")]
        [Tooltip("선택: 피격 중 상태 자체를 설명하는 태그")]
        [SerializeField] private GameplayTag hitReactStateTag;

        [Tooltip("선택: 피격 중 공격 차단 태그")]
        [SerializeField] private GameplayTag attackingBlockedTag;

        [Tooltip("선택: 피격 중 스킬 차단 태그")]
        [SerializeField] private GameplayTag skillBlockedTag;

        [Header("Optional")]
        [Tooltip("이 태그가 있으면 피격 연출을 무시한다. 보스/특수 몬스터 예외용")]
        [SerializeField] private GameplayTag hitReactImmuneTag;

        private TagSystem _tags;
        private Coroutine _reactionRoutine;

        private int _hitEnterTriggerHash;
        private int _hitLoopBoolHash;

        private void Awake()
        {
            _tags = GetComponent<TagSystem>();

            if (animator == null)
                animator = GetComponentInChildren<Animator>();

            if (hitFlashController == null)
                hitFlashController = GetComponentInChildren<SpriteHitFlashController>();

            _hitEnterTriggerHash = string.IsNullOrWhiteSpace(hitEnterTrigger) ? 0 : Animator.StringToHash(hitEnterTrigger);
            _hitLoopBoolHash = string.IsNullOrWhiteSpace(hitLoopBool) ? 0 : Animator.StringToHash(hitLoopBool);
        }

        /// <summary>
        /// 책임 :
        /// - 몬스터가 피해를 받았을 때 피격 연출을 시작한다.
        /// - 무적 프레임이 없으므로 재피격 시 기존 연출을 끊고 처음부터 다시 시작한다.
        /// </summary>
        public void OnHitFeedback(HitFeedbackPayload payload)
        {
            if (ShouldIgnoreHitReaction())
                return;

            if (_reactionRoutine != null)
            {
                StopCoroutine(_reactionRoutine);
                _reactionRoutine = null;
            }

            ClearReactionTags();
            SetHitLoop(false);

            _reactionRoutine = StartCoroutine(CoHitReaction());
        }

        /// <summary>
        /// 책임 :
        /// - 일반 몬스터 피격 연출 상태를 시간 순서대로 진행한다.
        /// </summary>
        private IEnumerator CoHitReaction()
        {
            // 1) 피격 진입
            AddTagSafe(hitReactStateTag);
            AddTagSafe(attackingBlockedTag);
            AddTagSafe(skillBlockedTag);

            PlayHitEnterAnimation();

            if (hitFlashController != null)
                hitFlashController.PlayFlash();

            yield return new WaitForSeconds(hitEnterSeconds);

            // 2) 피격 중
            SetHitLoop(true);
            yield return new WaitForSeconds(hitActiveSeconds);

            // 3) 종료
            SetHitLoop(false);
            ClearReactionTags();
            _reactionRoutine = null;
        }

        /// <summary>
        /// 책임 :
        /// - 피격 연출 면역 상태인지 검사한다.
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
        /// - 몬스터가 이미 사망 상태면 추가 피격 연출을 시작하지 않도록 차단한다.
        /// - 사망 이후 남은 후속 타격으로 피격 애니메이션이 다시 켜지지 않게 방어한다.
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
        /// - 피격 종료 시 사용한 태그를 정리한다.
        /// </summary>
        private void ClearReactionTags()
        {
            RemoveTagSafe(hitReactStateTag);
            RemoveTagSafe(attackingBlockedTag);
            RemoveTagSafe(skillBlockedTag);
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
    }
}
