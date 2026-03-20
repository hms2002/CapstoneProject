using UnityEngine;

public abstract class UpgradeEffectSO : ScriptableObject
{
    [Header("Reward UI Display")]
    public string rewardText;  // ContextText에 출력될 텍스트 (예: "공격력 10 증가")
    public Sprite rewardIcon; // RewardEffectSlotUI에 표시될 아이콘

    public abstract void ApplyEffect(SampleTopDownPlayer player);
}