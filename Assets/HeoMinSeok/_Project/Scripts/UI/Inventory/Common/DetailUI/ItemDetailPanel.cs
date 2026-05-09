using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 책임 :
/// - 공용 상세 패널의 헤더와 각 아이템 타입별 상세 뷰를 연결한다.
/// - 유물 프리뷰 가이드와 헤더 제목 병합처럼 공통 헤더 렌더링 규칙을 관리한다.
/// </summary>
public class ItemDetailPanel : MonoBehaviour, IHoverView, IHoverPositionOffsetProvider
{
    public static ItemDetailPanel Instance { get; private set; }

    [Header("Header")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text subtitleText;
    [SerializeField] private GameObject relicPreviewPreviousGuideRoot;
    [SerializeField] private GameObject relicPreviewNextGuideRoot;
    [SerializeField] private Image relicPreviewPreviousGuideIcon;
    [SerializeField] private Image relicPreviewNextGuideIcon;
    [SerializeField] private CanvasGroup relicPreviewPreviousGuide;
    [SerializeField] private CanvasGroup relicPreviewNextGuide;
    [SerializeField, Range(0f, 1f)] private float disabledGuideAlpha = 0.35f;

    [Header("Views")]
    [SerializeField] private WeaponDetailView weaponView;
    [SerializeField] private WeaponDetailViewV2 weaponViewV2;
    [SerializeField] private RelicDetailView relicView;
    [SerializeField] private ConsumableDetailView consumableView;

    [Header("Glossary (optional)")]
    [SerializeField] private GlossaryDatabase glossary;
    [SerializeField] private GlossaryPopup glossaryPopup;

    [Header("Services")]
    [SerializeField] private TooltipColorPalette tooltipColorPalette;
    [SerializeField] private string glossaryLinkColorHex = "5EC8FF";

    [Header("Presentation")]
    [SerializeField] private Vector2 openOffset = new Vector2(0f, -24f);
    [SerializeField, Min(0f)] private float openDuration = 0.12f;
    [SerializeField, Min(0f)] private float closeDuration = 0.1f;
    [SerializeField, Range(0f, 1f)] private float closedAlpha = 0f;
    [SerializeField] private bool useUnscaledTime = true;

    private ItemDetailPanelServices _services;
    private object currentDefinition;
    private string currentHeaderLevelSuffix = string.Empty;
    private CanvasGroup canvasGroup;
    private Coroutine presentationRoutine;
    private Vector2 hoverPositionOffset;
    private bool isAnimating;
    private int presentationSerial;

    public RectTransform Rect => transform as RectTransform;
    public Vector2 HoverPositionOffset => hoverPositionOffset;
    public bool IsActive => gameObject.activeSelf && (isAnimating || (canvasGroup != null && canvasGroup.alpha > 0.01f));

    public void OpenUI()
    {
        gameObject.SetActive(true);
    }

    public void CloseUI()
    {
        HideHover();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        GlobalUIRoot.AdoptToCanvas(GlobalCanvasLayer.Hover, transform);
        ResolvePresentationReferences();
        _services = new ItemDetailPanelServices
        {
            formatText = raw => DetailTextFormatter.Format(raw, tooltipColorPalette, glossaryLinkColorHex),
            showGlossary = ShowGlossaryPopup,
            setHeaderLevelText = SetHeaderLevelSuffix
        };

        SnapHiddenPresentation();
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        StopPresentationRoutine();

        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (!gameObject.activeSelf || relicView == null || !relicView.gameObject.activeSelf)
            return;

        UpdateRelicPreviewGuideState();
    }

    public void ShowHover(object definition, object context = null)
    {
        if (definition == null)
        {
            HideHover();
            return;
        }

        ResolvePresentationReferences();
        int serial = ++presentationSerial;
        bool animateOpen = !IsActive;
        var ctx = context as ItemDetailContext;
        currentDefinition = definition;
        currentHeaderLevelSuffix = string.Empty;

        if (glossaryPopup != null)
            glossaryPopup.Hide();

        gameObject.SetActive(true);
        SetRelicPreviewGuidesVisible(false);
        SyncRelicPreviewGuideIcons();

        if (definition is IInventoryItemDefinition common)
        {
            if (iconImage != null)
            {
                iconImage.sprite = common.Icon;
                iconImage.enabled = common.Icon != null;
            }

            RefreshRelicHeaderTitle();

            if (subtitleText != null)
                subtitleText.text = common.Kind.ToString();
        }
        else
        {
            RefreshRelicHeaderTitle();

            if (subtitleText != null)
                subtitleText.text = string.Empty;

            if (iconImage != null)
            {
                iconImage.sprite = null;
                iconImage.enabled = false;
            }
        }

        if (weaponViewV2 != null && weaponViewV2.CanShow(definition))
        {
            weaponViewV2.Show(definition, ctx, _services);
            if (weaponView != null)
                weaponView.Hide();
            if (relicView != null)
                relicView.Hide();
            if (consumableView != null)
                consumableView.Hide();
        }
        else if (weaponView != null && weaponView.CanShow(definition))
        {
            weaponView.Show(definition, ctx, _services);
            if (weaponViewV2 != null)
                weaponViewV2.Hide();
            if (relicView != null)
                relicView.Hide();
            if (consumableView != null)
                consumableView.Hide();
        }
        else if (relicView != null && relicView.CanShow(definition))
        {
            relicView.Show(definition, ctx, _services);
            SetRelicPreviewGuidesVisible(true);
            UpdateRelicPreviewGuideState();
            if (weaponView != null)
                weaponView.Hide();
            if (weaponViewV2 != null)
                weaponViewV2.Hide();
            if (consumableView != null)
                consumableView.Hide();
        }
        else if (consumableView != null && consumableView.CanShow(definition))
        {
            consumableView.Show(definition, ctx, _services);
            if (weaponView != null)
                weaponView.Hide();
            if (weaponViewV2 != null)
                weaponViewV2.Hide();
            if (relicView != null)
                relicView.Hide();
        }
        else
        {
            if (weaponView != null)
                weaponView.Hide();
            if (weaponViewV2 != null)
                weaponViewV2.Hide();

            if (relicView != null)
                relicView.Hide();

            if (consumableView != null)
                consumableView.Hide();
        }

        Canvas.ForceUpdateCanvases();
        PlayOpenPresentation(animateOpen, serial);
    }

    public void HideHover()
    {
        ResolvePresentationReferences();
        int serial = ++presentationSerial;

        if (canvasGroup != null && (gameObject.activeSelf || isAnimating))
        {
            PlayClosePresentation(serial);
            return;
        }

        CleanupHiddenContent();
        gameObject.SetActive(false);
    }

    private void CleanupHiddenContent()
    {
        if (weaponView != null)
            weaponView.Hide();

        if (weaponViewV2 != null)
            weaponViewV2.Hide();

        if (relicView != null)
            relicView.Hide();

        if (consumableView != null)
            consumableView.Hide();

        if (glossaryPopup != null)
            glossaryPopup.Hide();

        SetRelicPreviewGuidesVisible(false);
        currentDefinition = null;
        currentHeaderLevelSuffix = string.Empty;
        SetHeaderTitle(string.Empty);
    }

    private void ResolvePresentationReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (canvasGroup == null)
            canvasGroup = gameObject.AddComponent<CanvasGroup>();

        ForceNonInteractive();
    }

