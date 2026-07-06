using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 책임 : 월드 드롭 gameplay가 concrete sprite presenter 없이 드롭 아이콘 표시를 요청하게 하는 계약이다.
/// </summary>
public interface IWorldDropSpritePresenter
{
    SpriteRenderer Renderer { get; }

    void Apply(Sprite sprite);
    void Apply(Sprite sprite, bool forceRawSpriteSize);
}

/// <summary>
/// 책임 : 월드 드롭 gameplay가 concrete 낙하 애니메이터 없이 드롭 이동 연출을 요청하게 하는 계약이다.
/// </summary>
public interface IWorldItemDropAnimator
{
    void PlayDrop(Vector3 startPosition, Vector3 landingPosition, Action onCompleted);
}

/// <summary>
/// 책임 : 월드 드롭 gameplay의 낙하 연출 요청을 concrete animation 구현으로 연결한다.
/// </summary>
public interface IWorldItemDropAnimationBackend
{
    bool TryPlayDrop(GameObject owner, Vector3 startPosition, Vector3 landingPosition, Action onCompleted);
}

/// <summary>
/// 책임 : 월드 드롭 gameplay 호출자가 concrete animator 타입 없이 낙하 연출을 요청하게 한다.
/// </summary>
public static class WorldItemDropAnimationPlayback
{
    private static IWorldItemDropAnimationBackend backend;

    public static void RegisterBackend(IWorldItemDropAnimationBackend newBackend)
    {
        backend = newBackend;
    }

    public static bool TryPlayDrop(GameObject owner, Vector3 startPosition, Vector3 landingPosition, Action onCompleted)
    {
        return backend != null &&
               backend.TryPlayDrop(owner, startPosition, landingPosition, onCompleted);
    }
}

/// <summary>
/// 책임 : 월드 드롭 낙하 애니메이터가 concrete 착지 visual 구현 없이 착지 보조 연출을 연결하게 하는 계약이다.
/// </summary>
public interface IWorldItemDropLandingVisual
{
    void OnDropTravelStarted();
    IEnumerator PlayDropLandingRoutine();
}
