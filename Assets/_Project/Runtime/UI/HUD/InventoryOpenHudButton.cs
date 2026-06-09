using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이 클래스의 책임:
/// HUD의 인벤토리 안내 버튼 표시/클릭 가능 여부를 인벤토리 열기 요청 인터페이스에 맞춰 갱신한다.
/// 인벤토리 UI 구현체를 직접 알지 않고, 로딩/전환/중복 열림 상황에서는 안전하게 입력을 무시한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class InventoryOpenHudButton : MonoBehaviour
{
    [SerializeField] private Button button;
    [SerializeField] private MonoBehaviour openRequestHandlerSource;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private bool hideWhenUnavailable = true;

    private IInventoryOpenRequestHandler openRequestHandler;
    private CanvasGroup hudCanvasGroup;
    private Graphic[] hudGraphics;

    private void Awake()
    {
        ResolveButton();
        ResolveOpenRequestHandler();
        ResolveHudRoot();
        RefreshVisibility();
    }

    private void OnEnable()
    {
        ResolveButton();
        if (button != null)
            button.onClick.AddListener(HandleClick);

        RefreshVisibility();
    }

    private void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(HandleClick);
    }

    private void LateUpdate()
    {
        RefreshVisibility();
    }

    public GameObject GetPresentationRoot()
    {
        ResolveHudRoot();
        return hudRoot != null ? hudRoot : gameObject;
    }

    public void RefreshNow()
    {
        RefreshVisibility();
    }

    private void HandleClick()
    {
        if (!ResolveOpenRequestHandler())
            return;

        if (openRequestHandler.IsInventoryOpen || !openRequestHandler.CanOpenInventory)
            return;

        openRequestHandler.TryOpenInventory();
    }

    private void ResolveButton()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    private void ResolveHudRoot()
    {
        if (hudRoot == null)
            hudRoot = gameObject;

        if (hudCanvasGroup == null)
            hudCanvasGroup = hudRoot.GetComponent<CanvasGroup>();

        if (hudRoot == gameObject && hudCanvasGroup == null && hudGraphics == null)
            hudGraphics = hudRoot.GetComponentsInChildren<Graphic>(includeInactive: true);
    }

    /// <summary>
    /// 책임:
    /// - 기존 플레이어 HUD처럼 바인딩 가능한 플레이어/인벤토리 문맥이 없으면 안내 버튼을 숨긴다.
    /// - 버튼이 보이지 않는 동안 raycast와 interactable도 같이 내려 유령 클릭을 방지한다.
    /// </summary>
    private void RefreshVisibility()
    {
        ResolveHudRoot();

        bool canOpen = ResolveOpenRequestHandler() && openRequestHandler.CanOpenInventory;
        bool shouldShow = !hideWhenUnavailable || canOpen;
        ApplyVisible(shouldShow);

        if (button != null)
            button.interactable = canOpen;
    }

    private void ApplyVisible(bool visible)
    {
        if (hudRoot == null)
            return;

        if (hudRoot != gameObject)
        {
            if (hudRoot.activeSelf != visible)
                hudRoot.SetActive(visible);
            return;
        }

        if (hudCanvasGroup != null)
        {
            hudCanvasGroup.alpha = visible ? 1f : 0f;
            hudCanvasGroup.interactable = visible;
            hudCanvasGroup.blocksRaycasts = visible;
            return;
        }

        if (hudGraphics == null)
            hudGraphics = hudRoot.GetComponentsInChildren<Graphic>(includeInactive: true);

        for (int i = 0; i < hudGraphics.Length; i++)
        {
            if (hudGraphics[i] == null)
                continue;

            hudGraphics[i].enabled = visible;
        }
    }

    private bool ResolveOpenRequestHandler()
    {
        if (openRequestHandler != null)
            return true;

        if (openRequestHandlerSource is IInventoryOpenRequestHandler serializedHandler)
        {
            openRequestHandler = serializedHandler;
            return true;
        }

        MonoBehaviour[] behaviours = GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IInventoryOpenRequestHandler parentHandler)
            {
                openRequestHandlerSource = behaviours[i];
                openRequestHandler = parentHandler;
                return true;
            }
        }

#if UNITY_2023_1_OR_NEWER
        InventoryUIOpenRequestHandler sceneHandler = FindAnyObjectByType<InventoryUIOpenRequestHandler>(FindObjectsInactive.Include);
#else
        InventoryUIOpenRequestHandler sceneHandler = FindObjectOfType<InventoryUIOpenRequestHandler>(true);
#endif
        if (sceneHandler == null)
            return false;

        openRequestHandlerSource = sceneHandler;
        openRequestHandler = sceneHandler;
        return true;
    }
}
