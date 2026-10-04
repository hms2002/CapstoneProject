using UnityEngine;

public abstract class AffectionEffect : ScriptableObject
{
    [Header("Reward UI Display")]
    public string rewardText;  // ContextText에 출력될 텍스트 (예: "무기 슬롯 추가")
    public Sprite rewardIcon;
    public string RewardText => GameText.Asset(this, "rewardText", rewardText);

    public abstract void Execute();
}
