using System.Collections.Generic;
using UnityEngine;

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

    [Tooltip("데미지 팝업 요청/프로필/최종 색상 흐름을 콘솔에 출력합니다.")]
    [SerializeField] private bool logPopupRequests = true;

    [Header("Spawn")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.45f, 0f);

    [Tooltip("선택된 슬롯 안에서 규칙성을 숨기기 위한 랜덤 오프셋 범위")]
    [SerializeField] private Vector2 randomOffsetX = new Vector2(-0.22f, 0.22f);

    [SerializeField] private Vector2 randomOffsetY = new Vector2(-0.12f, 0.12f);

    [Header("Spawn De-overlap")]
    [Tooltip("중앙 포함 9방향 슬롯을 사용해 같은 위치의 데미지 텍스트가 겹치지 않게 배치합니다.")]
    [SerializeField] private bool useTetrisPlacement = true;

    [Tooltip("9방향 슬롯 사이의 월드 간격입니다.")]
    [SerializeField] private Vector2 slotSpacing = new Vector2(0.5f, 0.36f);

    [Tooltip("이미 예약된 팝업 위치와 이 거리보다 가까우면 겹친 것으로 봅니다.")]
    [SerializeField] private Vector2 minSeparation = new Vector2(0.4f, 0.28f);

    [Tooltip("프로필 수명보다 짧게 점유를 유지하고 싶을 때 사용하는 상한입니다.")]
    [SerializeField] private float maxReservationSeconds = 0.75f;

    [Header("Optional Parent")]
    [Tooltip("비우면 생성된 팝업은 루트에 배치된다.")]
    [SerializeField] private Transform popupParent;

    private bool hasWarnedInvalidPopupParent;
    private bool hasWarnedMissingFormatProfile;
    private DamagePopupFormatProfileSO runtimeFormatProfile;
    private readonly List<SlotReservation> slotReservations = new();
    private static readonly Vector2Int[] SlotDirections =
    {
        new Vector2Int(0, 0),
        new Vector2Int(1, 0),
        new Vector2Int(-1, 0),
        new Vector2Int(0, -1),
        new Vector2Int(0, 1),
        new Vector2Int(1, -1),
        new Vector2Int(-1, -1),
        new Vector2Int(1, 1),
        new Vector2Int(-1, 1)
    };

    /// <summary>
    /// 책임 :
    /// - 최근 생성된 데미지 팝업이 차지한 월드 위치와 만료 시각을 보관한다.
    /// - 다음 팝업이 중앙 포함 9방향 슬롯 중 겹치지 않는 위치를 고를 수 있게 한다.
    /// </summary>
    private readonly struct SlotReservation
    {
        public readonly Vector3 Position;
        public readonly float ExpireTime;

        public SlotReservation(Vector3 position, float expireTime)
        {
            Position = position;
            ExpireTime = expireTime;
        }
    }

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
        LogPopupServiceLifecycle("awake and registered as Instance");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (popupParent != null && popupParent.GetComponentInParent<Canvas>() != null)
            popupParent = null;

        slotSpacing.x = Mathf.Max(0.01f, slotSpacing.x);
        slotSpacing.y = Mathf.Max(0.01f, slotSpacing.y);
        minSeparation.x = Mathf.Max(0.01f, minSeparation.x);
        minSeparation.y = Mathf.Max(0.01f, minSeparation.y);
        maxReservationSeconds = Mathf.Max(0.05f, maxReservationSeconds);
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

        DamagePopupFormatProfileSO profile = ResolveFormatProfile();
        DamagePopupViewModel viewModel = profile != null ? profile.BuildViewModel(request) : default;
        float reservationSeconds = profile != null
            ? Mathf.Min(Mathf.Max(0.05f, viewModel.Lifetime), Mathf.Max(0.05f, maxReservationSeconds))
            : Mathf.Max(0.05f, maxReservationSeconds);

        Vector3 spawnPos = ResolveSpawnPosition(request.WorldPosition + worldOffset, reservationSeconds);

        Transform resolvedParent = ResolvePopupParent();
        DamagePopupWorldText instance = Instantiate(popupPrefab, spawnPos, Quaternion.identity, resolvedParent);
        if (profile != null)
        {
            LogPopupRequest(request, viewModel, profileSource: profile.name);
            instance.Setup(viewModel);
        }
        else
        {
            DamagePopupViewModel fallbackViewModel = BuildFallbackViewModel(request);
            LogPopupRequest(request, fallbackViewModel, profileSource: "code fallback");
            instance.Setup(fallbackViewModel);
        }
    }

    private void LogPopupRequest(DamagePopupRequest request, DamagePopupViewModel viewModel, string profileSource)
    {
        if (!logPopupRequests)
            return;

        string elementName = request.ElementTag != null ? request.ElementTag.Path : "none";
        Debug.Log(
            $"[DamagePopupService] show kind={request.Kind}, amount={request.Amount:0.###}, critical={request.IsCritical}, element={elementName}, profile={profileSource}, text='{viewModel.Text}', color={viewModel.TextColor}, scale={viewModel.StartScale:0.###}->{viewModel.EndScale:0.###}",
            this);
    }

    private void LogPopupServiceLifecycle(string message)
    {
        if (!logPopupRequests)
            return;

        Debug.Log($"[DamagePopupService] {message}. scene={gameObject.scene.name}, object={name}", this);
    }

    private Vector3 ResolveSpawnPosition(Vector3 basePosition, float reservationSeconds)
    {
        CleanupExpiredReservations();

        Vector3 spawnPos = useTetrisPlacement
            ? ResolveTetrisSlotPosition(basePosition)
            : basePosition;

        spawnPos.x += Random.Range(randomOffsetX.x, randomOffsetX.y);
        spawnPos.y += Random.Range(randomOffsetY.x, randomOffsetY.y);

        slotReservations.Add(new SlotReservation(
            spawnPos,
            Time.time + Mathf.Max(0.05f, reservationSeconds)));

        return spawnPos;
    }

    private Vector3 ResolveTetrisSlotPosition(Vector3 basePosition)
    {
        int bestIndex = 0;
        float bestScore = float.MaxValue;

        for (int i = 0; i < SlotDirections.Length; i++)
        {
            Vector3 candidate = basePosition + new Vector3(
                SlotDirections[i].x * slotSpacing.x,
                SlotDirections[i].y * slotSpacing.y,
                0f);

            float score = CalculateOverlapScore(candidate);
            if (score <= 0f)
                return candidate;

            if (score < bestScore)
            {
                bestScore = score;
                bestIndex = i;
            }
        }

        Vector2Int fallbackDirection = SlotDirections[bestIndex];
        return basePosition + new Vector3(
            fallbackDirection.x * slotSpacing.x,
            fallbackDirection.y * slotSpacing.y,
            0f);
    }

    private float CalculateOverlapScore(Vector3 candidate)
    {
        float score = 0f;

        for (int i = 0; i < slotReservations.Count; i++)
        {
            Vector3 delta = candidate - slotReservations[i].Position;
            float normalizedX = Mathf.Abs(delta.x) / Mathf.Max(0.01f, minSeparation.x);
            float normalizedY = Mathf.Abs(delta.y) / Mathf.Max(0.01f, minSeparation.y);

            if (normalizedX >= 1f || normalizedY >= 1f)
                continue;

            score += (1f - normalizedX) + (1f - normalizedY);
        }

        return score;
    }

    private void CleanupExpiredReservations()
    {
        float now = Time.time;
        for (int i = slotReservations.Count - 1; i >= 0; i--)
        {
            if (slotReservations[i].ExpireTime <= now)
                slotReservations.RemoveAt(i);
        }
    }

    private DamagePopupFormatProfileSO ResolveFormatProfile()
    {
        if (formatProfile != null)
            return formatProfile;

        if (runtimeFormatProfile == null)
        {
            runtimeFormatProfile = Resources.Load<DamagePopupFormatProfileSO>(DefaultFormatProfileResourcePath);

            if (runtimeFormatProfile == null && !hasWarnedMissingFormatProfile)
            {
                Debug.LogWarning(
                    $"[DamagePopupService] Damage popup format profile not found at Resources/{DefaultFormatProfileResourcePath}. Using code fallback styles.",
                    this);
                hasWarnedMissingFormatProfile = true;
            }
        }

        return runtimeFormatProfile;
    }

    private static DamagePopupViewModel BuildFallbackViewModel(DamagePopupRequest request)
    {
        string content = !string.IsNullOrWhiteSpace(request.TextOverride)
            ? request.TextOverride
            : Mathf.Max(1, Mathf.CeilToInt(request.Amount)).ToString();

        Color color = ResolveFallbackColor(request);
        Vector3 moveVelocity = request.Kind == DamagePopupKind.Critical
            ? new Vector3(0.58f, 1.48f, 0f)
            : new Vector3(0.45f, 1.25f, 0f);

        float startScale = request.Kind == DamagePopupKind.Critical ? 0.58f : 0.52f;
        float endScale = request.Kind == DamagePopupKind.Critical ? 0.7f : 0.64f;

        if (request.Kind == DamagePopupKind.Element)
        {
            startScale = 0.54f;
            endScale = 0.7f;
        }

        return new DamagePopupViewModel(
            content,
            color,
            moveVelocity,
            request.Kind == DamagePopupKind.Critical ? 0.85f : 0.78f,
            0.55f,
            startScale,
            endScale,
            fontSize: 0f,
            overrideFontSize: false);
    }

    private static Color ResolveFallbackColor(DamagePopupRequest request)
    {
        if (request.Kind == DamagePopupKind.Critical || request.IsCritical)
            return new Color(1f, 0.86f, 0.22f, 1f);

        if (request.Kind != DamagePopupKind.Element || request.ElementTag == null)
            return Color.white;

        string elementName = request.ElementTag.Name;
        if (string.Equals(elementName, "Electric", System.StringComparison.OrdinalIgnoreCase))
            return new Color(1f, 0.92f, 0.16f, 1f);

        if (string.Equals(elementName, "Blood", System.StringComparison.OrdinalIgnoreCase))
            return new Color(1f, 0.22f, 0.28f, 1f);

        if (string.Equals(elementName, "Fire", System.StringComparison.OrdinalIgnoreCase))
            return new Color(1f, 0.48f, 0.08f, 1f);

        if (string.Equals(elementName, "Poison", System.StringComparison.OrdinalIgnoreCase))
            return new Color(0.55f, 1f, 0.32f, 1f);

        return new Color(0.72f, 0.92f, 1f, 1f);
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
