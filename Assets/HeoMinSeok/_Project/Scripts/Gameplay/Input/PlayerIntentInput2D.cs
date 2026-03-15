using UnityEngine;
using UnityGAS;

[DisallowMultipleComponent]
public sealed class PlayerIntentInput2D : MonoBehaviour, IIntentMovementSource2D
{
    [Header("Refs")]
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private PlayerAim2D aim;

    [Header("Tags")]
    [Tooltip("이 태그가 있으면 WASD 대신 에임 방향으로 강제 이동합니다.")]
    [SerializeField] private GameplayTag forcedMoveTag;

    public Vector2 MoveInput { get; private set; }

    private void Awake()
    {
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (aim == null) aim = GetComponent<PlayerAim2D>();
    }

    private void Update()
    {
        bool forced = tagSystem != null && forcedMoveTag != null && tagSystem.HasTag(forcedMoveTag);

        if (!forced)
        {
            float x = Input.GetAxisRaw("Horizontal");
            float y = Input.GetAxisRaw("Vertical");
            MoveInput = new Vector2(x, y).normalized;
        }
        else
        {
            Vector2 aimDir = aim != null ? aim.AimDirection : Vector2.right;
            MoveInput = aimDir.sqrMagnitude > 0.0001f ? aimDir.normalized : Vector2.right;
        }
    }

    public IntentMovementData GetIntent()
    {
        return IntentMovementData.FromDirection(MoveInput);
    }
}