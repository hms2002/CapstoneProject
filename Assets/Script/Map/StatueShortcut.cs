using UnityEngine;

public class StatueShortcut : TemporaryShortcut
{
    public enum CostType { MagicStone, HP }

    [Header("비용 설정")]
    public CostType costType;
    public int costAmount = 100;

    protected override bool CheckCondition(TempPlayer player)
    {
        switch (costType)
        {
            case CostType.MagicStone:
                if (GameDataManager.Instance.Data.magicStone >= costAmount)
                {
                    GameDataManager.Instance.Data.magicStone -= costAmount;
                    return true;
                }
                Debug.Log("마정석이 부족합니다.");
                return false;

            case CostType.HP:
                // TempPlayer에 체력 관련 함수가 있다고 가정
                // if (player.CurrentHP > costAmount) {
                //     player.TakeDamage(costAmount);
                //     return true;
                // }
                Debug.Log("체력이 부족합니다.");
                return false;
        }
        return false;
    }

    public override string GetInteractDescription()
    {
        string typeName = costType == CostType.MagicStone ? "마정석" : "체력";
        return $"{typeName} {costAmount} 바치기";
    }
}