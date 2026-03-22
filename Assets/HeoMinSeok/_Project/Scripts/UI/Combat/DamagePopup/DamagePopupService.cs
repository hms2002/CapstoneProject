using UnityEngine;

/// <summary>
/// 책임 : 데미지 팝업 생성 요청을 받아 현재 활성 씬의 팝업 컨텍스트에 실제 팝업을 생성한다.
/// 게임 로직이 Canvas / Camera / 씬별 UI 구조를 몰라도 되도록 전역 진입점 역할을 담당한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DamagePopupService : MonoBehaviour
{
    public static DamagePopupService Instance { get; private set; }

    [Header("Prefab")]
    [SerializeField] private DamagePopupUI popupPrefab;

    [Header("Default Spawn Options")]
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 0.8f, 0f);
    [SerializeField] private float randomX = 25f;
    [SerializeField] private float randomY = 10f;

    private DamagePopupSceneAnchor currentAnchor;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    /// <summary>
    /// 책임 : 현재 씬의 팝업 표시 컨텍스트를 등록한다.
    /// 가장 최근에 활성화된 유효 앵커를 현재 컨텍스트로 채택한다.
    /// </summary>
    public void RegisterAnchor(DamagePopupSceneAnchor anchor)
    {
        if (anchor == null || !anchor.IsValid())
            return;

        currentAnchor = anchor;
    }

    /// <summary>
    /// 책임 : 현재 사용 중인 팝업 컨텍스트가 비활성화될 때 참조를 정리한다.
    /// 다른 씬 앵커가 이미 등록된 경우에는 건드리지 않는다.
    /// </summary>
    public void UnregisterAnchor(DamagePopupSceneAnchor anchor)
    {
        if (anchor == null)
            return;

        if (currentAnchor == anchor)
            currentAnchor = null;
    }

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

    /// <summary>
    /// 책임 : 현재 등록된 씬 컨텍스트를 사용해 월드 좌표를 UI 좌표로 변환하고 팝업을 생성한다.
    /// </summary>
    private void ShowInternal(float amount, Vector3 worldPosition)
    {
        if (popupPrefab == null)
            return;

        if (!TryGetUsableContext(out RectTransform canvasRoot, out Camera worldCamera))
            return;

        int damageInt = Mathf.Max(1, Mathf.CeilToInt(amount));

        Vector3 screenPos = worldCamera.WorldToScreenPoint(worldPosition + worldOffset);
        if (screenPos.z < 0f)
            return;

        Camera uiCamera = GetCanvasEventCamera(canvasRoot);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRoot,
                screenPos,
                uiCamera,
                out Vector2 localPoint))
            return;

        localPoint += new Vector2(
            Random.Range(-randomX, randomX),
            Random.Range(-randomY, randomY));

        DamagePopupUI instance = Instantiate(popupPrefab, canvasRoot);
        instance.Setup(damageInt, localPoint);
    }

    /// <summary>
    /// 책임 : 현재 등록된 앵커가 실제로 사용할 수 있는 상태인지 검사하고,
    /// 필요 시 씬 안에서 다시 탐색해 복구한다.
    /// </summary>
    private bool TryGetUsableContext(out RectTransform canvasRoot, out Camera worldCamera)
    {
        if (!IsAnchorUsable(currentAnchor))
            currentAnchor = FindFallbackAnchor();

        if (IsAnchorUsable(currentAnchor))
        {
            canvasRoot = currentAnchor.PopupRoot;
            worldCamera = currentAnchor.WorldCamera;
            return true;
        }

        canvasRoot = null;
        worldCamera = null;
        return false;
    }

    /// <summary>
    /// 책임 : 앵커가 null이 아니고, 활성 상태이며, 씬에 남아 있고, 필수 참조가 유효한지 검사한다.
    /// </summary>
    private static bool IsAnchorUsable(DamagePopupSceneAnchor anchor)
    {
        return anchor != null
            && anchor.isActiveAndEnabled
            && anchor.gameObject.scene.isLoaded
            && anchor.IsValid();
    }

    /// <summary>
    /// 책임 : 등록 타이밍이 어긋났을 때를 대비해 현재 로드된 씬들에서 사용 가능한 앵커를 보조적으로 탐색한다.
    /// 정상 동작의 주 경로는 RegisterAnchor 이며, 이 함수는 예외 복구용이다.
    /// </summary>
    private static DamagePopupSceneAnchor FindFallbackAnchor()
    {
#if UNITY_2023_1_OR_NEWER
        DamagePopupSceneAnchor[] anchors = Object.FindObjectsByType<DamagePopupSceneAnchor>(FindObjectsSortMode.None);
#else
        DamagePopupSceneAnchor[] anchors = Object.FindObjectsOfType<DamagePopupSceneAnchor>();
#endif

        for (int i = 0; i < anchors.Length; i++)
        {
            if (IsAnchorUsable(anchors[i]))
                return anchors[i];
        }

        return null;
    }

    /// <summary>
    /// 책임 : Canvas의 렌더 모드에 맞춰 ScreenPoint 변환에 사용할 UI 카메라를 반환한다.
    /// Overlay Canvas라면 null을 반환한다.
    /// </summary>
    private static Camera GetCanvasEventCamera(RectTransform canvasRoot)
    {
        Canvas canvas = canvasRoot.GetComponentInParent<Canvas>();
        if (canvas == null)
            return null;

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            return null;

        return canvas.worldCamera;
    }
}