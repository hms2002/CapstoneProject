using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어의 이동 의도를 수집해 이동 시스템이 읽을 수 있는 형태로 제공한다.
/// - 강제 이동 및 이동 차단 tag를 함께 반영해 현재 상태에 맞는 최종 이동 입력만 내보낸다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerIntentInput2D : MonoBehaviour, IIntentMovementSource2D, IAbilityMoveInputSource2D
{
    private const string MoveBlockedTagResourcePath = "Tags/State.Move.Intent.Blocked";

    [Header("Refs")]
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private PlayerAim2D aim;
    [SerializeField] private PlayerInteractor2D player;

    [Header("Tags")]
    [Tooltip("이 태그가 있으면 WASD 대신 AimDirection 방향으로 강제 이동합니다.")]
    [SerializeField] private GameplayTag forcedMoveTag;
    [SerializeField] private GameplayTag moveBlockedTag;

    /// <summary>
    /// 책임 :
    /// - 플레이어가 실제로 누르고 있는 원본 이동 입력을 보관한다.
    /// - 이동 차단 tag와 무관하게 "입력이 무엇이었는지"를 참조해야 하는 능력이 사용한다.
    /// </summary>
    public Vector2 RawMoveInput { get; private set; }

    /// <summary>
    /// 책임 :
    /// - 이동 차단 및 강제 이동 tag를 반영한 최종 이동 입력을 보관한다.
    /// - 일반 이동 시스템은 이 값을 읽어 현재 허용된 이동만 적용한다.
    /// </summary>
    public Vector2 MoveInput { get; private set; }

    public Vector2 AbilityMoveInput => MoveInput;

    private void Awake()
    {
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (aim == null) aim = GetComponent<PlayerAim2D>();
        if (player == null) player = GetComponent<PlayerInteractor2D>();
        if (moveBlockedTag == null) moveBlockedTag = Resources.Load<GameplayTag>(MoveBlockedTagResourcePath);
    }

    private void Update()
    {
        RawMoveInput = InputActionQuery.GetMoveVectorNormalized();

        if (player != null && player.CurrentState != InteractState.Idle)
        {
            MoveInput = Vector2.zero;
            return;
        }

        if (tagSystem != null && moveBlockedTag != null && tagSystem.HasTag(moveBlockedTag))
        {
            MoveInput = Vector2.zero;
            return;
        }

        bool forced = tagSystem != null &&
                      forcedMoveTag != null &&
                      tagSystem.HasTag(forcedMoveTag);

        if (!forced)
        {
            MoveInput = RawMoveInput;
        }
        else
        {
            Vector2 aimDir = aim != null ? aim.AimDirection : Vector2.right;
            MoveInput = aimDir.sqrMagnitude > 0.0001f
                ? aimDir.normalized
                : Vector2.right;
        }
    }

    public IntentMovementData GetIntent()
    {
        if (player != null && player.CurrentState != InteractState.Idle)
            return IntentMovementData.None;

        if (tagSystem != null && moveBlockedTag != null && tagSystem.HasTag(moveBlockedTag))
            return IntentMovementData.None;

        return IntentMovementData.FromDirection(MoveInput);
    }
}