    private void PlayOpenPresentation(bool animate, int serial)
    {
        StopPresentationRoutine();
        ResolvePresentationReferences();

        if (!gameObject.activeSelf)
            gameObject.SetActive(true);

        if (serial != presentationSerial)
            return;

        bool alreadyVisible = canvasGroup.alpha > 0.99f && hoverPositionOffset.sqrMagnitude < 0.01f;
        if (!animate || alreadyVisible || openDuration <= 0f)
        {
            ApplyPresentation(Vector2.zero, 1f);
            isAnimating = false;
            ForceNonInteractive();
            return;
        }

        Vector2 fromOffset = canvasGroup.alpha > 0.01f ? hoverPositionOffset : openOffset;
        presentationRoutine = StartCoroutine(CoPresentation(fromOffset, Vector2.zero, canvasGroup.alpha, 1f, openDuration, false, null));
    }

    private void PlayClosePresentation(int serial)
    {
        StopPresentationRoutine();
        ResolvePresentationReferences();

        if (!gameObject.activeSelf || (canvasGroup.alpha <= 0.01f && !isAnimating))
        {
            SnapHiddenPresentation();
            if (serial == presentationSerial)
                CleanupHiddenContent();
            gameObject.SetActive(false);
            return;
        }

        presentationRoutine = StartCoroutine(CoPresentation(
            hoverPositionOffset,
            openOffset,
            canvasGroup.alpha,
            closedAlpha,
            closeDuration,
            true,
            () =>
            {
                if (serial != presentationSerial)
                    return;

                CleanupHiddenContent();
                gameObject.SetActive(false);
            }));
    }

