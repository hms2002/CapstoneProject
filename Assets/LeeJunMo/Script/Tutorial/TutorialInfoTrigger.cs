using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public sealed class TutorialInfoTrigger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private TutorialInfoPanel infoPanel;
    [SerializeField] private bool playerOnly = true;

    [Header("Pages")]
    [SerializeField] private string tutorialId;
    [SerializeField] private TutorialInfoPage[] pages;
    [FormerlySerializedAs("windowSprite")]
    [SerializeField] private Sprite tutorialPanelSprite;
    [SerializeField] private Sprite titleSprite;

    [Header("Completion")]
    [SerializeField] private bool triggerOnce = true;
    [SerializeField] private bool usePersistentCompletion = true;
    [SerializeField] private bool markCompletedOnClose = true;
    [SerializeField] private bool allowReplayWhenCompleted;

    [Header("Timing")]
    [SerializeField, Min(0f)] private float fireDelaySeconds;
    [SerializeField] private float holdSeconds;
    [SerializeField] private bool useUnscaledDelayTime = true;
    [SerializeField] private bool disableColliderAfterFire;

    [Header("Events")]
    [SerializeField] private UnityEvent onFired;
    [SerializeField] private UnityEvent onSkippedAlreadyCompleted;

    private Collider2D triggerCollider;
    private Coroutine pendingFireRoutine;
    private bool hasTriggeredThisSession;
    private bool missingPanelWarningLogged;

    private void Reset()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;

        infoPanel = FindTutorialInfoPanel();
    }

    private void Awake()
    {
        triggerCollider = GetComponent<Collider2D>();
        if (triggerCollider != null)
            triggerCollider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (playerOnly && !IsPlayerCollider(other))
            return;

        Fire();
    }

    public void Fire()
    {
        FireAfterDelay(fireDelaySeconds);
    }

    public void FireNow()
    {
        if (!CanFire())
            return;

        TutorialInfoPanel panel = ResolvePanel();
        if (panel == null)
            return;

        bool shown = panel.Show(BuildRequest());
        if (!shown)
        {
            onSkippedAlreadyCompleted?.Invoke();
            return;
        }

        hasTriggeredThisSession = true;
        if (disableColliderAfterFire && triggerCollider != null)
            triggerCollider.enabled = false;

        onFired?.Invoke();
    }

    public void FireAfterDelay(float seconds)
    {
        if (!CanFire())
            return;

        if (pendingFireRoutine != null)
            StopCoroutine(pendingFireRoutine);

        if (seconds <= 0f)
        {
            FireNow();
            return;
        }

        pendingFireRoutine = StartCoroutine(FireAfterDelayRoutine(seconds));
    }

    public void ResetRuntimeTrigger()
    {
        hasTriggeredThisSession = false;
        if (triggerCollider != null)
            triggerCollider.enabled = true;
    }

    private IEnumerator FireAfterDelayRoutine(float seconds)
    {
        float elapsed = 0f;
        while (elapsed < seconds)
        {
            elapsed += useUnscaledDelayTime ? Time.unscaledDeltaTime : Time.deltaTime;
            yield return null;
        }

        pendingFireRoutine = null;
        FireNow();
    }

    private TutorialInfoRequest BuildRequest()
    {
        return new TutorialInfoRequest
        {
            tutorialId = tutorialId,
            tutorialPanelSprite = this.tutorialPanelSprite,
            titleSprite = titleSprite,
            pages = pages,
            holdSeconds = holdSeconds,
            usePersistentCompletion = usePersistentCompletion,
            markCompletedOnClose = markCompletedOnClose,
            allowReplayWhenCompleted = allowReplayWhenCompleted
        };
    }

    private bool CanFire()
    {
        if (triggerOnce && hasTriggeredThisSession)
            return false;

        if (usePersistentCompletion &&
            !allowReplayWhenCompleted &&
            TutorialProgressStore.IsCompleted(tutorialId))
        {
            onSkippedAlreadyCompleted?.Invoke();
            return false;
        }

        return true;
    }

    private TutorialInfoPanel ResolvePanel()
    {
        if (infoPanel != null)
            return infoPanel;

        infoPanel = FindTutorialInfoPanel();
        if (infoPanel != null)
            return infoPanel;

        if (!missingPanelWarningLogged)
        {
            Debug.LogWarning("[TutorialInfoTrigger] Missing TutorialInfoPanel reference.", this);
            missingPanelWarningLogged = true;
        }

        return null;
    }

    private static TutorialInfoPanel FindTutorialInfoPanel()
    {
#if UNITY_2023_1_OR_NEWER
        return FindAnyObjectByType<TutorialInfoPanel>(FindObjectsInactive.Include);
#else
        return FindObjectOfType<TutorialInfoPanel>(true);
#endif
    }

    private static bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
            return false;

        if (other.GetComponentInParent<PlayerInteractor2D>() != null)
            return true;

        return other.CompareTag("Player");
    }

    private void OnDisable()
    {
        if (pendingFireRoutine != null)
        {
            StopCoroutine(pendingFireRoutine);
            pendingFireRoutine = null;
        }
    }
}
