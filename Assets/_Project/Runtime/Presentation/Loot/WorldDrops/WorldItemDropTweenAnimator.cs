using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// 책임 : 월드 드롭 오브젝트의 DOTween 기반 포물선 낙하와 착지 보조 연출 실행을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WorldItemDropTweenAnimator : MonoBehaviour, IWorldItemDropAnimator
{
    [Header("Timing")]
    [SerializeField, Min(0.1f)] private float minDuration = 0.3f;
    [SerializeField, Min(0.1f)] private float maxDuration = 0.7f;
    [SerializeField, Min(0f)] private float durationPerUnit = 0.08f;

    [Header("Arc")]
    [SerializeField, Min(0.15f)] private float minArcHeight = 0.45f;
    [SerializeField, Min(0f)] private float arcHeightPerUnit = 0.25f;
    [SerializeField, Min(0.2f)] private float maxArcHeight = 1.15f;

    [Header("Spin")]
    [SerializeField, Range(1, 2)] private int minFullRotations = 1;
    [SerializeField, Range(1, 2)] private int maxFullRotations = 2;

    private Sequence activeSequence;
    private Tween travelTween;
    private Coroutine landingRoutine;
    private readonly List<Coroutine> landingChildRoutines = new();

    public void PlayDrop(Vector3 startPosition, Vector3 landingPosition, Action onCompleted)
    {
        KillActiveSequence();

        transform.position = startPosition;
        transform.rotation = Quaternion.identity;

        IWorldItemDropLandingVisual[] landingVisuals = ResolveLandingVisuals();
        for (int i = 0; i < landingVisuals.Length; i++)
            landingVisuals[i]?.OnDropTravelStarted();

        Vector3 visualCenterLocal = ResolveVisualCenterLocal();
        float distance = Vector2.Distance(startPosition, landingPosition);
        float duration = Mathf.Clamp(minDuration + distance * durationPerUnit, minDuration, maxDuration);
        float arcHeight = Mathf.Clamp(minArcHeight + distance * arcHeightPerUnit, minArcHeight, maxArcHeight);
        int turns = UnityEngine.Random.Range(minFullRotations, maxFullRotations + 1);
        float spinDegrees = 360f * turns * (UnityEngine.Random.value < 0.5f ? -1f : 1f);

        activeSequence = DOTween.Sequence()
            .SetLink(gameObject, LinkBehaviour.KillOnDestroy)
            .SetUpdate(UpdateType.Normal);

        travelTween = DOVirtual.Float(0f, 1f, duration, t =>
            {
                Vector3 basePosition = EvaluateParabolicPosition(startPosition, landingPosition, arcHeight, t);
                Quaternion rotation = Quaternion.Euler(0f, 0f, spinDegrees * EaseOutQuad(t));
                transform.SetPositionAndRotation(
                    basePosition + ResolveCenterPivotOffset(visualCenterLocal, rotation),
                    rotation);
            })
            .SetEase(Ease.Linear);

        activeSequence.Append(travelTween);
        activeSequence.AppendCallback(() =>
        {
            travelTween = null;
            transform.SetPositionAndRotation(landingPosition, Quaternion.identity);
        });
        activeSequence.OnComplete(() =>
        {
            activeSequence = null;
            travelTween = null;
            transform.SetPositionAndRotation(landingPosition, Quaternion.identity);
            landingRoutine = StartCoroutine(PlayLandingVisualsThenCompleteRoutine(landingVisuals, onCompleted));
        });
    }

    private void OnDisable()
    {
        KillActiveSequence();
    }

    private void OnDestroy()
    {
        KillActiveSequence();
    }

    private void KillActiveSequence()
    {
        if (travelTween != null)
        {
            travelTween.Kill();
            travelTween = null;
        }

        if (landingRoutine != null)
        {
            StopCoroutine(landingRoutine);
            landingRoutine = null;
        }

        for (int i = 0; i < landingChildRoutines.Count; i++)
        {
            if (landingChildRoutines[i] != null)
                StopCoroutine(landingChildRoutines[i]);
        }

        landingChildRoutines.Clear();

        if (activeSequence == null)
            return;

        activeSequence.Kill();
        activeSequence = null;
    }

    private static Vector3 EvaluateParabolicPosition(Vector3 startPosition, Vector3 landingPosition, float arcHeight, float t)
    {
        Vector3 linearPosition = Vector3.LerpUnclamped(startPosition, landingPosition, t);
        float arcOffset = 4f * arcHeight * t * (1f - t);
        linearPosition.y += arcOffset;
        return linearPosition;
    }

    private Vector3 ResolveVisualCenterLocal()
    {
        IItemDisplayVisualPresenter presenter = ResolveItemDisplayPresenter();
        if (presenter != null && presenter.TryResolveVisualBoundsWorld(out Bounds presenterBounds))
        {
            Vector3 presenterCenter = transform.InverseTransformPoint(presenterBounds.center);
            presenterCenter.z = 0f;
            return presenterCenter;
        }

        SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(includeInactive: true);
        bool hasBounds = false;
        Bounds visualBounds = default;

        for (int i = 0; i < renderers.Length; i++)
        {
            SpriteRenderer renderer = renderers[i];
            if (renderer == null || !renderer.enabled || renderer.sprite == null)
                continue;

            if (!hasBounds)
            {
                visualBounds = renderer.bounds;
                hasBounds = true;
                continue;
            }

            visualBounds.Encapsulate(renderer.bounds);
        }

        if (!hasBounds)
            return Vector3.zero;

        Vector3 localCenter = transform.InverseTransformPoint(visualBounds.center);
        localCenter.z = 0f;
        return localCenter;
    }

    private static Vector3 ResolveCenterPivotOffset(Vector3 visualCenterLocal, Quaternion rotation)
    {
        return visualCenterLocal - rotation * visualCenterLocal;
    }

    private IWorldItemDropLandingVisual[] ResolveLandingVisuals()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        List<IWorldItemDropLandingVisual> visuals = null;

        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is not IWorldItemDropLandingVisual landingVisual)
                continue;

            visuals ??= new List<IWorldItemDropLandingVisual>();
            visuals.Add(landingVisual);
        }

        return visuals != null
            ? visuals.ToArray()
            : Array.Empty<IWorldItemDropLandingVisual>();
    }

    private IItemDisplayVisualPresenter ResolveItemDisplayPresenter()
    {
        MonoBehaviour[] behaviours = GetComponentsInChildren<MonoBehaviour>(includeInactive: true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IItemDisplayVisualPresenter presenter)
                return presenter;
        }

        return null;
    }

    private IEnumerator PlayLandingVisualsThenCompleteRoutine(
        IWorldItemDropLandingVisual[] landingVisuals,
        Action onCompleted)
    {
        yield return PlayLandingVisualsRoutine(landingVisuals);

        landingRoutine = null;
        onCompleted?.Invoke();
    }

    private IEnumerator PlayLandingVisualsRoutine(IWorldItemDropLandingVisual[] landingVisuals)
    {
        if (landingVisuals == null || landingVisuals.Length == 0)
            yield break;

        int runningCount = 0;

        for (int i = 0; i < landingVisuals.Length; i++)
        {
            IEnumerator routine = landingVisuals[i]?.PlayDropLandingRoutine();
            if (routine == null)
                continue;

            runningCount++;
            Coroutine childRoutine = StartCoroutine(PlayChildLandingRoutine(routine, () => runningCount--));
            landingChildRoutines.Add(childRoutine);
        }

        while (runningCount > 0)
            yield return null;

        landingChildRoutines.Clear();
    }

    private IEnumerator PlayChildLandingRoutine(IEnumerator routine, Action onCompleted)
    {
        yield return routine;
        onCompleted?.Invoke();
    }

    private static float EaseOutQuad(float t)
    {
        t = Mathf.Clamp01(t);
        return 1f - (1f - t) * (1f - t);
    }
}
