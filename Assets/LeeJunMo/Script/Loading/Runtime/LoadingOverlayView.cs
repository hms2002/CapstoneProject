using UnityEngine;
using UnityEngine.UI;
using TMPro;

[DisallowMultipleComponent]
public sealed class LoadingOverlayView : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text detailText;
    [SerializeField] private TMP_Text percentText;
    [SerializeField] private TMP_Text tipLabelText;
    [SerializeField] private TMP_Text tipText;
    [SerializeField] private Image progressFillImage;
    [SerializeField] private RectTransform progressGlowRect;
    [SerializeField] private RectTransform travelHost;
    [SerializeField] private RectTransform defaultTravelRoot;
    [SerializeField] private RectTransform travelTrackBoundsRect;
    [SerializeField] private Image travelTrackFillImage;
    [SerializeField] private RectTransform travelWalkerRect;

    public RectTransform Root => root != null ? root : transform as RectTransform;
    public CanvasGroup CanvasGroup => canvasGroup;
    public TMP_Text TitleText => titleText;
    public TMP_Text StatusText => statusText;
    public TMP_Text DetailText => detailText;
    public TMP_Text PercentText => percentText;
    public TMP_Text TipLabelText => tipLabelText;
    public TMP_Text TipText => tipText;
    public Image ProgressFillImage => progressFillImage;
    public RectTransform ProgressGlowRect => progressGlowRect;
    public RectTransform TravelHost => travelHost;
    public RectTransform DefaultTravelRoot => defaultTravelRoot;
    public RectTransform TravelTrackBoundsRect => travelTrackBoundsRect;
    public Image TravelTrackFillImage => travelTrackFillImage;
    public RectTransform TravelWalkerRect => travelWalkerRect;

    private void Reset()
    {
        root = transform as RectTransform;
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnValidate()
    {
        if (root == null)
            root = transform as RectTransform;

        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
    }
}
