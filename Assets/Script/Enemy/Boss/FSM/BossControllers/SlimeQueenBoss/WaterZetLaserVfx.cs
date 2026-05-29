using System.Collections;
using UnityEngine;

/// <summary>
/// 책임:
/// - Slime Queen 물총 전용 레이저의 시작/유지/종료 애니메이션과 표시 길이/폭을 구성한다.
/// - Demon King 레이저와 비슷한 API를 제공하되, 물총 전용 디테일을 독립적으로 조정할 수 있게 분리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class WaterZetLaserVfx : MonoBehaviour
{
    private const string StartStateName = "Start";
    private const string IdleStateName = "Idle";
    private const string EndStateName = "End";
    private const float DefaultBodyVisualLengthMultiplier = 1f;

    [SerializeField] private SpriteRenderer startRenderer;
    [SerializeField] private SpriteRenderer bodyRenderer;
    [SerializeField] private Animator startAnimator;
    [SerializeField] private Animator bodyAnimator;
    [SerializeField] private AnimationClip startStartClip;
    [SerializeField] private AnimationClip bodyStartClip;
    [SerializeField] private AnimationClip startIdleClip;
    [SerializeField] private AnimationClip bodyIdleClip;
    [SerializeField] private AnimationClip startEndClip;
    [SerializeField] private AnimationClip bodyEndClip;
    [SerializeField, Min(1f)] private float fallbackFrameRate = 12f;
    [SerializeField, Min(0)] private int endDamageOffFrameIndex = 3;
    [SerializeField, Min(0.01f)] private float sourceBeamHeightUnits = 0.25f;
    [SerializeField, Min(0f)] private float bodyStartInset = 0.28f;
    [SerializeField] private int sortingOrder = 1;
    [SerializeField] private bool destroyOnComplete = true;

    private Coroutine playRoutine;

    public bool IsPlaying { get; private set; }
    public bool DamageActive { get; private set; }
    public bool EndActive { get; private set; }

    private void Awake()
    {
        ApplyRendererDefaults();
    }

    private void OnDisable()
    {
        DisableDamage();
        EndActive = false;
        IsPlaying = false;
    }

    public void Play(Vector2 origin, Vector2 direction, float length, float width, float damageHoldSeconds)
    {
        Play(origin, direction, length, width, damageHoldSeconds, DefaultBodyVisualLengthMultiplier);
    }

    public void Play(
        Vector2 origin,
        Vector2 direction,
        float length,
        float width,
        float damageHoldSeconds,
        float bodyVisualLengthMultiplier)
    {
        ApplyRendererDefaults();
        ConfigureGeometry(origin, direction, length, width, bodyVisualLengthMultiplier);

        if (playRoutine != null)
            StopCoroutine(playRoutine);

        playRoutine = StartCoroutine(CoPlay(Mathf.Max(0f, damageHoldSeconds)));
    }

    public void EnableDamage()
    {
        DamageActive = true;
    }

    public void DisableDamage()
    {
        DamageActive = false;
    }

    private IEnumerator CoPlay(float damageHoldSeconds)
    {
        IsPlaying = true;
        EndActive = false;
        DisableDamage();

        yield return CoPlayStartClips();
        EnableDamage();
        PlayIdleClips();

        float elapsed = 0f;
        while (elapsed < damageHoldSeconds)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        yield return CoPlayEndClips();

        DisableDamage();
        IsPlaying = false;
        playRoutine = null;

        if (destroyOnComplete)
            Destroy(gameObject);
    }

    private IEnumerator CoPlayStartClips()
    {
        float duration = Mathf.Max(GetClipLength(startStartClip), GetClipLength(bodyStartClip));
        PlayAnimatorState(startAnimator, StartStateName);
        PlayAnimatorState(bodyAnimator, StartStateName);

        if (duration <= 0f)
            yield break;

        yield return new WaitForSeconds(duration);
    }

    private void PlayIdleClips()
    {
        PlayAnimatorState(startAnimator, IdleStateName);
        PlayAnimatorState(bodyAnimator, IdleStateName);
    }

    private IEnumerator CoPlayEndClips()
    {
        float duration = Mathf.Max(GetClipLength(startEndClip), GetClipLength(bodyEndClip));
        float frameRate = ResolveFrameRate(startEndClip, bodyEndClip);
        float damageOffTime = Mathf.Max(0, endDamageOffFrameIndex) / frameRate;
        EndActive = true;
        PlayAnimatorState(startAnimator, EndStateName);
        PlayAnimatorState(bodyAnimator, EndStateName);

        if (duration <= 0f)
        {
            DisableDamage();
            EndActive = false;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            if (elapsed >= damageOffTime)
                DisableDamage();

            elapsed += Time.deltaTime;
            yield return null;
        }

        DisableDamage();
        EndActive = false;
    }

    private void ConfigureGeometry(Vector2 origin, Vector2 direction, float length, float width, float bodyVisualLengthMultiplier)
    {
        Vector2 safeDirection = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right;
        float safeLength = Mathf.Max(0.01f, length);
        float safeWidth = Mathf.Max(0.01f, width);
        float safeBodyLengthMultiplier = Mathf.Max(0.01f, bodyVisualLengthMultiplier);
        float rotationDeg = Mathf.Atan2(safeDirection.y, safeDirection.x) * Mathf.Rad2Deg;
        float visualYScale = ResolveVisualYScale(safeWidth);

        transform.SetPositionAndRotation(
            new Vector3(origin.x, origin.y, -0.05f),
            Quaternion.Euler(0f, 0f, rotationDeg));

        float startExtent = ResolveStartForwardExtent(visualYScale);
        float bodyOffset = Mathf.Max(0f, startExtent - bodyStartInset);
        float bodyLength = Mathf.Max(0.01f, safeLength - bodyOffset);
        float visualBodyLength = bodyLength * safeBodyLengthMultiplier;

        if (startRenderer != null)
        {
            startRenderer.transform.localPosition = Vector3.zero;
            startRenderer.transform.localRotation = Quaternion.identity;
            startRenderer.transform.localScale = new Vector3(visualYScale, visualYScale, 1f);
        }

        if (bodyRenderer != null)
        {
            bodyRenderer.drawMode = SpriteDrawMode.Tiled;
            bodyRenderer.size = new Vector2(visualBodyLength, ResolveBodySourceHeight());
            bodyRenderer.transform.localPosition = new Vector3(bodyOffset + visualBodyLength * 0.5f, 0f, 0f);
            bodyRenderer.transform.localRotation = Quaternion.identity;
            bodyRenderer.transform.localScale = new Vector3(1f, visualYScale, 1f);
        }
    }

    private void ApplyRendererDefaults()
    {
        ApplyRendererDefaults(startRenderer);
        ApplyRendererDefaults(bodyRenderer);
        ApplyAnimatorDefaults(startAnimator);
        ApplyAnimatorDefaults(bodyAnimator);

        if (bodyRenderer != null)
            bodyRenderer.drawMode = SpriteDrawMode.Tiled;
    }

    private void ApplyRendererDefaults(SpriteRenderer renderer)
    {
        if (renderer == null)
            return;

        renderer.enabled = true;
        DemonKingPrimitiveVisual.ApplyProjectileSorting(renderer, sortingOrder);
    }

    private static void ApplyAnimatorDefaults(Animator animator)
    {
        if (animator == null)
            return;

        animator.enabled = true;
        animator.speed = 1f;
    }

    private float ResolveVisualYScale(float targetWidth)
    {
        return Mathf.Max(0.01f, targetWidth / Mathf.Max(0.01f, sourceBeamHeightUnits));
    }

    private float ResolveBodySourceHeight()
    {
        Sprite sprite = bodyRenderer != null ? bodyRenderer.sprite : null;
        return sprite != null ? Mathf.Max(0.001f, sprite.bounds.size.y) : 1f;
    }

    private float ResolveStartForwardExtent(float visualYScale)
    {
        Sprite sprite = startRenderer != null ? startRenderer.sprite : null;
        if (sprite == null)
            return 0f;

        return Mathf.Max(0f, sprite.bounds.extents.x * visualYScale);
    }

    private static void PlayAnimatorState(Animator animator, string stateName)
    {
        if (animator == null || animator.runtimeAnimatorController == null)
            return;

        animator.Play(stateName, 0, 0f);
        animator.Update(0f);
    }

    private static float GetClipLength(AnimationClip clip)
    {
        return clip != null ? Mathf.Max(0f, clip.length) : 0f;
    }

    private float ResolveFrameRate(AnimationClip first, AnimationClip second)
    {
        if (first != null && first.frameRate > 0f)
            return first.frameRate;

        if (second != null && second.frameRate > 0f)
            return second.frameRate;

        return Mathf.Max(1f, fallbackFrameRate);
    }
}
