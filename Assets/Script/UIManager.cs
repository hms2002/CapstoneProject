using UnityEngine;
using System.Collections.Generic;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Controllers")]
    [SerializeField] private HoverUIController hoverUIController;

    [Header("UI Stack (Popups)")]
    private List<IStackableUI> uiStack = new List<IStackableUI>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else Destroy(gameObject);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape)) CloseTopUI();
    }

    // =========================================================
    // 1. 팝업 UI (Stackable) 관리
    // =========================================================
    public void PushUI(IStackableUI ui)
    {
        if (ui == null) return;

        if (uiStack.Contains(ui)) uiStack.Remove(ui);

        uiStack.Add(ui);
        ui.OpenUI();
    }

    public void PopUI(IStackableUI ui)
    {
        if (ui == null) return;

        if (uiStack.Contains(ui))
        {
            uiStack.Remove(ui);
            ui.CloseUI();

            // 팝업이 닫힐 때 허공에 떠 있는 Hover UI(툴팁) 강제 정리
            HideHoverImmediate();
        }
    }

    public void CloseTopUI()
    {
        if (uiStack.Count > 0)
        {
            IStackableUI topUI = uiStack[uiStack.Count - 1];
            if (topUI.CanCloseOnEscape) PopUI(topUI);
        }
    }

    public bool HasActivePopup() => uiStack.Count > 0;

    // =========================================================
    // 2. 호버 UI (Hover/Tooltip) 라우팅
    // =========================================================
    public void ShowHover(IHoverView view, RectTransform targetRect, object data, object context = null)
    {
        if (hoverUIController != null)
            hoverUIController.ShowHover(view, targetRect, data, context);
    }

    // [수정] 끄기 지시를 내릴 때 타겟 Rect도 같이 넘겨서 꼬임 방지
    public void HideHover(IHoverView view, RectTransform targetRect)
    {
        if (hoverUIController != null)
            hoverUIController.HideHover(view, targetRect);
    }

    public void HideHoverImmediate()
    {
        if (hoverUIController != null)
            hoverUIController.HideImmediate();
    }
}