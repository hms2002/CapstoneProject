using UnityEngine;
using System.Collections.Generic;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "NewAbility", menuName = "GAS/Ability Definition")]
    public class AbilityDefinition : ScriptableObject
    {
        [Header("Info")]
        public string abilityName = "New Ability";
        public Sprite icon;
        [TextArea] public string description = "Ability description.";

        [Header("Activation")]
        public float cooldown = 0f;
        [Tooltip("설정 시 쿨다운을 GE(Duration)로 관리합니다. (추천: GE_Cooldown + grantedTags에 Cooldown.* 태그 부여)")]
        public GameplayEffect cooldownEffect;
        public float castTime = 0f;
        public float recoveryTime = 0f;

        public bool canCastWhileMoving = true;
        public bool interruptible = true;

        [Tooltip("true면 Activate 시 target이 반드시 필요합니다. (타겟 선택/범위 체크 등은 Logic에서 처리)")]
        public bool requireTargetObject = false;

        [Header("Cost")]
        public float cost = 0f;
        public AttributeDefinition costAttribute;

        public enum AnimationChannel { Player, Weapon }
        [Header("Animation")]
        [Header("Animation Routing")]
        public AnimationChannel animationChannel = AnimationChannel.Player;
        public string animationTrigger;
        [HideInInspector] public int animationTriggerHash;

        [Header("Tags")]
        public List<GameplayTag> abilityTags = new List<GameplayTag>();
        public List<GameplayTag> requiredTags = new List<GameplayTag>();
        public List<GameplayTag> blockedByTags = new List<GameplayTag>();

        [Tooltip("Tags granted to the caster while this ability is executing (logic + recovery). Managed by AbilitySystem.")]
        public List<GameplayTag> grantedTagsWhileActive = new List<GameplayTag>();

        [Header("Target Tags (optional)")]
        public List<GameplayTag> targetRequiredTags = new List<GameplayTag>();
        public List<GameplayTag> targetBlockedByTags = new List<GameplayTag>();

        [Header("Logic (UE GameplayAbility-like)")]
        public AbilityLogic logic;
        // Logic에서 사용할 데이터(확장성을 고려하여 Logic이 사용할 데이터 분리)
        [Header("Typed Data (Optional)")]
        public UnityEngine.Object sourceObject;

        [Header("Effect Containers (optional)")]
        public List<AbilityEffectContainer> containers = new List<AbilityEffectContainer>();

        // -------------------------
        // GameplayCue (Cosmetic)
        // -------------------------
        [Header("GameplayCue (Optional)")]
        [Tooltip("캐스팅 시작(입력 접수 후 castTime 시작)")]
        public GameplayTag cueOnCastStart;

        [Tooltip("캐스팅 중 지속(Add/Remove)")]
        public GameplayTag cueWhileCasting;

        [Tooltip("Commit(코스트 지불 + 실행 시작 시점)")]
        public GameplayTag cueOnCommit;

        [Tooltip("능력 실행 중 지속(Add/Remove)")]
        public GameplayTag cueWhileActive;

        [Tooltip("정상 종료 1회 실행")]
        public GameplayTag cueOnEnd;

        [Tooltip("캐스팅 취소 1회 실행")]
        public GameplayTag cueOnCastCancelled;

        [Tooltip("실행 중 취소(Interrupt/CancelExecution) 1회 실행")]
        public GameplayTag cueOnExecutionCancelled;
        // ...

        public bool IsInstant => castTime <= 0f;
        public bool HasCost => cost > 0f && costAttribute != null;

        private TagMask _reqMask, _blockMask, _tReqMask, _tBlockMask;
        private bool _tagMasksCompiled;

        private void EnsureTagMasks()
        {
            if (_tagMasksCompiled) return;
            _reqMask = TagMask.Compile(requiredTags);
            _blockMask = TagMask.Compile(blockedByTags);
            _tReqMask = TagMask.Compile(targetRequiredTags);
            _tBlockMask = TagMask.Compile(targetBlockedByTags);
            _tagMasksCompiled = true;
        }

        private void OnValidate()
        {
            if (!string.IsNullOrEmpty(animationTrigger))
                animationTriggerHash = Animator.StringToHash(animationTrigger);
            else
                animationTriggerHash = 0;

            _tagMasksCompiled = false;
        }

        public bool CanActivate(GameObject caster, GameObject target)
        {
            var attributeSet = caster.GetComponent<AttributeSet>();
            if (attributeSet == null) return false;

            if (HasCost && attributeSet.GetAttributeValue(costAttribute) < cost) return false;
            if (requireTargetObject && target == null) return false;

            EnsureTagMasks();

            var tags = caster.GetComponent<TagSystem>();
            if (tags != null)
            {
                if (tags.HasAny(_blockMask)) return false;
                if (!tags.HasAll(_reqMask)) return false;
            }

            // 타겟이 명시된 경우에만 Tag 조건 검사 (거리/가시선/레이어 필터는 Logic에서)
            if (target != null && !IsValidTargetTags(target))
                return false;

            return true;
        }

        /// <summary>
        /// Tag-only validation for targets.
        /// </summary>
        public bool IsValidTargetTags(GameObject target)
        {
            if (target == null) return false;

            EnsureTagMasks();

            var targetTags = target.GetComponent<TagSystem>();
            if (targetTags == null)
                return targetRequiredTags.Count == 0;

            if (targetTags.HasAny(_tBlockMask)) return false;
            if (!targetTags.HasAll(_tReqMask)) return false;

            return true;
        }

        public void ApplyCost(GameObject caster)
        {
            if (!HasCost) return;
            var attributeSet = caster.GetComponent<AttributeSet>();
            if (attributeSet != null)
                attributeSet.ModifyAttributeValue(costAttribute, -cost, this);
        }
    }
}
