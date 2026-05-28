using CapstoneAudio;
using CapstonePresentation;
using UnityEngine;

/// <summary>
/// 책임:
/// - 슬라임 퀸 패턴 코드가 기존 오디오/프레젠테이션 인프라를 일관된 문맥으로 호출하게 돕는다.
/// - 패턴 host가 AudioSource를 직접 다루지 않도록 SoundRef와 WorldPresentationHook 실행만 캡슐화한다.
/// </summary>
internal static class SlimeQueenPresentationAudioUtility
{
    public static void PlaySound(
        in SoundRef sound,
        GameObject instigator,
        Vector3 position,
        Object sourceObject,
        GameObject target = null,
        GameObject causer = null)
    {
        if (!sound.IsSet)
            return;

        SoundPlaybackUtility.Play(
            sound,
            instigator,
            causer != null ? causer : instigator,
            target,
            position,
            sourceObject);
    }

    public static void PlayPresentation(
        in WorldPresentationHook presentation,
        GameObject instigator,
        Vector3 position,
        Object sourceObject,
        GameObject target = null,
        GameObject causer = null,
        Vector3? fallbackDirection = null)
    {
        if (!presentation.HasAnyContent)
            return;

        WorldPresentationRuntime.Play(
            presentation,
            WorldPresentationContext.AtWorld(
                instigator,
                position,
                fallbackDirection ?? Vector3.up,
                target,
                sourceObject,
                causer: causer));
    }
}
