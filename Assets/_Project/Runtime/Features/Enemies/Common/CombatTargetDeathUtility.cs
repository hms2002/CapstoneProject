using UnityEngine;

/// <summary>
/// 책임:
/// 적 AI가 플레이어 사망 시퀀스 중인 타겟을 새 공격 대상으로 삼지 않도록 공통 판정 경로를 제공한다.
/// </summary>
public static class CombatTargetDeathUtility
{
    public static bool IsPlayerDeathSequenceRunning(Transform candidate)
    {
        PlayerDeathReturnToHub2D deathController = ResolvePlayerDeathController(candidate);
        return deathController != null && deathController.IsDeathSequenceRunning;
    }

    private static PlayerDeathReturnToHub2D ResolvePlayerDeathController(Transform candidate)
    {
        if (candidate != null)
        {
            PlayerDeathReturnToHub2D direct = candidate.GetComponentInParent<PlayerDeathReturnToHub2D>();
            if (direct != null)
                return direct;
        }

        Transform registeredPlayer = PlayerRuntimeRegistry.GetPlayerTransform();
        return registeredPlayer != null
            ? registeredPlayer.GetComponent<PlayerDeathReturnToHub2D>()
            : null;
    }
}
