using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 컷신/보스 연출 등 게임플레이 보호가 필요한 동안 플레이어 입력 잠금과 무적 태그를 공용 규칙으로 관리한다.
/// - 여러 시스템이 동시에 보호를 요청해도 토큰 기반으로 중복 획득/해제를 안전하게 처리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerCinematicProtection : MonoBehaviour
{
    private const string DefaultPlayerInvulnerableTagResourcePath = "Tags/State.Invulnerable";

    private readonly HashSet<object> activeOwners = new();
    private readonly List<ManagedBehaviourState> lockedBehaviourStates = new();

    [SerializeField] private GameplayTag invulnerableTag;
    [SerializeField] private PlayerInteractor2D playerInteractor;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private MovementMotor2D movementMotor;
    [SerializeField] private Rigidbody2D rigidbody2D;

    private InteractState previousInteractState = InteractState.Idle;
    private bool hasAppliedInvulnerableTag;

    /// <summary>
    /// 책임 :
    /// - 보호 진입 전에 각 입력 Behaviour의 활성 상태를 기억해 복원 시 원래 상태로 되돌릴 수 있게 한다.
    /// - PlayerCinematicProtection 내부에서만 쓰이는 잠금 스냅샷 데이터 역할을 한다.
    /// </summary>
    private readonly struct ManagedBehaviourState
    {
        public ManagedBehaviourState(Behaviour behaviour, bool wasEnabled)
        {
            Behaviour = behaviour;
            WasEnabled = wasEnabled;
        }

        public Behaviour Behaviour { get; }
        public bool WasEnabled { get; }
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnDisable()
    {
        ForceReleaseAll();
    }

    /// <summary>
    /// 책임 :
    /// - 특정 연출 시스템이 플레이어 보호를 요청할 때 참조 카운트 역할의 owner 토큰을 등록한다.
    /// - 첫 획득 시점에만 실제 입력 잠금/무적 상태를 적용한다.
    /// </summary>
    public void Acquire(object ownerToken)
    {
        if (ownerToken == null)
            return;

        ResolveReferences();
        if (!activeOwners.Add(ownerToken))
            return;

        if (activeOwners.Count == 1)
            ApplyProtection();
    }

    /// <summary>
    /// 책임 :
    /// - 특정 연출 시스템의 보호 요청이 끝났을 때 owner 토큰을 해제한다.
    /// - 마지막 토큰이 빠질 때만 실제 입력 잠금/무적 상태를 복원한다.
    /// </summary>
    public void Release(object ownerToken)
    {
        if (ownerToken == null)
            return;

        if (!activeOwners.Remove(ownerToken))
            return;

        if (activeOwners.Count == 0)
            RestoreProtection();
    }

    /// <summary>
    /// 책임 :
    /// - 비활성화/강제 종료 경로에서 남아 있는 모든 보호 상태를 즉시 회수한다.
    /// - 토큰 상태와 실제 플레이어 잠금 상태가 어긋나지 않도록 최종 정리 책임을 진다.
    /// </summary>
    public void ForceReleaseAll()
    {
        activeOwners.Clear();
        RestoreProtection();
    }

    private void ResolveReferences()
    {
        if (playerInteractor == null)
            playerInteractor = GetComponent<PlayerInteractor2D>();

        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();

        if (movementMotor == null)
            movementMotor = GetComponent<MovementMotor2D>();

        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();

        if (invulnerableTag == null)
            invulnerableTag = Resources.Load<GameplayTag>(DefaultPlayerInvulnerableTagResourcePath);
    }

    private void ApplyProtection()
    {
        if (playerInteractor != null)
        {
            previousInteractState = NormalizeRestoreState(playerInteractor.CurrentState);
            playerInteractor.SetInteractState(InteractState.None);
        }

        CacheAndDisableBehaviour(GetComponent<PlayerIntentInput2D>());
        CacheAndDisableBehaviour(GetComponent<PlayerCombatInput2D>());
        CacheAndDisableBehaviour(GetComponent<PlayerAim2D>());
        CacheAndDisableBehaviour(GetComponent<PlayerConsumableInput2D>());

        movementMotor?.StopAllMotion();

        if (rigidbody2D != null)
        {
            rigidbody2D.linearVelocity = Vector2.zero;
            rigidbody2D.angularVelocity = 0f;
        }

        if (tagSystem != null && invulnerableTag != null && !hasAppliedInvulnerableTag)
        {
            tagSystem.AddTag(invulnerableTag, 1);
            hasAppliedInvulnerableTag = true;
        }
    }

    private void RestoreProtection()
    {
        if (tagSystem != null && invulnerableTag != null && hasAppliedInvulnerableTag)
        {
            tagSystem.RemoveTag(invulnerableTag, 1);
            hasAppliedInvulnerableTag = false;
        }

        for (int i = lockedBehaviourStates.Count - 1; i >= 0; i--)
        {
            ManagedBehaviourState state = lockedBehaviourStates[i];
            if (state.Behaviour != null)
                state.Behaviour.enabled = state.WasEnabled;
        }

        lockedBehaviourStates.Clear();

        if (playerInteractor != null)
            playerInteractor.SetInteractState(previousInteractState);

        previousInteractState = InteractState.Idle;
    }

    private void CacheAndDisableBehaviour(Behaviour behaviour)
    {
        if (behaviour == null)
            return;

        lockedBehaviourStates.Add(new ManagedBehaviourState(behaviour, behaviour.enabled));
        behaviour.enabled = false;
    }

    private static InteractState NormalizeRestoreState(InteractState state)
    {
        return state == InteractState.None ? InteractState.Idle : state;
    }
}
