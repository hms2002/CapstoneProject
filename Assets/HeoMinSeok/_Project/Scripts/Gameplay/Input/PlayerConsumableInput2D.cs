using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 플레이어의 1회용 아이템 슬롯 입력(1~4)을 처리하고 인벤토리 사용 호출로 변환한다.
/// - UI 잠금/스킬 잠금 상태에서는 consumable 사용 입력을 무시한다.
/// </summary>
public class PlayerConsumableInput2D : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private PlayerConsumableInventory consumableInventory;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayTag skillBlockedTag;

    private const string SkillBlockedTagResourcePath = "Tags/State.Skill.Blocked";

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
    }

    private void Update()
    {
        if (consumableInventory == null)
            return;

        if (IsUseBlocked())
            return;

        if (Input.GetKeyDown(KeyCode.Alpha1))
            consumableInventory.TryUseAt(0);
        else if (Input.GetKeyDown(KeyCode.Alpha2))
            consumableInventory.TryUseAt(1);
        else if (Input.GetKeyDown(KeyCode.Alpha3))
            consumableInventory.TryUseAt(2);
        else if (Input.GetKeyDown(KeyCode.Alpha4))
            consumableInventory.TryUseAt(3);
    }

    private bool IsUseBlocked()
    {
        return tagSystem != null &&
               skillBlockedTag != null &&
               tagSystem.HasTag(skillBlockedTag);
    }
}
