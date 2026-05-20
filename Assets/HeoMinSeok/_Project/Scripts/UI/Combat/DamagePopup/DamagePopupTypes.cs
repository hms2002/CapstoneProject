using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 전투 피해 팝업이 표현해야 하는 의미 분류를 정의한다.
/// - 전투 계층의 피해 의미와 UI 계층의 포맷 선택을 느슨하게 연결하는 키 역할을 한다.
/// </summary>
public enum DamagePopupKind
{
    Normal = 0,
    Element = 1,
    Critical = 2,
    Text = 3
}

/// <summary>
/// 책임 :
/// - 피해 팝업 하나를 생성하는 데 필요한 전투 의미와 월드 위치를 전달한다.
/// - DamagePopupService가 포맷 프로필을 적용하기 전의 원본 요청 모델로 사용된다.
/// </summary>
public readonly struct DamagePopupRequest
{
    public readonly float Amount;
    public readonly Vector3 WorldPosition;
    public readonly DamagePopupKind Kind;
    public readonly GameplayTag ElementTag;
    public readonly bool IsCritical;
    public readonly string TextOverride;

    public DamagePopupRequest(
        float amount,
        Vector3 worldPosition,
        DamagePopupKind kind = DamagePopupKind.Normal,
        GameplayTag elementTag = null,
        bool isCritical = false,
        string textOverride = null)
    {
        Amount = amount;
        WorldPosition = worldPosition;
        Kind = kind;
        ElementTag = elementTag;
        IsCritical = isCritical;
        TextOverride = textOverride;
    }

    public static DamagePopupRequest Damage(
        float amount,
        Vector3 worldPosition,
        bool isCritical = false)
    {
        return new DamagePopupRequest(
            amount,
            worldPosition,
            isCritical ? DamagePopupKind.Critical : DamagePopupKind.Normal,
            elementTag: null,
            isCritical: isCritical);
    }

    public static DamagePopupRequest Element(
        float amount,
        Vector3 worldPosition,
        GameplayTag elementTag)
    {
        return new DamagePopupRequest(
            amount,
            worldPosition,
            DamagePopupKind.Element,
            elementTag,
            isCritical: false);
    }

    public static DamagePopupRequest Text(string text, Vector3 worldPosition)
    {
        return new DamagePopupRequest(
            0f,
            worldPosition,
            DamagePopupKind.Text,
            elementTag: null,
            isCritical: false,
            textOverride: text);
    }
}

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
