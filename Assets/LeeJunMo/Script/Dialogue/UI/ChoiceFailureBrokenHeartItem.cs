using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

public sealed class ChoiceFailureBrokenHeartItem : MonoBehaviour
{
    [SerializeField] private RectTransform root;
    [SerializeField] private RectTransform leftPiece;
    [SerializeField] private RectTransform rightPiece;
    [SerializeField] private Image leftImage;
    [SerializeField] private Image rightImage;

    private Sequence activeSequence;

    private void Awake()
    {
        ResolveReferences();
        ResetState();
    }

    private void OnDisable()
    {
        Stop();
    }

    public void Play(
        Sprite sprite,
        Color heartColor,
        Vector2 startPosition,
        float size,
        float fallDistance,
        float horizontalDrift,
        float duration,
        float delay,
        float breakTimeRatio,
        float splitDistance,
        float breakDropDistance,
        float rotationAmount,
        Action<ChoiceFailureBrokenHeartItem> onComplete)
    {
        ResolveReferences();
        Stop();

        if (root == null || leftPiece == null || rightPiece == null || leftImage == null || rightImage == null)
        {
            onComplete?.Invoke(this);
            return;
        }

        gameObject.SetActive(true);
        ApplySprite(sprite);
        SetColorAlpha(heartColor, 0f);

        root.anchoredPosition = startPosition;
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-10f, 10f));
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

        ResetPieceTransform(leftPiece);
        ResetPieceTransform(rightPiece);

        float breakTime = Mathf.Clamp01(breakTimeRatio) * duration;
        float remainingDuration = Mathf.Max(0.05f, duration - breakTime);
        Vector2 endPosition = startPosition + new Vector2(horizontalDrift, -fallDistance);
        Color visibleColor = heartColor;
        visibleColor.a = heartColor.a;

        activeSequence = DOTween.Sequence();
        activeSequence.SetTarget(gameObject);
        activeSequence.SetUpdate(true);
        activeSequence.AppendInterval(delay);
        activeSequence.Append(leftImage.DOFade(visibleColor.a, 0.12f).SetEase(Ease.OutSine).SetUpdate(true));
        activeSequence.Join(rightImage.DOFade(visibleColor.a, 0.12f).SetEase(Ease.OutSine).SetUpdate(true));
        activeSequence.Join(root.DOAnchorPos(endPosition, duration).SetEase(Ease.InQuad).SetUpdate(true));

        float splitAt = delay + breakTime;
        activeSequence.Insert(splitAt, leftPiece.DOAnchorPos(new Vector2(-splitDistance, -breakDropDistance), remainingDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true));
        activeSequence.Insert(splitAt, rightPiece.DOAnchorPos(new Vector2(splitDistance, -breakDropDistance), remainingDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true));
        activeSequence.Insert(splitAt, leftPiece.DORotate(new Vector3(0f, 0f, rotationAmount), remainingDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true));
        activeSequence.Insert(splitAt, rightPiece.DORotate(new Vector3(0f, 0f, -rotationAmount), remainingDuration)
            .SetEase(Ease.OutCubic)
            .SetUpdate(true));

        float fadeAt = delay + duration * 0.72f;
        float fadeDuration = Mathf.Max(0.12f, duration * 0.26f);
        activeSequence.Insert(fadeAt, leftImage.DOFade(0f, fadeDuration).SetEase(Ease.InSine).SetUpdate(true));
        activeSequence.Insert(fadeAt, rightImage.DOFade(0f, fadeDuration).SetEase(Ease.InSine).SetUpdate(true));
        activeSequence.OnComplete(() =>
        {
            ResetState();
            onComplete?.Invoke(this);
        });
    }

    public void ShowStaticPreview(Sprite sprite, Color heartColor, Vector2 position, float size, float splitDistance)
    {
        ResolveReferences();
        Stop();

        if (root == null || leftPiece == null || rightPiece == null || leftImage == null || rightImage == null)
            return;

        gameObject.SetActive(true);
        ApplySprite(sprite);
        SetColorAlpha(heartColor, heartColor.a);

        root.anchoredPosition = position;
        root.localScale = Vector3.one;
        root.localRotation = Quaternion.identity;
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size);
        root.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size);

        leftPiece.anchoredPosition = new Vector2(-splitDistance, 0f);
        rightPiece.anchoredPosition = new Vector2(splitDistance, 0f);
        leftPiece.localRotation = Quaternion.Euler(0f, 0f, 12f);
        rightPiece.localRotation = Quaternion.Euler(0f, 0f, -12f);
        leftPiece.localScale = Vector3.one;
        rightPiece.localScale = Vector3.one;
    }

    public void Stop()
    {
        activeSequence?.Kill();
        activeSequence = null;

        if (root != null)
            root.DOKill();

        if (leftPiece != null)
            leftPiece.DOKill();

        if (rightPiece != null)
            rightPiece.DOKill();

        if (leftImage != null)
            leftImage.DOKill();

        if (rightImage != null)
            rightImage.DOKill();
    }

    public void ResetState()
    {
        ResolveReferences();
        Stop();

        ResetPieceTransform(leftPiece);
        ResetPieceTransform(rightPiece);
        SetColorAlpha(Color.white, 0f);

        gameObject.SetActive(false);
    }

    private void ResolveReferences()
    {
        if (root == null)
            root = transform as RectTransform;

        if (leftPiece == null)
            leftPiece = transform.Find("Left") as RectTransform;

        if (rightPiece == null)
            rightPiece = transform.Find("Right") as RectTransform;

        if (leftImage == null && leftPiece != null)
            leftImage = leftPiece.GetComponent<Image>();

        if (rightImage == null && rightPiece != null)
            rightImage = rightPiece.GetComponent<Image>();
    }

    private void ApplySprite(Sprite sprite)
    {
        ConfigureImage(leftImage, sprite);
        ConfigureImage(rightImage, sprite);
    }

    private static void ConfigureImage(Image image, Sprite sprite)
    {
        if (image == null)
            return;

        image.sprite = sprite;
        image.raycastTarget = false;
        image.preserveAspect = true;
    }

    private void SetColorAlpha(Color baseColor, float alpha)
    {
        Color color = baseColor;
        color.a = alpha;

        if (leftImage != null)
            leftImage.color = color;

        if (rightImage != null)
            rightImage.color = color;
    }

    private static void ResetPieceTransform(RectTransform piece)
    {
        if (piece == null)
            return;

        piece.anchorMin = Vector2.zero;
        piece.anchorMax = Vector2.one;
        piece.offsetMin = Vector2.zero;
        piece.offsetMax = Vector2.zero;
        piece.anchoredPosition = Vector2.zero;
        piece.localRotation = Quaternion.identity;
        piece.localScale = Vector3.one;
    }
}
