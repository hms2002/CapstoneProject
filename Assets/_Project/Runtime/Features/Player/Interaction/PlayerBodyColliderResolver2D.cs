using UnityEngine;

/// <summary>
/// 책임 : trigger·공격 히트박스·센서가 부모 플레이어를 타고 본체 판정을 훔치지 않도록 실제 이동 몸체 Collider만 IPlayerInteractor로 정규화한다.
/// </summary>
public static class PlayerBodyColliderResolver2D
{
    public static bool TryResolve(
        Collider2D candidate,
        out IPlayerInteractor player)
    {
        player = null;
        if (candidate == null ||
            !candidate.enabled ||
            candidate.isTrigger ||
            !candidate.gameObject.activeInHierarchy)
        {
            return false;
        }

        PlayerInteractor2D runtimePlayer =
            candidate.GetComponentInParent<PlayerInteractor2D>();
        if (runtimePlayer != null)
        {
            if (runtimePlayer.BodyCollider != candidate)
                return false;

            player = runtimePlayer;
            return true;
        }

        IPlayerInteractor directPlayer = candidate.GetComponent<IPlayerInteractor>();
        if (directPlayer == null || directPlayer.Transform != candidate.transform)
            return false;

        player = directPlayer;
        return true;
    }
}
