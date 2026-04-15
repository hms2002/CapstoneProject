using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "NewAbility", menuName = "GAS/Ability Definition")]
    public class AbilityDefinition : ScriptableObject
    {
        /// <summary>
        /// 책임 :
        /// - Ability가 어떤 실행 레인에서 동작할지 정의한다.
        /// - ExclusiveQueued는 기존 큐/버퍼 단독 실행,
        ///   ParallelIndependent는 큐에 막히지 않고 독립 실행된다.
        /// </summary>
        public enum ExecutionPolicy
        {
            ExclusiveQueued,
            ParallelIndependent
        }

        [Header("Info")]
        public string abilityName = "New Ability";
        public Sprite icon;
        [TextArea] public string description = "Ability description.";

        [Header("Activation")]
        public float cooldown = 0f;

        [Header("Execution Policy")]
        [Tooltip("1차 병행 구현은 ParallelIndependent + Instant Ability만 안전하게 지원한다.")]
        public ExecutionPolicy executionPolicy = ExecutionPolicy.ExclusiveQueued;

        [Header("Charges (Optional)")]
        public bool useCharges = false;
        public int maxCharges = 1;

        [Tooltip("설정 시 쿨다운을 GE(Duration)로 관리합니다. (추천: GE_Cooldown + grantedTags에 Cooldown.* 태그 부여)")]
        public GameplayEffect cooldownEffect;

        [Header("Cooldown (GAS-style)")]
        [Tooltip("Optional. If set, this tag is granted to the caster while the cooldown is active. Exact match.")]
        public GameplayTag cooldownTag;

        [Tooltip("true면 cooldown 시작 시점이 commit이 아니라 종료 시점(정상 종료/취소 포함)이다.")]
        public bool startCooldownOnEnd = false;

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

        // ------------------------------------------------------------------
        // Tags
        // ------------------------------------------------------------------
        // 이 섹션은 전부 "같은 종류의 태그"가 아니다.
        //
        // 읽는 순서 추천:
        // 1) abilityTags
        //    - 이 Ability 자체를 설명하는 분류 태그
        //    - 예: "검 스킬", "이동기", "근접 공격" 같은 정체성
        //
        // 2) requiredTags / blockedByTags
        //    - caster가 "발동 직전" 가지고 있어야 / 가지면 안 되는 태그
        //    - 즉, 시작 가능 여부를 판단하는 조건
        //
        // 3) targetRequiredTags / targetBlockedByTags
        //    - target이 유효한 대상인지 판단하는 조건
        //
        // 4) grantedTagsWhileActive
        //    - 이 Ability가 실행 중인 동안 caster에게 붙여 줄 상태 태그
        //    - 예: State.Skill, State.Attacking, State.Move.Dash
        //
        // 5) cancelCastingOnTags / cancelExecutionOnTags
        //    - 실행 도중 어떤 상태가 생기면 취소되는지 정의
        //
        // 6) cooldownTag
        //    - 이 Ability의 쿨타임이 돌고 있음을 나타내는 표식
        //
        // 핵심 구분:
        // - abilityTags            : "이 Ability는 누구인가?"
        // - required/blocked       : "언제 시작 가능한가?"
        // - grantedWhileActive     : "실행 중 caster가 어떤 상태가 되는가?"
        // - cancelOn...            : "도중에 어떤 상태가 오면 끊기는가?"
        // ------------------------------------------------------------------

        [Header("Tags")]

        /// <summary>
        /// 책임 :
        /// - 이 Ability 자체의 정체성/분류를 설명한다.
        /// - 보통 "검 계열", "이동기", "근접 공격"처럼 Ability를 라벨링할 때 사용한다.
        ///
        /// 주의 :
        /// - 이 목록은 기본적으로 "발동 가능 여부"를 직접 막는 용도가 아니다.
        /// - 즉, 보통은 "스킬 설명용 분류 태그"로 생각하는 편이 맞다.
        ///
        /// 예 :
        /// - abilityTags = [Weapon.Sword, Ability.Melee, Ability.Dash]
        /// </summary>
        public List<GameplayTag> abilityTags = new List<GameplayTag>();

        /// <summary>
        /// 책임 :
        /// - caster가 이 Ability를 발동하기 전에 반드시 가지고 있어야 하는 태그를 정의한다.
        /// - "발동 전 필수 조건"이다.
        ///
        /// 예 :
        /// - 검 장착 중일 때만 가능
        /// - 특정 자세/조준 상태일 때만 가능
        ///
        /// 해석 :
        /// - requiredTags = "이 Ability를 쓰기 전에 caster에게 꼭 있어야 하는 것"
        ///
        /// 예 :
        /// - State.Equip.Weapon.Sword
        /// </summary>
        public List<GameplayTag> requiredTags = new List<GameplayTag>();

        /// <summary>
        /// 책임 :
        /// - caster가 이 태그 중 하나라도 가지고 있으면 Ability 발동을 막는다.
        /// - "발동 전 금지 조건"이다.
        ///
        /// 예 :
        /// - State.Skill.Blocked
        /// - State.Attacking.Blocked
        /// - State.Status.Groggy
        ///
        /// 해석 :
        /// - blockedByTags = "이게 붙어 있으면 시작 자체를 못 한다"
        ///
        /// 주의 :
        /// - 실행 도중 취소 조건과는 다르다.
        /// - 이 목록은 "시작 전에 검사"한다.
        /// </summary>
        public List<GameplayTag> blockedByTags = new List<GameplayTag>();

        [Header("Cancellation (Optional)")]

        /// <summary>
        /// 책임 :
        /// - CASTING 단계(castTime 진행 중) 동안 caster에게 이 태그가 새로 붙으면 캐스팅을 취소한다.
        ///
        /// 해석 :
        /// - 시전 바 읽는 중 끊김 조건
        ///
        /// 예 :
        /// - 캐스팅 중 그로기 걸리면 취소
        /// - 캐스팅 중 강한 제어 불가 상태가 오면 취소
        ///
        /// 주의 :
        /// - "발동 전 금지"가 아니라 "이미 캐스팅 시작 후 중도 취소"다.
        /// </summary>
        [Tooltip("If any of these tags are added to the caster while CASTING, the cast will be cancelled.")]
        public List<GameplayTag> cancelCastingOnTags = new();

        /// <summary>
        /// 책임 :
        /// - EXECUTING 단계(커밋 후 실제 로직 실행 + 회복 구간 포함) 동안
        ///   caster에게 이 태그가 새로 붙으면 실행을 취소한다.
        ///
        /// 해석 :
        /// - 이미 스킬이 나가고 있는 중에 끊김 조건
        ///
        /// 예 :
        /// - 실행 중 그로기 진입
        /// - 실행 중 특정 강제 중단 상태 진입
        ///
        /// 주의 :
        /// - 보통 순간 Event 태그보다는 지속되는 State 태그가 더 잘 맞는다.
        /// </summary>
        [Tooltip("If any of these tags are added to the caster while EXECUTING, the execution will be cancelled.")]
        public List<GameplayTag> cancelExecutionOnTags = new();

        [Header("Tag Sets (Optional)")]

        /// <summary>
        /// 책임 :
        /// - requiredTags의 태그셋 버전이다.
        /// - 여러 필수 조건 태그를 의미 있는 묶음으로 재사용하고 싶을 때 사용한다.
        ///
        /// 예 :
        /// - 특정 무기군 공통 요구조건 묶음
        /// - 특정 stance 공통 요구조건 묶음
        ///
        /// 권장 :
        /// - 원자 태그는 작게 유지하고,
        ///   반복되는 설정은 TagSet으로 묶어 재사용한다.
        /// </summary>
        public List<GameplayTagSet> requiredTagSets = new();

        /// <summary>
        /// 책임 :
        /// - blockedByTags의 태그셋 버전이다.
        /// - 여러 금지 조건 태그를 정책 묶음으로 재사용하고 싶을 때 사용한다.
        ///
        /// 예 :
        /// - "기절 계열 제약"
        /// - "컷신 중 조작 제한"
        /// </summary>
        public List<GameplayTagSet> blockedByTagSets = new();

        /// <summary>
        /// 책임 :
        /// - targetRequiredTags의 태그셋 버전이다.
        /// - 타겟에게 요구하는 조건을 묶음으로 재사용한다.
        /// </summary>
        public List<GameplayTagSet> targetRequiredTagSets = new();

        /// <summary>
        /// 책임 :
        /// - targetBlockedByTags의 태그셋 버전이다.
        /// - 타겟에게 붙어 있으면 안 되는 조건을 묶음으로 재사용한다.
        /// </summary>
        public List<GameplayTagSet> targetBlockedByTagSets = new();

        // 태그 셋 변경 여부를 체크하는 내부 캐시 변수
        private int _reqSetVerHash, _blockSetVerHash, _tReqSetVerHash, _tBlockSetVerHash;

        /// <summary>
        /// 책임 :
        /// - Ability가 활성 상태인 동안 caster에게 자동으로 부여할 상태 태그를 정의한다.
        /// - AbilitySystem이 실행 시작/종료에 맞춰 add/remove를 관리한다.
        ///
        /// 해석 :
        /// - "이 Ability가 돌아가는 동안 caster는 어떤 상태가 되는가?"
        ///
        /// 대표 예 :
        /// - State.Skill
        /// - State.Attacking
        /// - State.Move.Dash
        /// - State.Move.ForceMove
        ///
        /// 주의 :
        /// - abilityTags와 다르다.
        /// - abilityTags는 Ability의 정체성이고,
        ///   grantedTagsWhileActive는 "실행 중 caster 상태"다.
        /// </summary>
        [Tooltip("Tags granted to the caster while this ability is executing (logic + recovery). Managed by AbilitySystem.")]
        public List<GameplayTag> grantedTagsWhileActive = new List<GameplayTag>();

        [Header("Target Tags (optional)")]

        /// <summary>
        /// 책임 :
        /// - target이 이 Ability의 유효한 대상이 되기 위해 반드시 가져야 하는 태그를 정의한다.
        ///
        /// 해석 :
        /// - "대상이 이런 상태여야 이 Ability를 적용할 수 있다"
        ///
        /// 예 :
        /// - 특정 마커가 붙은 대상만 가능
        /// - 특정 속성/상태 대상만 가능
        /// </summary>
        public List<GameplayTag> targetRequiredTags = new List<GameplayTag>();

        /// <summary>
        /// 책임 :
        /// - target이 이 태그 중 하나라도 가지고 있으면 유효 대상에서 제외한다.
        ///
        /// 해석 :
        /// - "대상이 이 상태면 이 Ability의 타겟이 될 수 없다"
        ///
        /// 예 :
        /// - State.Invulnerable
        /// - 특정 면역 상태
        /// </summary>
        public List<GameplayTag> targetBlockedByTags = new List<GameplayTag>();

        [Header("Logic (UE GameplayAbility-like)")]
        public AbilityLogic logic;

        /// <summary>
        /// 책임 :
        /// - AbilityLogic이 사용할 추가 데이터를 정의한다.
        /// - Logic 클래스가 직접 자신 전용 SO/프리팹/설정 객체를 읽을 수 있도록 연결하는 자리다.
        ///
        /// 예 :
        /// - projectile prefab
        /// - typed config SO
        /// - weapon data
        /// </summary>
        [Header("Typed Data (Optional)")]
        public UnityEngine.Object sourceObject;

        [Header("Effect Containers (optional)")]
        public List<AbilityEffectContainer> containers = new List<AbilityEffectContainer>();

        // -------------------------
        // GameplayCue (Cosmetic)
        // -------------------------
        // Cue 태그는 규칙 판정보다 "표현 계층 트리거"에 가깝다.
        // 즉, VFX / SFX / 카메라 / UI 피드백 재생을 위한 키로 보는 편이 맞다.
        //
        // 권장 해석:
        // - cueOnCastStart          : 캐스팅 시작 1회
        // - cueWhileCasting         : 캐스팅 중 유지형 표현
        // - cueOnCommit             : 커밋 순간 1회
        // - cueWhileActive          : 실행 중 유지형 표현
        // - cueOnEnd                : 정상 종료 1회
        // - cueOnCastCancelled      : 캐스팅 취소 1회
        // - cueOnExecutionCancelled : 실행 중 취소 1회
        //
        // 주의:
        // - Cue는 "보여주는 것"을 위한 키다.
        // - 이동 가능 여부 / 데미지 판정 같은 게임 규칙 자체는 Cue로 판단하지 않는다.
        // -------------------------

        [Header("Audio (Optional)")]

        [Tooltip("캐스팅 시작 시 1회 재생되는 사운드")]
        public SoundRef audioOnCastStart;

        [Tooltip("캐스팅 중 유지되는 루프 사운드")]
        public SoundRef audioWhileCasting;

        [Tooltip("Commit 시점에 1회 재생되는 사운드")]
        public SoundRef audioOnCommit;

        [Tooltip("실행 중 유지되는 루프 사운드")]
        public SoundRef audioWhileActive;

        [Tooltip("정상 종료 시 1회 재생되는 사운드")]
        public SoundRef audioOnEnd;

        [Tooltip("캐스팅 취소 시 1회 재생되는 사운드")]
        public SoundRef audioOnCastCancelled;

        [Tooltip("실행 중 취소 시 1회 재생되는 사운드")]
        public SoundRef audioOnExecutionCancelled;

        [Tooltip("실제 타격이 적중했을 때 재생할 기본 타격 사운드")]
        public SoundRef impactSound;

        [Header("Camera Shake (Optional)")]
        public CameraShakeHook cameraShakeOnCastStart;
        public CameraShakeHook cameraShakeWhileCasting;
        public CameraShakeHook cameraShakeOnCommit;
        public CameraShakeHook cameraShakeWhileActive;
        public CameraShakeHook cameraShakeOnEnd;
        public CameraShakeHook cameraShakeOnCastCancelled;
        public CameraShakeHook cameraShakeOnExecutionCancelled;
        public CameraShakeHook cameraShakeOnHitConfirmed;

        [Header("Spawned Presentation (Optional)")]
        public WorldPresentationHook presentationOnCastStart;
        public WorldPresentationHook presentationWhileCasting;
        public WorldPresentationHook presentationOnCommit;
        public WorldPresentationHook presentationWhileActive;
        public WorldPresentationHook presentationOnEnd;
        public WorldPresentationHook presentationOnCastCancelled;
        public WorldPresentationHook presentationOnExecutionCancelled;
        public WorldPresentationHook presentationOnHitConfirmed;

        [Header("GameplayCue (Optional)")]
        [Tooltip("캐스트 시작 시 실행할 cue 목록")]
        public List<GameplayTag> cuesOnCastStart = new List<GameplayTag>();

        [Tooltip("캐스팅 중 유지할 cue 목록")]
        public List<GameplayTag> cuesWhileCasting = new List<GameplayTag>();

        [Tooltip("Commit 시 실행할 cue 목록")]
        public List<GameplayTag> cuesOnCommit = new List<GameplayTag>();

        [Tooltip("실행 중 유지할 cue 목록")]
        public List<GameplayTag> cuesWhileActive = new List<GameplayTag>();

        [Tooltip("정상 종료 시 실행할 cue 목록")]
        public List<GameplayTag> cuesOnEnd = new List<GameplayTag>();

        [Tooltip("캐스팅 취소 시 실행할 cue 목록")]
        public List<GameplayTag> cuesOnCastCancelled = new List<GameplayTag>();

        [Tooltip("실행 중 취소 시 실행할 cue 목록")]
        public List<GameplayTag> cuesOnExecutionCancelled = new List<GameplayTag>();

        [Tooltip("실제 히트 확정 시 실행할 cue 목록")]
        public List<GameplayTag> cuesOnHitConfirmed = new List<GameplayTag>();

        [Min(0f)]
        [Tooltip("히트 cue 전반에 전달할 기본 세기 배수")]
        public float hitCueMagnitude = 1f;

        [Min(0f)]
        [Tooltip("치명타일 때 hit cue 세기에 곱할 배수")]
        public float criticalHitCueMagnitudeMultiplier = 2f;

        /// <summary>
        /// 책임 :
        /// - 입력 접수 후 castTime이 시작되는 시점에 1회성 연출을 요청한다.
        /// </summary>
        [Tooltip("캐스팅 시작(입력 접수 후 castTime 시작)")]
        [HideInInspector]
        public GameplayTag cueOnCastStart;

        /// <summary>
        /// 책임 :
        /// - 캐스팅 중 유지되어야 하는 연출을 식별한다.
        /// - 보통 Add/Remove 형태의 루프형 VFX, 차징 이펙트 등에 사용한다.
        /// </summary>
        [Tooltip("캐스팅 중 지속(Add/Remove)")]
        [HideInInspector]
        public GameplayTag cueWhileCasting;

        /// <summary>
        /// 책임 :
        /// - Commit(코스트 지불 + 실제 실행 시작) 시점의 1회성 연출을 요청한다.
        /// </summary>
        [Tooltip("Commit(코스트 지불 + 실행 시작 시점)")]
        [HideInInspector]
        public GameplayTag cueOnCommit;

        /// <summary>
        /// 책임 :
        /// - Ability 실행 중 유지되어야 하는 연출을 식별한다.
        /// - 보통 돌진 잔상, 지속 오라, 활성화 상태 표시에 사용한다.
        /// </summary>
        [Tooltip("능력 실행 중 지속(Add/Remove)")]
        [HideInInspector]
        public GameplayTag cueWhileActive;

        /// <summary>
        /// 책임 :
        /// - Ability가 정상 종료되었을 때 1회성 연출을 요청한다.
        /// </summary>
        [Tooltip("정상 종료 1회 실행")]
        [HideInInspector]
        public GameplayTag cueOnEnd;

        /// <summary>
        /// 책임 :
        /// - 캐스팅 단계에서 취소되었을 때 1회성 연출을 요청한다.
        /// </summary>
        [Tooltip("캐스팅 취소 1회 실행")]
        [HideInInspector]
        public GameplayTag cueOnCastCancelled;

        /// <summary>
        /// 책임 :
        /// - 실행 단계에서 취소되었을 때 1회성 연출을 요청한다.
        /// </summary>
        [Tooltip("실행 중 취소(Interrupt/CancelExecution) 1회 실행")]
        [HideInInspector]
        public GameplayTag cueOnExecutionCancelled;

        [Tooltip("실제 피격 확정(HitConfirm) 시 실행할 1회성 cue")]
        [HideInInspector]
        public GameplayTag cueOnHitConfirmed;
        [HideInInspector]
        public List<GameplayTag> additionalCuesOnHitConfirmed = new List<GameplayTag>();

        public bool IsInstant => castTime <= 0f;
        public bool HasCost => cost > 0f && costAttribute != null;

        private TagMask _reqMask, _blockMask, _tReqMask, _tBlockMask;
        private bool _tagMasksCompiled;

        /// <summary>
        /// 책임 :
        /// - direct tag + tag set을 하나의 TagMask로 컴파일한다.
        /// - CanActivate / IsValidTargetTags에서 빠르게 검사할 수 있도록 캐시를 구성한다.
        ///
        /// 포함 대상 :
        /// - requiredTags + requiredTagSets
        /// - blockedByTags + blockedByTagSets
        /// - targetRequiredTags + targetRequiredTagSets
        /// - targetBlockedByTags + targetBlockedByTagSets
        /// </summary>
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

            // ------------------------------------------------------------------
            // direct tags
            // 책임 :
            // - Inspector에 직접 넣은 낱개 태그를 마스크에 추가한다.
            // - exact match 기준으로 컴파일된다.
            // ------------------------------------------------------------------
            if (requiredTags != null)
                for (int i = 0; i < requiredTags.Count; i++)
                    if (requiredTags[i] != null)
                        _reqMask.AddExact(requiredTags[i]);

            if (blockedByTags != null)
                for (int i = 0; i < blockedByTags.Count; i++)
                    if (blockedByTags[i] != null)
                        _blockMask.AddExact(blockedByTags[i]);

            if (targetRequiredTags != null)
                for (int i = 0; i < targetRequiredTags.Count; i++)
                    if (targetRequiredTags[i] != null)
                        _tReqMask.AddExact(targetRequiredTags[i]);

            if (targetBlockedByTags != null)
                for (int i = 0; i < targetBlockedByTags.Count; i++)
                    if (targetBlockedByTags[i] != null)
                        _tBlockMask.AddExact(targetBlockedByTags[i]);

            // ------------------------------------------------------------------
            // tag sets
            // 책임 :
            // - TagSet에 묶여 있는 태그들을 마스크에 펼쳐서 추가한다.
            // - 설정 편의용 묶음이 런타임 검사에서는 direct tag와 같은 기준으로 처리되게 한다.
            // ------------------------------------------------------------------
            var visited = new HashSet<GameplayTagSet>();

            if (requiredTagSets != null)
                for (int i = 0; i < requiredTagSets.Count; i++)
                    requiredTagSets[i]?.AddToMask(_reqMask, visited);

            visited.Clear();

            if (blockedByTagSets != null)
                for (int i = 0; i < blockedByTagSets.Count; i++)
                    blockedByTagSets[i]?.AddToMask(_blockMask, visited);

            visited.Clear();

            if (targetRequiredTagSets != null)
                for (int i = 0; i < targetRequiredTagSets.Count; i++)
                    targetRequiredTagSets[i]?.AddToMask(_tReqMask, visited);

            visited.Clear();

            if (targetBlockedByTagSets != null)
                for (int i = 0; i < targetBlockedByTagSets.Count; i++)
                    targetBlockedByTagSets[i]?.AddToMask(_tBlockMask, visited);

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

            MigrateLegacyCueFields();
            _tagMasksCompiled = false;
        }

        public IEnumerable<GameplayTag> EnumerateCuesOnCastStart() => EnumerateCueTags(cuesOnCastStart, cueOnCastStart);
        public IEnumerable<GameplayTag> EnumerateCuesWhileCasting() => EnumerateCueTags(cuesWhileCasting, cueWhileCasting);
        public IEnumerable<GameplayTag> EnumerateCuesOnCommit() => EnumerateCueTags(cuesOnCommit, cueOnCommit);
        public IEnumerable<GameplayTag> EnumerateCuesWhileActive() => EnumerateCueTags(cuesWhileActive, cueWhileActive);
        public IEnumerable<GameplayTag> EnumerateCuesOnEnd() => EnumerateCueTags(cuesOnEnd, cueOnEnd);
        public IEnumerable<GameplayTag> EnumerateCuesOnCastCancelled() => EnumerateCueTags(cuesOnCastCancelled, cueOnCastCancelled);
        public IEnumerable<GameplayTag> EnumerateCuesOnExecutionCancelled() => EnumerateCueTags(cuesOnExecutionCancelled, cueOnExecutionCancelled);
        public IEnumerable<GameplayTag> EnumerateCuesOnHitConfirmed() => EnumerateCueTags(cuesOnHitConfirmed, cueOnHitConfirmed, additionalCuesOnHitConfirmed);

        private void MigrateLegacyCueFields()
        {
            MigrateLegacyCue(cuesOnCastStart, ref cueOnCastStart);
            MigrateLegacyCue(cuesWhileCasting, ref cueWhileCasting);
            MigrateLegacyCue(cuesOnCommit, ref cueOnCommit);
            MigrateLegacyCue(cuesWhileActive, ref cueWhileActive);
            MigrateLegacyCue(cuesOnEnd, ref cueOnEnd);
            MigrateLegacyCue(cuesOnCastCancelled, ref cueOnCastCancelled);
            MigrateLegacyCue(cuesOnExecutionCancelled, ref cueOnExecutionCancelled);
            MigrateLegacyCue(cuesOnHitConfirmed, ref cueOnHitConfirmed);
            MigrateLegacyCueList(cuesOnHitConfirmed, additionalCuesOnHitConfirmed);
        }

        private static void MigrateLegacyCue(List<GameplayTag> destination, ref GameplayTag legacyCue)
        {
            if (legacyCue == null)
                return;

            destination ??= new List<GameplayTag>();
            if (!destination.Contains(legacyCue))
                destination.Add(legacyCue);

            legacyCue = null;
        }

        private static void MigrateLegacyCueList(List<GameplayTag> destination, List<GameplayTag> legacyCues)
        {
            if (legacyCues == null || legacyCues.Count == 0)
                return;

            destination ??= new List<GameplayTag>();
            for (int i = 0; i < legacyCues.Count; i++)
            {
                GameplayTag cue = legacyCues[i];
                if (cue != null && !destination.Contains(cue))
                    destination.Add(cue);
            }

            legacyCues.Clear();
        }

        private static IEnumerable<GameplayTag> EnumerateCueTags(
            List<GameplayTag> cueList,
            GameplayTag legacyCue,
            List<GameplayTag> legacyExtra = null)
        {
            HashSet<GameplayTag> yielded = null;

            if (legacyCue != null)
            {
                yielded = new HashSet<GameplayTag> { legacyCue };
                yield return legacyCue;
            }

            if (legacyExtra != null)
            {
                for (int i = 0; i < legacyExtra.Count; i++)
                {
                    GameplayTag cue = legacyExtra[i];
                    if (cue == null)
                        continue;

                    yielded ??= new HashSet<GameplayTag>();
                    if (yielded.Add(cue))
                        yield return cue;
                }
            }

            if (cueList == null)
                yield break;

            for (int i = 0; i < cueList.Count; i++)
            {
                GameplayTag cue = cueList[i];
                if (cue == null)
                    continue;

                yielded ??= new HashSet<GameplayTag>();
                if (yielded.Add(cue))
                    yield return cue;
            }
        }

        /// <summary>
        /// 책임 :
        /// - caster / target 기준으로 Ability 발동 가능 여부를 판정한다.
        ///
        /// 검사 순서 :
        /// 1) caster의 AttributeSet 존재 여부
        /// 2) cost 충족 여부
        /// 3) target 필수 여부
        /// 4) caster의 required / blocked 태그 조건
        /// 5) target의 required / blocked 태그 조건
        ///
        /// 해석 :
        /// - "이 Ability를 지금 이 순간 시작할 수 있는가?"
        /// </summary>
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
                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                tags.PrintHasTags("HAS TAG");
                TagRegistry.PrintTagMaskLog(_blockMask);
                #endif

                // 금지 태그가 하나라도 있으면 발동 불가
                if (tags.HasAny(_blockMask)) return false;

                // 필수 태그를 하나라도 만족하지 못하면 발동 불가
                if (!tags.HasAll(_reqMask)) return false;
            }

            // 타겟이 명시된 경우에만 Tag 조건 검사
            // 거리/가시선/레이어/세부 판정은 Logic에서 처리
            if (target != null && !IsValidTargetTags(target))
                return false;

            return true;
        }

        /// <summary>
        /// 책임 :
        /// - target에 대해서 "태그 조건만" 검사한다.
        /// - 거리, 방향, 가시선, 충돌 범위 같은 물리/전투 판정은 다루지 않는다.
        ///
        /// 해석 :
        /// - "이 대상은 태그 관점에서 유효한 대상인가?"
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

        /// <summary>
        /// 책임 :
        /// - Ability의 cost를 caster에게 적용한다.
        /// - 현재는 단일 AttributeDefinition에 대한 고정 비용 차감만 담당한다.
        ///
        /// 주의 :
        /// - 실제 commit 타이밍은 AbilitySystem / Coordinator 흐름에 따라 결정된다.
        /// </summary>
        public void ApplyCost(GameObject caster)
        {
            if (!HasCost) return;

            var attributeSet = caster.GetComponent<AttributeSet>();
            if (attributeSet != null)
                attributeSet.TryModifyAttributeValue(costAttribute, -cost, this);
        }
    }
}
