using System;
using UnityEngine;

/// <summary>
/// 책임 : 월드 드롭 낙하 연출 요청을 Presentation 소유 DOTween animator 컴포넌트로 수행한다.
/// </summary>
public sealed class WorldItemDropAnimationService : IWorldItemDropAnimationBackend
{
    private static readonly WorldItemDropAnimationService Instance = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterBackend()
    {
        WorldItemDropAnimationPlayback.RegisterBackend(Instance);
    }

    public bool TryPlayDrop(GameObject owner, Vector3 startPosition, Vector3 landingPosition, Action onCompleted)
    {
        if (owner == null)
            return false;

        IWorldItemDropAnimator animator = ResolveAnimator(owner);
        if (animator == null)
            animator = owner.AddComponent<WorldItemDropTweenAnimator>();

        animator.PlayDrop(startPosition, landingPosition, onCompleted);
        return true;
    }

    private static IWorldItemDropAnimator ResolveAnimator(GameObject owner)
    {
        if (owner == null)
            return null;

        MonoBehaviour[] behaviours = owner.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IWorldItemDropAnimator animator)
                return animator;
        }

        return null;
    }
}
