using TMPro;
using UnityEngine;

public class DamagePopupUI : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private TMP_Text text;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform rectTransform;

    [Header("Motion (UI space)")]
    [SerializeField] private Vector2 moveVelocity = new Vector2(0f, 140f); // px/sec
    [SerializeField] private float lifetime = 0.75f;
    [SerializeField] private float fadeOutRatio = 0.55f;

    [Header("Scale")]
    [SerializeField] private float startScale = 0.9f;
    [SerializeField] private float endScale = 1.1f;

    private float t;

    private void Reset()
    {
        text = GetComponentInChildren<TMP_Text>();
        canvasGroup = GetComponent<CanvasGroup>();
        rectTransform = GetComponent<RectTransform>();
    }

    private void Awake()
    {
        if (text == null) text = GetComponentInChildren<TMP_Text>();
        if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
        if (rectTransform == null) rectTransform = GetComponent<RectTransform>();
    }

    public void Setup(int amount, Vector2 anchoredPos)
    {
        t = 0f;

        if (rectTransform != null)
            rectTransform.anchoredPosition = anchoredPos;

        if (text != null)
            text.text = amount.ToString();

        if (canvasGroup != null)
            canvasGroup.alpha = 1f;

        transform.localScale = Vector3.one * startScale;
    }

    private void Update()
    {
        t += Time.deltaTime;
        float p = Mathf.Clamp01(t / lifetime);

        if (rectTransform != null)
            rectTransform.anchoredPosition += moveVelocity * Time.deltaTime;

        transform.localScale = Vector3.one * Mathf.Lerp(startScale, endScale, p);

        // Fade
        if (canvasGroup != null)
        {
            if (p >= fadeOutRatio)
            {
                float fp = (p - fadeOutRatio) / Mathf.Max(0.0001f, 1f - fadeOutRatio);
                canvasGroup.alpha = Mathf.Lerp(1f, 0f, fp);
            }
        }

        if (t >= lifetime)
            Destroy(gameObject);
    }
}
