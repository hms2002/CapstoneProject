using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class BookWorldSpriteSequencePresentation : MonoBehaviour
{
    private const int BaseLayer = 0;
    private const string BaseLayerPrefix = "Base Layer.";

    [Header("References")]
    [SerializeField] private SpriteRenderer targetRenderer;
    [SerializeField] private Animator animator;

    [Header("Animator States")]
    [SerializeField] private string closedIdleStateName = "ClosedIdle";
    [SerializeField] private string openedIdleStateName = "OpenedIdle";
    [SerializeField] private string openStateName = "Open";
    [SerializeField] private string closeStateName = "Close";
    [SerializeField] private AnimationClip closedIdleClip;
    [SerializeField] private AnimationClip openedIdleClip;
    [SerializeField] private AnimationClip openClip;
    [SerializeField] private AnimationClip closeClip;

    [Header("Timing")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private bool playClosedIdleOnEnable = true;

    private Coroutine activeRoutine;
    private bool isOpen;

    private void Awake()
    {
        ResolveReferences();
        ConfigureAnimatorUpdateMode();
    }

    private void OnEnable()
    {
        if (playClosedIdleOnEnable)
            SnapClosed();
    }

    private void OnDisable()
    {
        StopActiveRoutine();
    }

    public void PlayOpen()
    {
        ResolveReferences();
        ConfigureAnimatorUpdateMode();

        if (animator == null || isOpen)
            return;

        StopActiveRoutine();
        isOpen = true;

        if (!CanStartRoutine())
        {
            PlayAnimatorState(openedIdleStateName);
            return;
        }

        activeRoutine = StartCoroutine(PlayOpenRoutine());
    }

    public void PlayClose()
    {
        ResolveReferences();
        ConfigureAnimatorUpdateMode();

        if (animator == null || !isOpen)
            return;

        StopActiveRoutine();
        isOpen = false;

        if (!CanStartRoutine())
        {
            PlayAnimatorState(closedIdleStateName);
            return;
        }

        activeRoutine = StartCoroutine(PlayCloseRoutine());
    }

    public void SnapClosed()
    {
        StopActiveRoutine();
        ResolveReferences();
        ConfigureAnimatorUpdateMode();
        isOpen = false;
        PlayAnimatorState(closedIdleStateName);
    }

    private IEnumerator PlayOpenRoutine()
    {
        if (PlayAnimatorState(openStateName))
            yield return WaitForClip(openClip);

        PlayAnimatorState(openedIdleStateName);
        activeRoutine = null;
    }

    private IEnumerator PlayCloseRoutine()
    {
        if (PlayAnimatorState(closeStateName))
            yield return WaitForClip(closeClip);

        PlayAnimatorState(closedIdleStateName);
        activeRoutine = null;
    }

    private bool PlayAnimatorState(string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        if (!HasState(animator, stateName))
            return false;

        animator.Play(stateName, BaseLayer, 0f);
        animator.Update(0f);
        return true;
    }

    private IEnumerator WaitForClip(AnimationClip clip)
    {
        if (clip == null || clip.length <= 0f)
            yield break;

        if (useUnscaledTime)
        {
            yield return new WaitForSecondsRealtime(clip.length);
        }
        else
        {
            yield return new WaitForSeconds(clip.length);
        }
    }

    private void ResolveReferences()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponent<SpriteRenderer>();

        if (animator == null)
            animator = GetComponent<Animator>();
    }

    private void ConfigureAnimatorUpdateMode()
    {
        if (animator != null)
            animator.updateMode = useUnscaledTime ? AnimatorUpdateMode.UnscaledTime : AnimatorUpdateMode.Normal;
    }

    private void StopActiveRoutine()
    {
        if (activeRoutine == null)
            return;

        StopCoroutine(activeRoutine);
        activeRoutine = null;
    }

    private bool CanStartRoutine()
    {
        return isActiveAndEnabled && gameObject.activeInHierarchy;
    }

    private static bool HasState(Animator animator, string stateName)
    {
        if (animator == null || string.IsNullOrWhiteSpace(stateName))
            return false;

        int shortHash = Animator.StringToHash(stateName);
        if (animator.HasState(BaseLayer, shortHash))
            return true;

        int qualifiedHash = Animator.StringToHash(BaseLayerPrefix + stateName);
        return animator.HasState(BaseLayer, qualifiedHash);
    }
}
