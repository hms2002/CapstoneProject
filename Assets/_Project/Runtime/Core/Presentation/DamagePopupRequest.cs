using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임: 전투 피해 팝업이 표현해야 하는 의미 분류를 정의해 Core 전투 의미와 UI 포맷 선택을 느슨하게 연결한다.
/// </summary>
public enum DamagePopupKind
{
    Normal = 0,
    Element = 1,
    Critical = 2,
    Text = 3
}

/// <summary>
/// 책임: Core 전투 로직이 구체 데미지 팝업 UI 없이 팝업 의미, 수치, 월드 위치를 전달하는 요청 값 타입이다.
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
/// 책임: Core 데미지 팝업 요청을 실제 UI 표시 구현으로 넘기는 최소 backend 계약이다.
/// </summary>
public interface IDamagePopupBackend
{
    void Show(DamagePopupRequest request);
}

/// <summary>
/// 책임: Core 전투 코드가 구체 DamagePopupService 구현 없이 데미지 팝업 표시를 요청하게 한다.
/// </summary>
public static class DamagePopupPlayback
{
    private static IDamagePopupBackend backend;

    public static void RegisterBackend(IDamagePopupBackend damagePopupBackend)
    {
        backend = damagePopupBackend;
    }

    public static void Show(DamagePopupRequest request)
    {
        backend?.Show(request);
    }

    public static void ShowText(string text, Vector3 worldPosition)
    {
        Show(DamagePopupRequest.Text(text, worldPosition));
    }
}
