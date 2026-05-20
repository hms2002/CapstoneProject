using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 : 데미지 팝업 생성 요청을 받아 월드 좌표에 실제 팝업 프리팹을 생성한다.
/// 게임 로직이 팝업 프리팹 참조나 생성 규칙을 직접 알지 않도록 전역 진입점 역할을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DamagePopupService : MonoBehaviour
{
    private const string DefaultFormatProfileResourcePath = "DamagePopup/DamagePopupFormatProfile_Default";

    public static DamagePopupService Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private DamagePopupWorldText popupPrefab;

    [Header("Format")]
    [Tooltip("비워두면 Resources/DamagePopup/DamagePopupFormatProfile_Default를 자동 사용합니다.")]
    [SerializeField] private DamagePopupFormatProfileSO formatProfile;

    [Header("Spawn")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.8f, 0f);

    [Tooltip("월드 위치 기준 랜덤 오프셋 범위")]
    [SerializeField] private Vector2 randomOffsetX = new Vector2(-0.15f, 0.15f);

    [SerializeField] private Vector2 randomOffsetY = new Vector2(-0.05f, 0.05f);

    [Header("Optional Parent")]
    [Tooltip("비우면 생성된 팝업은 루트에 배치된다.")]
    [SerializeField] private Transform popupParent;

    private bool hasWarnedInvalidPopupParent;
    private DamagePopupFormatProfileSO runtimeFormatProfile;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        ResolveFormatProfile();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (popupParent != null && popupParent.GetComponentInParent<Canvas>() != null)
            popupParent = null;
    }
#endif

    /// <summary>
    /// 책임 : 외부 게임 로직이 간단히 호출할 수 있는 전역 정적 진입점이다.
    /// 서비스 인스턴스가 존재할 때만 실제 팝업 생성을 위임한다.
    /// </summary>
    public static void Show(float amount, Vector3 worldPosition)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[DamagePopupService] Instance가 없습니다. 씬 시작 전에 서비스가 생성되어야 합니다.");
            return;
        }

        Instance.ShowInternal(amount, worldPosition);
    }

    public static void Show(DamagePopupRequest request)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[DamagePopupService] Instance가 없습니다. 씬 시작 전에 서비스가 생성되어야 합니다.");
            return;
        }

        Instance.ShowInternal(request);
    }

    public static void ShowText(string content, Vector3 worldPosition)
    {
        if (Instance == null)
        {
            Debug.LogWarning("[DamagePopupService] Instance가 없습니다. 씬 시작 전에 서비스가 생성되어야 합니다.");
            return;
        }

        Instance.ShowInternal(DamagePopupRequest.Text(content, worldPosition));
    }

    /// <summary>
    /// 책임 : 월드 좌표를 기준으로 데미지 팝업 프리팹을 생성하고 초기화한다.
    /// </summary>
    private void ShowInternal(float amount, Vector3 worldPosition)
    {
        ShowInternal(DamagePopupRequest.Damage(amount, worldPosition));
    }

    private void ShowInternal(DamagePopupRequest request)
    {
        if (popupPrefab == null)
            return;

        Vector3 spawnPos = request.WorldPosition + worldOffset;
        spawnPos.x += Random.Range(randomOffsetX.x, randomOffsetX.y);
        spawnPos.y += Random.Range(randomOffsetY.x, randomOffsetY.y);

        Transform resolvedParent = ResolvePopupParent();
        DamagePopupWorldText instance = Instantiate(popupPrefab, spawnPos, Quaternion.identity, resolvedParent);
        DamagePopupFormatProfileSO profile = ResolveFormatProfile();
        if (profile != null)
            instance.Setup(profile.BuildViewModel(request));
        else if (!string.IsNullOrWhiteSpace(request.TextOverride))
            instance.Setup(request.TextOverride);
        else
            instance.Setup(Mathf.Max(1, Mathf.CeilToInt(request.Amount)));
    }

    private DamagePopupFormatProfileSO ResolveFormatProfile()
    {
        if (formatProfile != null)
            return formatProfile;

        if (runtimeFormatProfile == null)
            runtimeFormatProfile = Resources.Load<DamagePopupFormatProfileSO>(DefaultFormatProfileResourcePath);

        return runtimeFormatProfile;
    }

    private Transform ResolvePopupParent()
    {
        if (popupParent == null)
            return null;

        if (popupParent.GetComponentInParent<Canvas>() == null)
            return popupParent;

        if (!hasWarnedInvalidPopupParent)
        {
            Debug.LogWarning(
                "[DamagePopupService] popupParent is under a Canvas. World-space damage popups ignore Canvas parents and spawn without it.",
                this);
            hasWarnedInvalidPopupParent = true;
        }

        return null;
    }
}
