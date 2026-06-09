using DG.Tweening;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class LightningSpearDropLandingVisual2D : MonoBehaviour, IWorldItemDropLandingVisual
{
    [Header("References")]
    [SerializeField] private Transform spearTransform;
    [SerializeField] private SpriteRenderer spearRenderer;
    [SerializeField] private SpriteMask buriedMask;

    [Header("Pose")]
    [SerializeField] private Vector3 travelLocalPosition = new(0f, 0.45f, 0f);
    [SerializeField] private Vector3 embeddedLocalPosition = Vector3.zero;
    [SerializeField] private Vector3 embeddedLocalEulerAngles = new(0f, 0f, -180f);

    [Header("Timing")]
    [SerializeField, Min(0f)] private float embedDuration = 0.14f;

    private Tween activeLandingTween;

    public void OnDropTravelStarted()
    {
        ResolveReferences();
        KillActiveLandingTween();
        ApplyTravelPose();
    }

    public Tween CreateDropLandingTween()
    {
        ResolveReferences();

        if (spearTransform == null)
            return null;

        KillActiveLandingTween();

        Sequence landingSequence = DOTween.Sequence()
            .AppendCallback(ApplyLandingStartState);

        if (embedDuration > 0f)
        {
            landingSequence.Append(
                spearTransform
                    .DOLocalMove(embeddedLocalPosition, embedDuration)
                    .SetEase(Ease.OutCubic));
        }
        else
        {
            landingSequence.AppendCallback(() => spearTransform.localPosition = embeddedLocalPosition);
        }

        landingSequence.OnComplete(ApplyEmbeddedPose);
        landingSequence.OnKill(() =>
        {
            if (activeLandingTween == landingSequence)
                activeLandingTween = null;
        });

        activeLandingTween = landingSequence;
        return landingSequence;
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyEmbeddedPose();
    }

    private void OnDisable()
    {
        KillActiveLandingTween();
    }

    private void OnDestroy()
    {
        KillActiveLandingTween();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnValidate()
    {
        ResolveReferences();
        embedDuration = Mathf.Max(0f, embedDuration);
    }

    private void ApplyTravelPose()
    {
        if (spearTransform != null)
        {
            spearTransform.localPosition = travelLocalPosition;
            spearTransform.localRotation = Quaternion.Euler(embeddedLocalEulerAngles);
        }

        if (spearRenderer != null)
            spearRenderer.maskInteraction = SpriteMaskInteraction.None;

        if (buriedMask != null)
            buriedMask.enabled = false;
    }

    private void ApplyLandingStartState()
    {
        if (spearTransform != null)
        {
            spearTransform.localPosition = travelLocalPosition;
            spearTransform.localRotation = Quaternion.Euler(embeddedLocalEulerAngles);
        }

        if (buriedMask != null)
            buriedMask.enabled = true;

        if (spearRenderer != null)
            spearRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
    }

    private void ApplyEmbeddedPose()
    {
        if (spearTransform != null)
        {
            spearTransform.localPosition = embeddedLocalPosition;
            spearTransform.localRotation = Quaternion.Euler(embeddedLocalEulerAngles);
        }

        if (buriedMask != null)
            buriedMask.enabled = true;

        if (spearRenderer != null)
            spearRenderer.maskInteraction = SpriteMaskInteraction.VisibleOutsideMask;
    }

    private void KillActiveLandingTween()
    {
        if (activeLandingTween == null)
            return;

        Tween tween = activeLandingTween;
        activeLandingTween = null;
        tween.Kill();
    }

    private void ResolveReferences()
    {
        if (spearRenderer == null)
            spearRenderer = GetComponentInChildren<SpriteRenderer>(includeInactive: true);

        if (spearTransform == null && spearRenderer != null)
            spearTransform = spearRenderer.transform;

        if (buriedMask == null)
            buriedMask = GetComponentInChildren<SpriteMask>(includeInactive: true);
    }
}

public interface IWorldItemDropLandingVisual
{
    void OnDropTravelStarted();
    Tween CreateDropLandingTween();
}