    private IEnumerator CoPresentation(
        Vector2 fromOffset,
        Vector2 toOffset,
        float fromAlpha,
        float toAlpha,
        float duration,
        bool driveRectPosition,
        System.Action onComplete)
    {
        isAnimating = true;
        RectTransform rectTransform = Rect;
        Vector2 baseAnchoredPosition = rectTransform != null
            ? rectTransform.anchoredPosition - fromOffset
            : Vector2.zero;

        if (duration <= 0f)
        {
            ApplyPresentation(toOffset, toAlpha, driveRectPosition, baseAnchoredPosition);
            FinishPresentation(onComplete);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = EaseOutCubic(t);
            ApplyPresentation(
                Vector2.LerpUnclamped(fromOffset, toOffset, eased),
                Mathf.LerpUnclamped(fromAlpha, toAlpha, eased),
                driveRectPosition,
                baseAnchoredPosition);
            yield return null;
        }

        ApplyPresentation(toOffset, toAlpha, driveRectPosition, baseAnchoredPosition);
        FinishPresentation(onComplete);
    }

    private void FinishPresentation(System.Action onComplete)
    {
        presentationRoutine = null;
        isAnimating = false;
        ForceNonInteractive();
        onComplete?.Invoke();
    }

    private void ApplyPresentation(Vector2 offset, float alpha, bool driveRectPosition = false, Vector2 baseAnchoredPosition = default)
    {
        hoverPositionOffset = offset;

        if (canvasGroup != null)
            canvasGroup.alpha = alpha;

        if (driveRectPosition && Rect != null)
            Rect.anchoredPosition = baseAnchoredPosition + offset;
    }

    private void SnapHiddenPresentation()
    {
        StopPresentationRoutine();
        ApplyPresentation(openOffset, closedAlpha);
        ForceNonInteractive();
    }

    private void ForceNonInteractive()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void StopPresentationRoutine()
    {
        if (presentationRoutine == null)
            return;

        StopCoroutine(presentationRoutine);
        presentationRoutine = null;
        isAnimating = false;
    }

