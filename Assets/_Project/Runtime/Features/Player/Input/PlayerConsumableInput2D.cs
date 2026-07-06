using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어의 1회용 아이템 슬롯 입력(1~4)을 처리하고 인벤토리 사용 호출로 변환한다.
/// - UI/대화/연출/스킬 잠금 상태에서는 consumable 사용 입력을 무시한다.
/// </summary>
public class PlayerConsumableInput2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerConsumableInventory consumableInventory;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayTag skillBlockedTag;

    private const string SkillBlockedTagResourcePath = "Tags/State.Skill.Blocked";
    private PlayerInteractor2D playerInteractor;

    public static PlayerConsumableInput2D GetOrAdd(Transform owner)
    {
        if (owner == null)
            return null;

        var input = owner.GetComponent<PlayerConsumableInput2D>();
        return input != null ? input : owner.gameObject.AddComponent<PlayerConsumableInput2D>();
    }

    private void Awake()
    {
        if (consumableInventory == null)
            consumableInventory = GetComponent<PlayerConsumableInventory>();
        if (tagSystem == null)
            tagSystem = GetComponent<TagSystem>();
        if (skillBlockedTag == null)
            skillBlockedTag = Resources.Load<GameplayTag>(SkillBlockedTagResourcePath);
        if (playerInteractor == null)
            playerInteractor = GetComponent<PlayerInteractor2D>();
    }

    private void Update()
    {
        if (consumableInventory == null)
            return;

        if (IsUseBlocked())
            return;

        if (InputActionQuery.WasPressedThisFrame(InputActionId.ConsumableSlot1))
            consumableInventory.TryUseAt(0);
        else if (InputActionQuery.WasPressedThisFrame(InputActionId.ConsumableSlot2))
            consumableInventory.TryUseAt(1);
        else if (InputActionQuery.WasPressedThisFrame(InputActionId.ConsumableSlot3))
            consumableInventory.TryUseAt(2);
        else if (InputActionQuery.WasPressedThisFrame(InputActionId.ConsumableSlot4))
            consumableInventory.TryUseAt(3);
    }

    private bool IsUseBlocked()
    {
        if (playerInteractor == null)
            playerInteractor = GetComponent<PlayerInteractor2D>();

        if (playerInteractor != null && playerInteractor.CurrentState != InteractState.Idle)
            return true;

        if (IsGameplayInputBlockedByUiOrFlow())
            return true;

        return tagSystem != null &&
               skillBlockedTag != null &&
               tagSystem.HasTag(skillBlockedTag);
    }

    private static bool IsGameplayInputBlockedByUiOrFlow()
    {
        if (DialoguePlayback.IsPlaying)
            return true;

        if (UiInteractionStateQuery.HasBlockingUI())
            return true;

        if (SceneTransitionPlayback.IsTransitionActive)
            return true;

        return LoadingPresentationQuery.IsActiveLoadingPresentation;
    }
}
