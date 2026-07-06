using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 전역 UI 캔버스와 UI 서비스 루트를 보관하고 씬 전환 후에도 유지한다.
/// - UI 기능들이 이름/계층 의존을 직접 갖지 않도록 공통 캔버스 탐색 API를 제공한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class GlobalUIRoot : MonoBehaviour, IGlobalCanvasBackend, IGlobalCanvasRootMarker, ITitleScenePersistentCleanupTarget
{
    public static GlobalUIRoot Instance { get; private set; }

    [Header("Hierarchy")]
    [SerializeField] private Transform servicesRoot;

    [Header("Canvases")]
    [SerializeField] private Canvas gameplayHudCanvas;
    [SerializeField] private Canvas dialogueCanvas;
    [SerializeField] private Canvas popupCanvas;
    [SerializeField] private Canvas hoverCanvas;
    [SerializeField] private Canvas promptCanvas;
    [SerializeField] private Canvas rewardCanvas;
    [SerializeField] private Canvas damagePopupCanvas;
    [SerializeField] private Canvas bossHudCanvas;
    [SerializeField] private Canvas gameOverCanvas;
    [SerializeField] private Canvas loadingCanvas;

    [Header("Status UI")]
    [SerializeField] private StatusHudPresenter statusHudPresenterPrefab;
    [SerializeField] private StatusHudTooltipView statusTooltipPrefab;

    private readonly Dictionary<GlobalCanvasLayer, Canvas> canvases = new();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GlobalCanvasPlayback.RegisterBackend(this);
        DontDestroyOnLoad(gameObject);
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            GlobalCanvasPlayback.RegisterBackend(null);

        if (Instance == this)
            Instance = null;
    }

    public static void AdoptService(Transform target)
    {
        if (target == null)
            return;

        if (!TryResolveInstance(out var root))
            return;

        target.SetParent(root.servicesRoot != null ? root.servicesRoot : root.transform, true);
    }

    public static void AdoptToCanvas(GlobalCanvasLayer layer, Transform target, bool worldPositionStays = false)
    {
        if (target == null)
            return;

        if (!TryResolveInstance(out var root))
            return;

        Canvas canvas = root.GetCanvasInternal(layer);
        if (canvas == null)
            return;

        target.SetParent(canvas.transform, worldPositionStays);
    }

    public static Canvas GetCanvas(GlobalCanvasLayer layer)
    {
        return TryResolveInstance(out var root) ? root.GetCanvasInternal(layer) : null;
    }

    Canvas IGlobalCanvasBackend.GetCanvas(GlobalCanvasLayer layer)
    {
        return GetCanvasInternal(layer);
    }

    void IGlobalCanvasBackend.AdoptService(Transform target)
    {
        GlobalUIRoot.AdoptService(target);
    }

    public T[] GetComponentsInRoot<T>(bool includeInactive)
        where T : Component
    {
        return GetComponentsInChildren<T>(includeInactive);
    }

    /// <summary>
    /// 책임 :
    /// - 상태 HUD 표시용 presenter 프리팹 참조를 UI composition root에서 제공한다.
    /// - 상태 HUD 초기화 계층이 Resources 경로 문자열 대신 명시적 UI 자산 참조를 사용하게 만든다.
    /// </summary>
    public static StatusHudPresenter GetStatusHudPresenterPrefab()
    {
        return TryResolveInstance(out var root) ? root.statusHudPresenterPrefab : null;
    }

    /// <summary>
    /// 책임 :
    /// - 상태 HUD hover 상세 정보를 그릴 tooltip 프리팹 참조를 UI composition root에서 제공한다.
    /// - 상태 툴팁이 HUD와 분리된 hover 계층 자산으로 관리되게 만든다.
    /// </summary>
    public static StatusHudTooltipView GetStatusTooltipPrefab()
    {
        return TryResolveInstance(out var root) ? root.statusTooltipPrefab : null;
    }

    private static bool TryResolveInstance(out GlobalUIRoot root)
    {
        if (Instance != null)
        {
            root = Instance;
            return true;
        }

        var existing = Object.FindFirstObjectByType<GlobalUIRoot>();
        if (existing != null)
        {
            Instance = existing;
            existing.ResolveReferences();
            root = existing;
            return true;
        }

        root = null;
        return false;
    }

    private void ResolveReferences()
    {
        servicesRoot ??= FindChildTransform("Services");
        if (servicesRoot == null)
            servicesRoot = transform;

        gameplayHudCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.GameplayHUD));
        dialogueCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.Dialogue));
        popupCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.Popup));
        hoverCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.Hover));
        promptCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.Prompt)) ??
                          FindChildCanvas("PromptLayout") ??
                          ResolvePromptCanvasFromController();
        rewardCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.Reward));
        damagePopupCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.DamagePopup));
        bossHudCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.BossHUD));
        gameOverCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.GameOver));
        loadingCanvas ??= FindChildCanvas(GetCanvasName(GlobalCanvasLayer.Loading));

        canvases.Clear();
        RegisterCanvas(GlobalCanvasLayer.GameplayHUD, gameplayHudCanvas);
        RegisterCanvas(GlobalCanvasLayer.Dialogue, dialogueCanvas);
        RegisterCanvas(GlobalCanvasLayer.Popup, popupCanvas);
        RegisterCanvas(GlobalCanvasLayer.Hover, hoverCanvas);
        RegisterCanvas(GlobalCanvasLayer.Prompt, promptCanvas);
        RegisterCanvas(GlobalCanvasLayer.Reward, rewardCanvas);
        RegisterCanvas(GlobalCanvasLayer.DamagePopup, damagePopupCanvas);
        RegisterCanvas(GlobalCanvasLayer.BossHUD, bossHudCanvas);
        RegisterCanvas(GlobalCanvasLayer.GameOver, gameOverCanvas);
        RegisterCanvas(GlobalCanvasLayer.Loading, loadingCanvas);
    }

    private void RegisterCanvas(GlobalCanvasLayer layer, Canvas canvas)
    {
        if (canvas != null)
            canvases[layer] = canvas;
    }

    private Canvas GetCanvasInternal(GlobalCanvasLayer layer)
    {
        ResolveReferences();
        return canvases.TryGetValue(layer, out Canvas canvas) ? canvas : null;
    }

    private Transform FindChildTransform(string childName)
    {
        Transform child = transform.Find(childName);
        if (child == null)
        {
            var children = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < children.Length; i++)
            {
                if (children[i] != null && children[i].name == childName)
                    return children[i];
            }
        }

        return child;
    }

    private Canvas FindChildCanvas(string childName)
    {
        Transform child = FindChildTransform(childName);
        if (child == null)
            return null;

        Canvas canvas = child.GetComponent<Canvas>();
        return canvas != null ? canvas : child.GetComponentInChildren<Canvas>(true);
    }

    private static Canvas ResolvePromptCanvasFromController()
    {
        WorldInteractionPromptController promptController = WorldInteractionPromptController.Instance;
        return promptController != null ? promptController.GetComponentInParent<Canvas>(true) : null;
    }

    private static string GetCanvasName(GlobalCanvasLayer layer)
    {
        return layer switch
        {
            GlobalCanvasLayer.GameplayHUD => "GameplayHUDCanvas",
            GlobalCanvasLayer.Dialogue => "DialogueCanvas",
            GlobalCanvasLayer.Popup => "PopupCanvas",
            GlobalCanvasLayer.Hover => "HoverCanvas",
            GlobalCanvasLayer.Prompt => "PromptCanvas",
            GlobalCanvasLayer.Reward => "RewardCanvas",
            GlobalCanvasLayer.DamagePopup => "DamagePopupCanvas",
            GlobalCanvasLayer.BossHUD => "BossHUDCanvas",
            GlobalCanvasLayer.GameOver => "GameOverCanvas",
            GlobalCanvasLayer.Loading => "LoadingCanvas",
            _ => "UICanvas"
        };
    }
}
