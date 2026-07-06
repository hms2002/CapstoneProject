using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// 책임: 시네마틱 카메라 전환이 안정될 때까지 코루틴 호출자가 기다릴 수 있는 공통 대기 유틸리티를 제공한다.
/// </summary>
public static class CameraCinematicWaitUtility
{
    private const float DefaultSettleTimeoutSeconds = 3f;
    private const float DefaultViewportStableTolerance = 0.0025f;
    private const int DefaultStableFrameCount = 2;

    public static IEnumerator WaitForCameraSettle(
        CinemachineBrain brain,
        Camera outputCamera,
        Transform target,
        float timeoutSeconds = DefaultSettleTimeoutSeconds,
        float viewportStableTolerance = DefaultViewportStableTolerance,
        int requiredStableFrames = DefaultStableFrameCount)
    {
        yield return null;

        float timeout = Mathf.Max(
            0f,
            timeoutSeconds,
            ResolveBlendFallbackSeconds(brain));
        if (timeout <= 0f)
            yield break;

        int stableFrames = 0;
        int requiredFrames = Mathf.Max(1, requiredStableFrames);
        float elapsed = 0f;
        bool hasPreviousViewportPoint = false;
        Vector3 previousViewportPoint = default;

        while (elapsed < timeout)
        {
            bool blendComplete = brain == null || !brain.IsBlending;
            Camera camera = outputCamera != null ? outputCamera : ResolveOutputCamera(brain);
            bool targetStable = target == null ||
                                camera == null ||
                                IsTargetViewportStable(
                                    camera,
                                    target,
                                    viewportStableTolerance,
                                    ref previousViewportPoint,
                                    ref hasPreviousViewportPoint);

            if (blendComplete && targetStable)
            {
                stableFrames++;
                if (stableFrames >= requiredFrames)
                    yield break;
            }
            else
            {
                stableFrames = 0;
            }

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    private static float ResolveBlendFallbackSeconds(CinemachineBrain brain)
    {
        if (brain == null)
            return 0f;

        float blendTime = brain.DefaultBlend.Time;
        return blendTime > 0f ? blendTime + 0.1f : 0f;
    }

    private static Camera ResolveOutputCamera(CinemachineBrain brain)
    {
        if (brain != null && brain.OutputCamera != null)
            return brain.OutputCamera;

        return Camera.main;
    }

    private static bool IsTargetViewportStable(
        Camera camera,
        Transform target,
        float tolerance,
        ref Vector3 previousViewportPoint,
        ref bool hasPreviousViewportPoint)
    {
        if (camera == null || target == null)
            return true;

        Vector3 viewportPoint = camera.WorldToViewportPoint(target.position);
        if (viewportPoint.z < camera.nearClipPlane)
        {
            hasPreviousViewportPoint = false;
            return false;
        }

        if (!hasPreviousViewportPoint)
        {
            previousViewportPoint = viewportPoint;
            hasPreviousViewportPoint = true;
            return false;
        }

        float resolvedTolerance = Mathf.Max(0f, tolerance);
        bool isStable = Mathf.Abs(viewportPoint.x - previousViewportPoint.x) <= resolvedTolerance &&
                        Mathf.Abs(viewportPoint.y - previousViewportPoint.y) <= resolvedTolerance;

        previousViewportPoint = viewportPoint;
        return isStable;
    }
}
