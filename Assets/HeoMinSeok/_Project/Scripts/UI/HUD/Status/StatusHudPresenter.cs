using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - StatusHudService가 수집한 엔트리를 gameplay HUD 위젯으로 렌더링하고 풀링을 관리한다.
/// - 상태 HUD가 특정 프리팹 authoring 없이도 런타임에 표시될 수 있도록 기본 루트와 엔트리 뷰를 코드로 구성한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class StatusHudPresenter : MonoBehaviour
{
    private static StatusHudPresenter instance;

    [Header("Authoring")]
    [SerializeField] private RectTransform container;
    [SerializeField] private StatusHudEntryView entryViewPrefab;
    [SerializeField] private Vector2 fallbackAnchoredPosition = new(-220f, -110f);
    [SerializeField] private Vector2 fallbackContainerSize = new(360f, 56f);
    [SerializeField, Min(0f)] private float fallbackEntrySpacing = 8f;
    [SerializeField] private TextAnchor fallbackChildAlignment = TextAnchor.UpperRight;

    private readonly List<StatusHudEntry> entries = new();
    private readonly List<StatusHudEntryView> entryViews = new();
    private StatusHudService service;

    public static StatusHudPresenter Instance => EnsureInstance();

    public static StatusHudPresenter EnsureInstance()
    {
        if (instance != null)
            return instance;

        instance = FindFirstObjectByType<StatusHudPresenter>();
        if (instance != null)
            return instance;

        StatusHudPresenter prefab = GlobalUIRoot.GetStatusHudPresenterPrefab();
        if (prefab != null)
        {
            instance = Instantiate(prefab);
            return instance;
        }

        GameObject root = new("StatusHudPresenter");
        RuntimePresentationFallbackAudit.Record(
            root,
            "Status HUD presenter fallback",
            "a GlobalUIRoot status HUD presenter prefab");
        instance = root.AddComponent<StatusHudPresenter>();
        return instance;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        service = StatusHudService.EnsureInstance();
        EnsureRoot();
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private void LateUpdate()
    {
        service ??= StatusHudService.EnsureInstance();
        EnsureRoot();

        if (service == null || container == null)
            return;

        service.CollectEntries(entries);
        entries.Sort(CompareEntries);
        EnsureViewPool(entries.Count);

        for (int i = 0; i < entryViews.Count; i++)
        {
            bool active = i < entries.Count;
            entryViews[i].gameObject.SetActive(active);
            if (active)
                entryViews[i].Bind(entries[i]);
        }
    }

    /// <summary>
    /// 책임 :
    /// - gameplay HUD canvas 아래에 상태 HUD 전용 루트 컨테이너를 준비한다.
    /// - 씬/프리팹 수작업 없이도 상태 HUD가 기존 HUD 옆에 붙을 수 있게 기본 배치 구조를 런타임에 보장한다.
    /// </summary>
    private void EnsureRoot()
    {
        if (container != null)
            return;

        Canvas gameplayCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.GameplayHUD);
        if (gameplayCanvas == null)
            return;

        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.GameplayHUD, transform, false);

        container = gameObject.GetComponent<RectTransform>() ?? gameObject.AddComponent<RectTransform>();
        container.anchorMin = new Vector2(1f, 1f);
        container.anchorMax = new Vector2(1f, 1f);
        container.pivot = new Vector2(1f, 1f);
        container.anchoredPosition = fallbackAnchoredPosition;
        if (container.sizeDelta == Vector2.zero)
            container.sizeDelta = fallbackContainerSize;

        HorizontalLayoutGroup layout = gameObject.GetComponent<HorizontalLayoutGroup>() ?? gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = fallbackEntrySpacing;
        layout.childControlWidth = false;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        layout.childAlignment = fallbackChildAlignment;

        ContentSizeFitter fitter = gameObject.GetComponent<ContentSizeFitter>() ?? gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private void EnsureViewPool(int desiredCount)
    {
        while (entryViews.Count < desiredCount)
        {
            StatusHudEntryView view;
            if (entryViewPrefab != null)
            {
                view = Instantiate(entryViewPrefab, container);
                view.name = $"StatusHudEntry_{entryViews.Count}";
            }
            else
            {
                RuntimePresentationFallbackAudit.Record(
                    this,
                    "Status HUD entry fallback",
                    "a StatusHudEntryView prefab assigned on StatusHudPresenter");

                GameObject child = new($"StatusHudEntry_{entryViews.Count}");
                child.transform.SetParent(container, false);
                view = child.AddComponent<StatusHudEntryView>();
            }

            entryViews.Add(view);
        }
    }

    private static int CompareEntries(StatusHudEntry a, StatusHudEntry b)
    {
        int byPriority = b.Priority.CompareTo(a.Priority);
        if (byPriority != 0)
            return byPriority;

        int byGroup = a.Group.CompareTo(b.Group);
        if (byGroup != 0)
            return byGroup;

        return string.CompareOrdinal(a.StatusId, b.StatusId);
    }
}
