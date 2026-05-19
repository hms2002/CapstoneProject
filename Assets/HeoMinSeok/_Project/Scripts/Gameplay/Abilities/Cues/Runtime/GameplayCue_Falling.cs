using UnityEngine;
using UnityGAS;
using DG.Tweening;

public class GameplayCue_Falling : GameplayCueNotify
{
    [Header("Visual Settings")]
    [SerializeField] private float animDuration = 0.8f;
    [SerializeField] private Ease fallEase = Ease.InBack;
    [SerializeField] private float rotateSpeed = 720f;
    [Tooltip("플레이어 발바닥 오프셋 (보통 -0.5 ~ -0.8)")]
    [SerializeField] private Vector3 footOffset = new Vector3(0, -0.5f, 0);

    private Vector3 originalScale;
    private Quaternion originalRotation;
    private RigidbodyType2D originalBodyType;

    private Tween scaleTween;
    private Tween rotateTween;
    private Tween moveTween;

    public override void OnAdd(GameplayCueParams p)
    {
        GameObject target = p.Target;
        if (target == null)
            return;

        SaveAndDisablePhysics(target);

        Vector3 targetHolePosition = ResolveFallPosition(p, target);

        scaleTween = target.transform.DOScale(Vector3.zero, animDuration).SetEase(fallEase);
        rotateTween = target.transform.DORotate(new Vector3(0, 0, rotateSpeed), animDuration, RotateMode.FastBeyond360)
            .SetEase(Ease.InCubic).SetLoops(-1, LoopType.Incremental);
        moveTween = target.transform.DOMove(targetHolePosition, animDuration).SetEase(Ease.OutQuart);
    }

    private Vector3 ResolveFallPosition(GameplayCueParams cueParams, GameObject target)
    {
        if (cueParams.HasExplicitPosition)
            return cueParams.Position;

        return PitFallPositionResolver.ResolveFallCenter(target.transform.position, cueParams.Causer, footOffset);
    }

    private void SaveAndDisablePhysics(GameObject target)
    {
        originalScale = target.transform.localScale;
        originalRotation = target.transform.localRotation;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            originalBodyType = rb.bodyType;
            rb.linearVelocity = Vector2.zero;
            rb.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    public override void OnRemove(GameplayCueParams p)
    {
        GameObject target = p.Target;
        if (target == null) return;

        scaleTween?.Kill();
        rotateTween?.Kill();
        moveTween?.Kill();

        if (originalScale.sqrMagnitude < 0.01f) originalScale = Vector3.one;
        target.transform.localScale = originalScale;
        target.transform.localRotation = originalRotation;

        Rigidbody2D rb = target.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.bodyType = originalBodyType;
            rb.linearVelocity = Vector2.zero;
        }
    }
}
