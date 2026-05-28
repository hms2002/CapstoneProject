using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임 : 호감도 수치 변화와 부정 선택지 연출에서 사용하는 공통 피드백 사운드를 중복 없이 재생한다.
/// </summary>
public static class AffectionFeedbackSoundPlayer
{
    private static readonly SoundRef AffectionUpSound = SoundRef.FromKey("sound_ui_affectionup");
    private static readonly SoundRef AffectionDownSound = SoundRef.FromKey("sound_ui_affectiondown");

    private const float DuplicateWindowSeconds = 0.08f;

    private static float lastUpSoundTime = float.NegativeInfinity;
    private static float lastDownSoundTime = float.NegativeInfinity;

    public static void PlayChange(int delta)
    {
        if (delta > 0)
        {
            PlayUp();
            return;
        }

        if (delta < 0)
            PlayDown();
    }

    public static void PlayDown()
    {
        if (IsDuplicate(ref lastDownSoundTime))
            return;

        SoundPlaybackUtility.Play(AffectionDownSound);
    }

    private static void PlayUp()
    {
        if (IsDuplicate(ref lastUpSoundTime))
            return;

        SoundPlaybackUtility.Play(AffectionUpSound);
    }

    private static bool IsDuplicate(ref float lastPlayedTime)
    {
        float now = Time.unscaledTime;
        if (now - lastPlayedTime < DuplicateWindowSeconds)
            return true;

        lastPlayedTime = now;
        return false;
    }
}
