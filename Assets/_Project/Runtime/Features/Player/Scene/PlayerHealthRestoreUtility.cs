using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 : 씬 진입/복원 정책에서 플레이어의 현재 체력을 연결된 최대체력 값까지 회복시키는 공통 처리를 제공한다.
/// </summary>
internal static class PlayerHealthRestoreUtility
{
    public static bool FillLinkedHealthToMax(GameObject player, Object source)
    {
        if (player == null)
            return false;

        AttributeSet attributeSet = player.GetComponent<AttributeSet>();
        if (attributeSet == null)
            return false;

        bool didApply = false;
        foreach (AttributeSet.MaxLink link in attributeSet.EnumerateMaxLinks())
        {
            if (link.value == null || link.max == null)
                continue;

            float maxValue = attributeSet.GetCurrentValue(link.max);
            if (attributeSet.TrySetCurrentValue(link.value, maxValue, source))
                didApply = true;
        }

        return didApply;
    }
}
