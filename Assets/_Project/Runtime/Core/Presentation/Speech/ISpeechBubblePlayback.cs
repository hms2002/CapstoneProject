using System;
using UnityEngine;

/// <summary>
/// 책임 :
/// - gameplay 코드가 월드 말풍선 출력, 진행, 숨김을 요청하는 최소 표시 계약을 제공한다.
/// - 말풍선 prefab, pool, TMP layout 같은 구체 UI 구현은 UI 계층에 남긴다.
/// </summary>
public interface ISpeechBubblePlayback
{
    Transform BubbleTransform { get; }

    void Speak(string text, float duration = 2.5f);
    void Speak(string text, float duration, SpeechBubbleThemeSettings theme);
    void Speak(string text, float duration, SpeechBubbleThemeSettings theme, Action onHidden);
    void SpeakWithOffsetDelta(string text, float duration, SpeechBubbleThemeSettings theme, Action onHidden, Vector3 offsetDelta);
    void SpeakWithPreSizedLayout(
        string text,
        float duration,
        SpeechBubbleThemeSettings theme,
        Action onHidden,
        float minTextWidth,
        float maxTextWidth,
        float minTextHeight);
    void HideActive();
    bool TryAdvanceActive();
}
