using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class HitboxVisualAnimatorPlayer : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private RuntimeAnimatorController baseController;
    [SerializeField] private AnimationClip placeholderClip;
    [SerializeField] private AnimationClip clip;
    [SerializeField] private string stateName = "Play";
    [SerializeField, Min(0.01f)] private float speed = 1f;
    [SerializeField] private bool playOnEnable = true;
    [SerializeField] private bool restartOnEnable = true;
    [SerializeField] private bool destroyOnComplete;
    [SerializeField, Min(0f)] private float destroyDelayPadding;

    private AnimatorOverrideController overrideController;
    private RuntimeAnimatorController overrideBaseController;
    private Coroutine destroyRoutine;

    public bool DestroyOnComplete => destroyOnComplete;
    public float CurrentClipDuration => clip != null ? clip.length / Mathf.Max(0.01f, speed) : 0f;

    private void Awake()
    {
        ResolveReferences();
        ApplyClipOverride();
    }

    private void OnEnable()
    {
        ResolveReferences();
        ApplyClipOverride();

        if (playOnEnable)
            Play();
    }

    private void OnDisable()
    {
        if (destroyRoutine != null)
        {
            StopCoroutine(destroyRoutine);
            destroyRoutine = null;
        }
    }

    public void Play()
    {
        if (animator == null)
            return;

        animator.speed = speed;

        if (restartOnEnable && !string.IsNullOrWhiteSpace(stateName))
            animator.Play(stateName, 0, 0f);

        if (restartOnEnable)
            animator.Update(0f);

        if (destroyOnComplete && clip != null && isActiveAndEnabled)
        {
            if (destroyRoutine != null)
                StopCoroutine(destroyRoutine);

            destroyRoutine = StartCoroutine(CoDestroyAfterClip());
        }
    }

    public void PlayClip(AnimationClip nextClip)
    {
        if (nextClip != null)
            clip = nextClip;

        ResolveReferences();
        ApplyClipOverride();
        Play();
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void ApplyClipOverride()
    {
        if (animator == null || clip == null)
            return;

        RuntimeAnimatorController controller = baseController != null
            ? baseController
            : overrideBaseController != null
                ? overrideBaseController
                : animator.runtimeAnimatorController;

        if (controller == null)
            return;

        if (overrideController == null || overrideBaseController != controller)
        {
            overrideBaseController = controller;
            overrideController = new AnimatorOverrideController(controller);
        }

        string placeholderName = ResolvePlaceholderClipName(controller);
        if (string.IsNullOrWhiteSpace(placeholderName))
            return;

        overrideController[placeholderName] = clip;
        animator.runtimeAnimatorController = overrideController;
    }

    private string ResolvePlaceholderClipName(RuntimeAnimatorController controller)
    {
        if (placeholderClip != null)
            return placeholderClip.name;

        AnimationClip[] clips = controller != null ? controller.animationClips : null;
        return clips != null && clips.Length > 0 && clips[0] != null
            ? clips[0].name
            : null;
    }

    private IEnumerator CoDestroyAfterClip()
    {
        float duration = clip != null ? clip.length / Mathf.Max(0.01f, speed) : 0f;
        duration += destroyDelayPadding;

        if (duration > 0f)
            yield return new WaitForSeconds(duration);

        destroyRoutine = null;

        if (gameObject != null)
            Destroy(gameObject);
    }
}
