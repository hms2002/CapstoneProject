using System.Collections.Generic;
using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 게임플레이 효과를 정의하는 ScriptableObject 베이스 클래스.
    ///
    /// 역할:
    /// - 이 효과가 "무엇인지"를 정의한다.
    ///   예: 이름, 설명, 기본 지속시간, 스택 규칙, 부여 태그, 연출 큐,
    ///   그리고 Apply / Remove를 통한 payload 로직.
    ///
    /// 중요한 점:
    /// - 이 클래스는 "런타임 인스턴스"가 아니라 "효과 정의 자산"이다.
    /// - 런타임에서 활성 효과 목록을 관리하고,
    ///   지속시간을 감소시키고,
    ///   cueWhileActive의 Add/Remove 타이밍을 관리하고,
    ///   granted tag를 부여/회수하고,
    ///   source/context를 추적하는 책임은 GameplayEffectRunner 쪽에 있다.
    ///
    /// 적용 경로:
    /// - ApplyEffectSpec(...) : 런타임 문맥을 풍부하게 전달하는 정식 경로.
    ///   새 시스템은 가능하면 이 경로를 우선 사용한다.
    /// - ApplyEffect(...) : 단순/고정 효과를 위한 간편 경로.
    ///   레거시 코드나 문맥 정보가 거의 필요 없는 경우에 사용한다.
    ///
    /// duration 의미:
    /// - 이 duration은 GameplayEffectRunner가 관리하는
    ///   "활성 GameplayEffect 인스턴스의 수명"을 뜻한다.
    /// - 내부 payload(예: AttributeModifier)가 별도의 duration을 가진다면
    ///   그것은 다른 개념이며, 이 값과 항상 같을 필요는 없다.
    /// </summary>
    public abstract class GameplayEffect : ScriptableObject
    {
        [Header("Info")]
        public string effectName = "New Effect";
        [TextArea] public string description = "Effect description.";
        public Sprite icon;

        [Header("Duration")]
        [Tooltip("GameplayEffectRunner가 관리하는 활성 효과의 수명. 0 이하이면 즉발 효과로 취급된다.")]
        public float duration = 0f;

        [Header("Stacking")]
        public bool canStack = false;
        public int maxStacks = 1;

        [Header("Granted Tags")]
        public List<GameplayTag> grantedTags = new List<GameplayTag>();

        [Header("Granted Tag Sets (Optional)")]
        public List<GameplayTagSet> grantedTagSets = new();

        [Header("Audio (Optional)")]
        public SoundRef audioOnExecute;
        [Tooltip("Loops once when the effect becomes active and stops when the effect is removed.")]
        public SoundRef audioWhileActive;
        public SoundRef audioOnRemove;

        [Header("Camera Shake (Optional)")]
        public CameraShakeHook cameraShakeOnExecute;
        [Tooltip("Played once on active enter. Not retriggered every frame while the effect remains active.")]
        public CameraShakeHook cameraShakeWhileActive;
        public CameraShakeHook cameraShakeOnRemove;

        [Header("Spawned Presentation (Optional)")]
        public WorldPresentationHook presentationOnExecute;
        [Tooltip("Spawned once on active enter. Use a looping prefab or ManualRelease lifetime when persistence is needed.")]
        public WorldPresentationHook presentationWhileActive;
        public WorldPresentationHook presentationOnRemove;

        [Header("GameplayCue (Optional)")]
        [Tooltip("효과가 실행될 때 1회 실행되는 큐. Duration 효과는 Runner 정책에 따라 최초 적용/갱신 시 실행될 수 있다.")]
        public GameplayTag cueOnExecute;

        [Tooltip("Duration 효과가 살아 있는 동안 유지되는 큐. Add/Remove 타이밍은 GameplayEffectRunner가 관리한다.")]
        [Tooltip("Added once on active enter and removed once on active exit. GameplayEffectRunner owns the Add/Remove timing.")]
        public GameplayTag cueWhileActive;

        [Tooltip("Duration 효과가 종료되거나 제거될 때 1회 실행되는 큐.")]
        public GameplayTag cueOnRemove;

        public bool IsInstant => duration <= 0f;
        public bool IsDuration => duration > 0f;

        public GameplayPresentationPhase GetExecutePhase() => GameplayPresentationPhase.Create(
            audioOnExecute,
            cameraShakeOnExecute,
            presentationOnExecute,
            cues: null,
            legacyCue: cueOnExecute);

        public GameplayPresentationPhase GetWhileActivePhase() => GameplayPresentationPhase.Create(
            audioWhileActive,
            cameraShakeWhileActive,
            presentationWhileActive,
            cues: null,
            legacyCue: cueWhileActive);

        public GameplayPresentationPhase GetRemovePhase() => GameplayPresentationPhase.Create(
            audioOnRemove,
            cameraShakeOnRemove,
            presentationOnRemove,
            cues: null,
            legacyCue: cueOnRemove);

        /// <summary>
        /// 효과 payload 적용 훅.
        /// - GameplayEffectRunner가 효과를 적용하거나 갱신할 때 호출한다.
        /// - Spec을 지원하는 효과라면 Runner가 ISpecGameplayEffect 경로를 우선 사용할 수 있다.
        /// </summary>
        public abstract void Apply(GameObject target, GameObject instigator, int stackCount = 1);

        /// <summary>
        /// 효과 payload 제거 훅.
        /// - GameplayEffectRunner가 활성 Duration 효과를 종료/제거할 때 호출한다.
        /// - 즉발 효과는 보통 여기서 아무 일도 하지 않는다.
        /// </summary>
        public abstract void Remove(GameObject target, GameObject instigator);
    }
}
