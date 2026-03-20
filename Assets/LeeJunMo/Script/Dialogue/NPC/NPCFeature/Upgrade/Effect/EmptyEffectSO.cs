using UnityEngine;

[CreateAssetMenu(fileName = "Effect_Empty", menuName = "Game/Upgrade Effects/Empty (Dummy)")]
public class EmptyEffectSO : UpgradeEffectSO
{
    [Header("개발자 메모")]
    [TextArea]
    public string memo = "이 효과는 아무 기능도 없습니다.\n단순히 길을 뚫거나, 나중에 구현할 기능을 위해 비워둔 것입니다.";

    public override void ApplyEffect(SampleTopDownPlayer player)
    {
        // 로직 없음 (의도됨)
        Debug.Log($"[EmptyEffect] '{name}' 효과가 적용되었지만, 아무 일도 일어나지 않았습니다.");
    }
}