    private static float EaseOutCubic(float t)
    {
        t = 1f - Mathf.Clamp01(t);
        return 1f - t * t * t;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 상세 대상이 유물일 때만 헤더의 Q/E 프리뷰 가이드 비주얼을 각각 노출한다.
    /// - 실제 프리뷰 입력 처리 책임은 RelicDetailView에 두고, 공용 패널은 표시 여부만 관리한다.
    /// </summary>
    private void SetRelicPreviewGuidesVisible(bool visible)
    {
        if (relicPreviewPreviousGuideRoot != null)
            relicPreviewPreviousGuideRoot.SetActive(visible);

        if (relicPreviewNextGuideRoot != null)
            relicPreviewNextGuideRoot.SetActive(visible);
    }

    /// <summary>
    /// 책임 :
    /// - 유물 프리뷰가 더 이상 불가능한 방향의 Q/E 가이드만 어둡게 표시한다.
    /// - 실제 입력과 레벨 상태는 RelicDetailView가 소유하고, 공용 패널은 시각 상태만 반영한다.
    /// </summary>
    private void UpdateRelicPreviewGuideState()
    {
        if (relicView == null)
            return;

        ApplyGuideAlpha(relicPreviewPreviousGuide, relicView.CanPreviewPreviousLevel);
        ApplyGuideAlpha(relicPreviewNextGuide, relicView.CanPreviewNextLevel);
    }

    /// <summary>
    /// 책임 :
    /// - 헤더 가이드 이미지의 밝기만 조절하고, 활성/비활성 책임은 별도 guide root 토글에 맡긴다.
    /// </summary>
    private void ApplyGuideAlpha(CanvasGroup guide, bool enabled)
    {
        if (guide == null)
            return;

        guide.alpha = enabled ? 1f : disabledGuideAlpha;
    }

    /// <summary>
    /// 책임 :
    /// - 현재 바인딩된 Skill1/Skill2 키에 맞춰 유물 프리뷰 가이드 아이콘을 동기화한다.
    /// - 입력 리매핑이 바뀌어도 상세 패널은 InputBindingService를 통해 같은 아이콘 규칙을 재사용한다.
    /// </summary>
    private void SyncRelicPreviewGuideIcons()
    {
        InputBindingService input = InputBindingService.EnsureInstance();

        if (relicPreviewPreviousGuideIcon != null)
        {
            Sprite icon = input.GetBindingIcon(InputActionId.Skill1);
            relicPreviewPreviousGuideIcon.sprite = icon;
            relicPreviewPreviousGuideIcon.enabled = icon != null;
        }

        if (relicPreviewNextGuideIcon != null)
        {
            Sprite icon = input.GetBindingIcon(InputActionId.Skill2);
            relicPreviewNextGuideIcon.sprite = icon;
            relicPreviewNextGuideIcon.enabled = icon != null;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 공용 헤더 제목 문자열을 현재 표시 대상에 맞게 조합한다.
    /// - 유물은 이름 뒤에 프리뷰 레벨 텍스트를 병합해 별도 레벨 텍스트 오브젝트 없이 같은 줄에서 보여준다.
    /// </summary>
    private string BuildHeaderTitle(string baseTitle)
    {
        if (currentDefinition is RelicDefinition && !string.IsNullOrEmpty(currentHeaderLevelSuffix))
            return $"{baseTitle} {currentHeaderLevelSuffix}";

        return baseTitle ?? string.Empty;
    }

    /// <summary>
    /// 책임 :
    /// - 헤더 title 텍스트를 한 곳에서 갱신한다.
    /// - RelicDetailView가 레벨 프리뷰를 바꿀 때도 같은 경로를 통해 제목을 다시 그릴 수 있게 한다.
    /// </summary>
    private void SetHeaderTitle(string text)
    {
        if (titleText != null)
            titleText.text = text ?? string.Empty;
    }

    /// <summary>
    /// 책임 :
    /// - 유물 상세 뷰가 계산한 레벨 프리뷰 문자열을 헤더 제목 suffix로 저장한다.
    /// - 제목 재조합은 공통 패널이 맡아, 유물 뷰는 헤더 레이아웃 구조를 몰라도 되게 한다.
    /// </summary>
    private void SetHeaderLevelSuffix(string text)
    {
        currentHeaderLevelSuffix = text ?? string.Empty;
        RefreshRelicHeaderTitle();
    }

    /// <summary>
    /// 책임 :
    /// - 현재 유물 헤더의 레벨 표기가 바뀌었을 때 제목 문자열을 다시 조합한다.
    /// - 레벨 프리뷰 로직은 RelicDetailView가 소유하고, ItemDetailPanel은 제목 렌더링만 맡는다.
    /// </summary>
    private void RefreshRelicHeaderTitle()
    {
        if (!gameObject.activeSelf)
            return;

        string baseTitle = currentDefinition != null ? currentDefinition.ToString() : string.Empty;
        if (currentDefinition is IInventoryItemDefinition common)
            baseTitle = common.DisplayName;

        SetHeaderTitle(BuildHeaderTitle(baseTitle));
    }

    private void ShowGlossaryPopup(string key)
    {
        if (glossaryPopup == null)
            return;

        if (glossary != null && glossary.TryGet(key, out var desc))
        {
            glossaryPopup.Show(key, desc);
        }
        else
        {
            glossaryPopup.Show(key, "설명이 등록되지 않았습니다.");
        }
    }
}
