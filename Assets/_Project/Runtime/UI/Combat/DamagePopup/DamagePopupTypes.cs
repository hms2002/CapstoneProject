using UnityEngine;
/// <summary>
/// 책임 :
/// - DamagePopupFormatProfileSO가 선택한 최종 텍스트/색/움직임 연출 값을 WorldText 프리팹에 전달한다.
/// - 팝업 프리팹이 포맷 선택 규칙을 직접 알지 않게 하는 표시 전용 모델이다.
/// </summary>
public readonly struct DamagePopupViewModel
{
    public readonly string Text;
    public readonly Color TextColor;
    public readonly Vector3 MoveVelocity;
    public readonly float Lifetime;
    public readonly float FadeOutRatio;
    public readonly float StartScale;
    public readonly float EndScale;
    public readonly float FontSize;
    public readonly bool OverrideFontSize;

    public DamagePopupViewModel(
        string text,
        Color textColor,
        Vector3 moveVelocity,
        float lifetime,
        float fadeOutRatio,
        float startScale,
        float endScale,
        float fontSize,
        bool overrideFontSize)
    {
        Text = text;
        TextColor = textColor;
        MoveVelocity = moveVelocity;
        Lifetime = lifetime;
        FadeOutRatio = fadeOutRatio;
        StartScale = startScale;
        EndScale = endScale;
        FontSize = fontSize;
        OverrideFontSize = overrideFontSize;
    }
}
