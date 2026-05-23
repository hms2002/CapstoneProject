using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - CombatDamageAction 기반 팝업과 Attribute 감소 listener 기반 fallback 팝업이 같은 피해를 중복 표시하지 않게 조율한다.
/// - 기존 씬/프리팹의 DamagePopupListener2D를 즉시 제거하지 않아도 새 메타데이터 팝업 경로를 안전하게 도입하게 한다.
/// </summary>
public static class DamagePopupDuplicateSuppressor
{
    /// <summary>
    /// 책임 :
    /// - 특정 타겟에 대해 방금 표시한 피해량과 중복 억제 만료 시각을 보관한다.
    /// - fallback listener가 같은 피해를 다시 표시할지 판단하는 최소 상태 단위다.
    /// </summary>
    private readonly struct SuppressionEntry
    {
        public readonly float Amount;
        public readonly float ExpireTime;

        public SuppressionEntry(float amount, float expireTime)
        {
            Amount = amount;
            ExpireTime = expireTime;
        }
    }

    private const float DefaultWindowSeconds = 0.08f;
    private const float AmountTolerance = 0.05f;
    private static readonly Dictionary<int, SuppressionEntry> s_recentByTarget = new();

    public static void Register(GameObject target, float amount, float windowSeconds = DefaultWindowSeconds)
    {
        if (target == null || amount <= 0f)
            return;

        s_recentByTarget[target.GetInstanceID()] = new SuppressionEntry(
            amount,
            Time.time + Mathf.Max(0.01f, windowSeconds));
    }

    public static bool TryConsume(GameObject target, float amount)
    {
        if (target == null || amount <= 0f)
            return false;

        int id = target.GetInstanceID();
        if (!s_recentByTarget.TryGetValue(id, out SuppressionEntry entry))
            return false;

        if (Time.time > entry.ExpireTime)
        {
            s_recentByTarget.Remove(id);
            return false;
        }

        if (Mathf.Abs(entry.Amount - amount) > AmountTolerance)
            return false;

        s_recentByTarget.Remove(id);
        return true;
    }
}
