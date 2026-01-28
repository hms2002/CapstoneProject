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
        [Header("Charges (Optional)")]
        public bool useCharges = false;
        public int maxCharges = 1; // useCharges=true면 2 이상 권장

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
        [Header("Tag Sets (Optional)")]
        public List<GameplayTagSet> requiredTagSets = new();
        public List<GameplayTagSet> blockedByTagSets = new();
        public List<GameplayTagSet> targetRequiredTagSets = new();
        public List<GameplayTagSet> targetBlockedByTagSets = new();
        // 태그 셋 변경 여부를 체크하는 내부 캐시 변수
        private int _reqSetVerHash, _blockSetVerHash, _tReqSetVerHash, _tBlockSetVerHash;

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
            int newReqHash = GameplayTagSet.ComputeVersionHash(requiredTagSets);
            int newBlkHash = GameplayTagSet.ComputeVersionHash(blockedByTagSets);
            int newTReqHash = GameplayTagSet.ComputeVersionHash(targetRequiredTagSets);
            int newTBlkHash = GameplayTagSet.ComputeVersionHash(targetBlockedByTagSets);

            if (_tagMasksCompiled &&
                newReqHash == _reqSetVerHash &&
                newBlkHash == _blockSetVerHash &&
                newTReqHash == _tReqSetVerHash &&
                newTBlkHash == _tBlockSetVerHash)
                return;

            TagRegistry.EnsureInitialized();

            _reqMask = new TagMask(TagRegistry.WordCount);
            _blockMask = new TagMask(TagRegistry.WordCount);
            _tReqMask = new TagMask(TagRegistry.WordCount);
            _tBlockMask = new TagMask(TagRegistry.WordCount);

            // direct tags
            if (requiredTags != null) for (int i = 0; i < requiredTags.Count; i++) if (requiredTags[i] != null) _reqMask.Add(requiredTags[i]);
            if (blockedByTags != null) for (int i = 0; i < blockedByTags.Count; i++) if (blockedByTags[i] != null) _blockMask.Add(blockedByTags[i]);
            if (targetRequiredTags != null) for (int i = 0; i < targetRequiredTags.Count; i++) if (targetRequiredTags[i] != null) _tReqMask.Add(targetRequiredTags[i]);
            if (targetBlockedByTags != null) for (int i = 0; i < targetBlockedByTags.Count; i++) if (targetBlockedByTags[i] != null) _tBlockMask.Add(targetBlockedByTags[i]);

            // tag sets
            var visited = new HashSet<GameplayTagSet>();
            if (requiredTagSets != null) for (int i = 0; i < requiredTagSets.Count; i++) requiredTagSets[i]?.AddToMask(_reqMask, visited);
            visited.Clear();
            if (blockedByTagSets != null) for (int i = 0; i < blockedByTagSets.Count; i++) blockedByTagSets[i]?.AddToMask(_blockMask, visited);
            visited.Clear();
            if (targetRequiredTagSets != null) for (int i = 0; i < targetRequiredTagSets.Count; i++) targetRequiredTagSets[i]?.AddToMask(_tReqMask, visited);
            visited.Clear();
            if (targetBlockedByTagSets != null) for (int i = 0; i < targetBlockedByTagSets.Count; i++) targetBlockedByTagSets[i]?.AddToMask(_tBlockMask, visited);

            _reqSetVerHash = newReqHash;
            _blockSetVerHash = newBlkHash;
            _tReqSetVerHash = newTReqHash;
            _tBlockSetVerHash = newTBlkHash;

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
