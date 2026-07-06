using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
/// <summary>
/// 책임 : 플레이어 트리거 진입 또는 명시 호출 시 튜토리얼 안내 패널 표시 요청을 만든다.
/// </summary>
public sealed class TutorialInfoTrigger : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private MonoBehaviour infoPanel;
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

        infoPanel = FindTutorialInfoPanelBehaviour();
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

        ITutorialInfoPanel panel = ResolvePanel();
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

    private ITutorialInfoPanel ResolvePanel()
    {
        if (TryGetPanel(infoPanel, out ITutorialInfoPanel panel))
            return panel;

        MonoBehaviour foundPanel = FindTutorialInfoPanelBehaviour();
        if (TryGetPanel(foundPanel, out panel))
        {
            infoPanel = foundPanel;
            return panel;
        }

        if (!missingPanelWarningLogged)
        {
            Debug.LogWarning("[TutorialInfoTrigger] Missing tutorial info panel reference.", this);
            missingPanelWarningLogged = true;
        }

        return null;
    }

    private static bool TryGetPanel(MonoBehaviour source, out ITutorialInfoPanel panel)
    {
        panel = source as ITutorialInfoPanel;
        return panel != null;
    }

    private static MonoBehaviour FindTutorialInfoPanelBehaviour()
    {
#if UNITY_2023_1_OR_NEWER
        MonoBehaviour[] behaviours = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
#else
        MonoBehaviour[] behaviours = FindObjectsOfType<MonoBehaviour>(true);
#endif
        foreach (MonoBehaviour behaviour in behaviours)
        {
            if (behaviour is ITutorialInfoPanel)
                return behaviour;
        }

        return null;
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
