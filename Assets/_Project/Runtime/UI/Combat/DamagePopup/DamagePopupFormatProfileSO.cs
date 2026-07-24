using System;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 피해 종류와 원소 태그를 어떤 텍스트 포맷/색/이동 연출로 보여줄지 매칭한다.
/// - 전투 로직이 presentation 세부값을 모르고 DamagePopupRequest만 전달할 수 있게 하는 설정 에셋이다.
/// </summary>
[CreateAssetMenu(fileName = "DamagePopupFormatProfile", menuName = "UI/Combat/Damage Popup Format Profile")]
public sealed class DamagePopupFormatProfileSO : ScriptableObject
{
    /// <summary>
    /// 책임 :
    /// - 하나의 피해 팝업 분류 또는 원소 태그에 대응하는 표시 스타일 값을 보관한다.
    /// - 프로필 에셋이 포맷/색/움직임/수명 설정을 인스펙터에서 조합할 수 있게 한다.
    /// </summary>
    [Serializable]
    public sealed class FormatEntry
    {
        [Tooltip("이 포맷이 대응할 피해 분류입니다.")]
        public DamagePopupKind kind = DamagePopupKind.Normal;

        [Tooltip("원소 피해일 때 특정 태그에만 대응하려면 지정합니다. 비워두면 해당 kind의 기본 포맷입니다.")]
        public GameplayTag elementTag;

        [Tooltip("표시 문자열 포맷입니다. {0}=정수 피해량, {1}=원소 태그 이름")]
        public string format = "{0}";

        public Color textColor = Color.white;
        public Vector3 moveVelocity = new Vector3(0.45f, 1.25f, 0f);
        [Min(0.05f)] public float lifetime = 0.75f;
        [Range(0f, 0.95f)] public float fadeOutRatio = 0.55f;
        [Min(0.01f)] public float startScale = 0.9f;
        [Min(0.01f)] public float endScale = 1.1f;

        [Tooltip("0 이하이면 프리팹의 기본 폰트 크기를 유지합니다.")]
        public float fontSize = 0f;
    }

    [Header("Fallbacks")]
    [SerializeField] private FormatEntry normal = new FormatEntry
    {
        kind = DamagePopupKind.Normal,
        format = "{0}",
        textColor = Color.white,
        moveVelocity = new Vector3(0.45f, 1.25f, 0f),
        lifetime = 0.75f,
        fadeOutRatio = 0.55f,
        startScale = 0.9f,
        endScale = 1.1f,
        fontSize = 0f
    };

    [SerializeField] private FormatEntry critical = new FormatEntry
    {
        kind = DamagePopupKind.Critical,
        format = "{0}!",
        textColor = new Color(1f, 0.86f, 0.22f, 1f),
        moveVelocity = new Vector3(0.55f, 1.45f, 0f),
        lifetime = 0.85f,
        fadeOutRatio = 0.6f,
        startScale = 1.15f,
        endScale = 1.35f,
        fontSize = 8.5f
    };

    [SerializeField] private FormatEntry elementFallback = new FormatEntry
    {
        kind = DamagePopupKind.Element,
        format = "{0}",
        textColor = new Color(0.75f, 0.95f, 1f, 1f),
        moveVelocity = new Vector3(0.45f, 1.2f, 0f),
        lifetime = 0.8f,
        fadeOutRatio = 0.55f,
        startScale = 0.85f,
        endScale = 1.05f,
        fontSize = 0f
    };

    [SerializeField] private FormatEntry textFallback = new FormatEntry
    {
        kind = DamagePopupKind.Text,
        format = "{0}",
        textColor = Color.white,
        moveVelocity = new Vector3(0.35f, 1.15f, 0f),
        lifetime = 0.75f,
        fadeOutRatio = 0.55f,
        startScale = 0.9f,
        endScale = 1.05f,
        fontSize = 0f
    };

    [Header("Element Overrides")]
    [SerializeField] private FormatEntry[] elementEntries;

    public DamagePopupViewModel BuildViewModel(DamagePopupRequest request)
    {
        FormatEntry entry = ResolveEntry(request);
        string content = ResolveText(request, entry);

        return new DamagePopupViewModel(
            content,
            entry != null ? entry.textColor : Color.white,
            entry != null ? entry.moveVelocity : new Vector3(0.45f, 1.25f, 0f),
            entry != null ? Mathf.Max(0.05f, entry.lifetime) : 0.75f,
            entry != null ? Mathf.Clamp01(entry.fadeOutRatio) : 0.55f,
            entry != null ? Mathf.Max(0.01f, entry.startScale) : 0.9f,
            entry != null ? Mathf.Max(0.01f, entry.endScale) : 1.1f,
            entry != null ? entry.fontSize : 0f,
            entry != null && entry.fontSize > 0f);
    }

    private FormatEntry ResolveEntry(DamagePopupRequest request)
    {
        if (request.Kind == DamagePopupKind.Element)
        {
            FormatEntry exact = FindElementEntry(request.ElementTag);
            return exact ?? elementFallback ?? normal;
        }

        if (request.IsCritical || request.Kind == DamagePopupKind.Critical)
            return critical ?? normal;

        if (request.Kind == DamagePopupKind.Text)
            return textFallback ?? normal;

        return normal;
    }

    private FormatEntry FindElementEntry(GameplayTag elementTag)
    {
        if (elementTag == null || elementEntries == null)
            return null;

        for (int i = 0; i < elementEntries.Length; i++)
        {
            FormatEntry entry = elementEntries[i];
            if (entry == null || entry.kind != DamagePopupKind.Element)
                continue;

            if (entry.elementTag == elementTag)
                return entry;
        }

        return null;
    }

    private static string ResolveText(DamagePopupRequest request, FormatEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(request.TextOverride))
            return request.TextOverride;

        int amount = Mathf.Max(1, Mathf.CeilToInt(request.Amount));
        string elementName = request.ElementTag != null ? request.ElementTag.CachedPath : string.Empty;
        string format = entry != null && !string.IsNullOrWhiteSpace(entry.format) ? entry.format : "{0}";
        try
        {
            return string.Format(format, amount, elementName);
        }
        catch (FormatException)
        {
            return amount.ToString();
        }
    }
}
