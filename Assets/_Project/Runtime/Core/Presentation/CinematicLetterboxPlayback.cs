using System.Collections;
using System.Collections.Generic;

/// <summary>
/// 책임 : 컷씬 코드가 구체 레터박스 UI 구현 없이 레터박스 연출을 제어하게 하는 handle 계약이다.
/// </summary>
public interface ICinematicLetterboxOverlayHandle
{
    IEnumerator PlayIn(float duration, float letterboxHeightRatio, float uiTargetAlpha);
    IEnumerator PlayIn(float duration, float letterboxHeightRatio, float uiTargetAlpha, bool captureGlobalUiLayers);
    IEnumerator PlayIn(float duration, float letterboxHeightRatio, float uiTargetAlpha, IReadOnlyList<GlobalCanvasLayer> fadedLayers);
    IEnumerator PlayOut(float duration);
    void Dispose();
}

/// <summary>
/// 책임 : Core/Gameplay 계층이 구체 레터박스 UI 타입 없이 레터박스 handle을 생성하게 하는 backend 계약이다.
/// </summary>
public interface ICinematicLetterboxBackend
{
    ICinematicLetterboxOverlayHandle CreateOverlay();
}

/// <summary>
/// 책임 : 컷씬 호출자가 현재 등록된 UI backend를 통해 레터박스 handle을 생성하게 한다.
/// </summary>
public static class CinematicLetterboxPlayback
{
    private static readonly ICinematicLetterboxOverlayHandle NullHandle = new NullCinematicLetterboxOverlayHandle();
    private static ICinematicLetterboxBackend backend;

    public static void RegisterBackend(ICinematicLetterboxBackend letterboxBackend)
    {
        backend = letterboxBackend;
    }

    public static ICinematicLetterboxOverlayHandle CreateOverlay()
    {
        return backend != null ? backend.CreateOverlay() ?? NullHandle : NullHandle;
    }

    /// <summary>
    /// 책임 : 레터박스 backend가 없을 때 컷씬 코루틴 흐름을 깨지 않는 no-op handle을 제공한다.
    /// </summary>
    private sealed class NullCinematicLetterboxOverlayHandle : ICinematicLetterboxOverlayHandle
    {
        public IEnumerator PlayIn(float duration, float letterboxHeightRatio, float uiTargetAlpha)
        {
            yield break;
        }

        public IEnumerator PlayIn(float duration, float letterboxHeightRatio, float uiTargetAlpha, bool captureGlobalUiLayers)
        {
            yield break;
        }

        public IEnumerator PlayIn(
            float duration,
            float letterboxHeightRatio,
            float uiTargetAlpha,
            IReadOnlyList<GlobalCanvasLayer> fadedLayers)
        {
            yield break;
        }

        public IEnumerator PlayOut(float duration)
        {
            yield break;
        }

        public void Dispose()
        {
        }
    }
}
