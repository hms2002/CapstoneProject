using System.Collections;
using UnityEngine;

/// <summary>
/// 책임 :
/// - 보스의 그로기 상태를 감지해 스프라이트 색감, 펄스, 선택적 연출 루트를 함께 제어한다.
/// - 아트 리소스가 없어도 기본적인 "무너진 상태" 피드백을 코드만으로 제공한다.
/// - 선택적으로 연결된 진입/유지/종료 루트 토글을 통해 나중에 VFX 프리팹을 쉽게 얹을 수 있게 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class BossGroggyPresentation : MonoBehaviour
{
    [Header("Binding")]
    [SerializeField] private BossControllerBase targetBoss;

    [Header("Tint")]
    [SerializeField] private SpriteRenderer[] tintTargets;
    [SerializeField] private Color groggyTint = new Color(1f, 0.9f, 0.65f, 1f);

    [Header("Pulse")]
    [SerializeField] private Transform pulseTarget;
    [SerializeField] private bool pulseWhileGroggy = true;
    [SerializeField] private float pulseAmplitude = 0.045f;
    [SerializeField] private float pulseSpeed = 7f;

    [Header("Optional Roots")]
    [SerializeField] private GameObject enterEffectRoot;
    [SerializeField] private float enterEffectDuration = 0.35f;
    [SerializeField] private GameObject whileActiveRoot;
    [SerializeField] private GameObject exitEffectRoot;
    [SerializeField] private float exitEffectDuration = 0.35f;

    private Color[] originalColors;
    private Vector3 originalPulseScale = Vector3.one;
    private bool wasGroggy;
    private Coroutine enterRoutine;
    private Coroutine exitRoutine;

    private void Awake()
    {
        if (targetBoss == null)
            targetBoss = GetComponent<BossControllerBase>();

        if ((tintTargets == null || tintTargets.Length == 0) && targetBoss != null)
            tintTargets = targetBoss.GetComponentsInChildren<SpriteRenderer>(includeInactive: true);

        if (pulseTarget == null)
            pulseTarget = transform;

        CacheOriginalState();
        SetWhileActiveRoot(false);
        SetTransientRootActive(enterEffectRoot, false);
        SetTransientRootActive(exitEffectRoot, false);
    }

    private void OnEnable()
    {
        CacheOriginalState();
        RefreshImmediate();
    }

    private void Update()
    {
        bool isGroggy = targetBoss != null && targetBoss.HasGroggyTag();
        if (isGroggy != wasGroggy)
        {
            HandleGroggyStateChanged(isGroggy);
            wasGroggy = isGroggy;
        }

        if (isGroggy)
        {
            ApplyTint(groggyTint);
            UpdatePulse();
        }
        else
        {
            ApplyOriginalTint();
            RestorePulseScale();
        }
    }

    /// <summary>
    /// 책임 :
    /// - GameplayCue나 외부 시스템이 "그로기 지속 시작"을 명시적으로 알려줄 때 진입 연출을 강제로 동기화한다.
    /// - 상태 폴링과 별개로 즉시 enter/while 연출을 시작할 수 있는 공용 진입점이다.
    /// </summary>
    public void HandleCueAdded()
    {
        if (wasGroggy)
            return;

        HandleGroggyStateChanged(true);
        wasGroggy = true;
    }

    /// <summary>
    /// 책임 :
    /// - GameplayCue나 외부 시스템이 "그로기 지속 종료"를 명시적으로 알려줄 때 종료 연출을 강제로 동기화한다.
    /// - 상태 태그 제거와 프레임 타이밍이 어긋나도 exit 연출이 빠지지 않게 보장한다.
    /// </summary>
    public void HandleCueRemoved()
    {
        if (!wasGroggy)
            return;

        HandleGroggyStateChanged(false);
        wasGroggy = false;
    }

    /// <summary>
    /// 책임 :
    /// - 지속 중인 그로기 cue가 다시 갱신될 때 유지 루트와 색/스케일 상태를 즉시 다시 맞춘다.
    /// - 중복 Add/Refresh가 들어와도 연출 상태가 흔들리지 않도록 안전하게 재동기화한다.
    /// </summary>
    public void HandleCueRefreshed()
    {
        ApplyTint(groggyTint);
        SetWhileActiveRoot(true);

        if (enterRoutine != null)
        {
            StopCoroutine(enterRoutine);
            enterRoutine = null;
        }

        if (exitRoutine != null)
        {
            StopCoroutine(exitRoutine);
            exitRoutine = null;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 현재 연결 상태를 기준으로 즉시 연출 상태를 재동기화한다.
    /// - 씬 시작 직후 이미 그로기 상태가 붙어 있는 경우에도 UI/연출이 어긋나지 않게 한다.
    /// </summary>
    private void RefreshImmediate()
    {
        bool isGroggy = targetBoss != null && targetBoss.HasGroggyTag();
        wasGroggy = isGroggy;

        if (isGroggy)
        {
            ApplyTint(groggyTint);
            SetWhileActiveRoot(true);
        }
        else
        {
            ApplyOriginalTint();
            RestorePulseScale();
            SetWhileActiveRoot(false);
        }
    }

    /// <summary>
    /// 책임 :
    /// - 그로기 진입/종료 순간의 원샷 연출과 유지 루트 전환을 한곳에서 처리한다.
    /// - 루프 연출은 유지 루트가 담당하고, 원샷은 짧은 활성 토글로 재생 트리거만 보낸다.
    /// </summary>
    private void HandleGroggyStateChanged(bool isGroggy)
    {
        SetWhileActiveRoot(isGroggy);

        if (isGroggy)
        {
            if (exitRoutine != null)
            {
                StopCoroutine(exitRoutine);
                exitRoutine = null;
            }

            if (enterRoutine != null)
                StopCoroutine(enterRoutine);

            enterRoutine = StartCoroutine(PlayTransientRoot(enterEffectRoot, enterEffectDuration));
        }
        else
        {
            if (enterRoutine != null)
            {
                StopCoroutine(enterRoutine);
                enterRoutine = null;
            }

            if (exitRoutine != null)
                StopCoroutine(exitRoutine);

            exitRoutine = StartCoroutine(PlayTransientRoot(exitEffectRoot, exitEffectDuration));
        }
    }

    /// <summary>
    /// 책임 :
    /// - 초기 SpriteRenderer 색과 펄스 기준 스케일을 캐시한다.
    /// - 나중에 그로기 종료 시 원래 룩으로 안전하게 되돌리는 기준값이 된다.
    /// </summary>
    private void CacheOriginalState()
    {
        if (tintTargets != null)
        {
            originalColors = new Color[tintTargets.Length];
            for (int i = 0; i < tintTargets.Length; i++)
                originalColors[i] = tintTargets[i] != null ? tintTargets[i].color : Color.white;
        }

        if (pulseTarget != null)
            originalPulseScale = pulseTarget.localScale;
    }

    /// <summary>
    /// 책임 :
    /// - 그로기 동안 보스가 약해진 느낌이 들도록 부드러운 확대/축소 펄스를 적용한다.
    /// - 루프 애니메이션이 없는 보스도 정지 화면처럼 보이지 않게 기본 생동감을 더한다.
    /// </summary>
    private void UpdatePulse()
    {
        if (!pulseWhileGroggy || pulseTarget == null)
            return;

        float pulse = 1f + Mathf.Sin(Time.time * pulseSpeed) * pulseAmplitude;
        pulseTarget.localScale = originalPulseScale * pulse;
    }

    private void RestorePulseScale()
    {
        if (pulseTarget != null)
            pulseTarget.localScale = originalPulseScale;
    }

    private void ApplyTint(Color tint)
    {
        if (tintTargets == null)
            return;

        for (int i = 0; i < tintTargets.Length; i++)
        {
            if (tintTargets[i] == null)
                continue;

            tintTargets[i].color = tint;
        }
    }

    private void ApplyOriginalTint()
    {
        if (tintTargets == null || originalColors == null)
            return;

        int count = Mathf.Min(tintTargets.Length, originalColors.Length);
        for (int i = 0; i < count; i++)
        {
            if (tintTargets[i] == null)
                continue;

            tintTargets[i].color = originalColors[i];
        }
    }

    private void SetWhileActiveRoot(bool isActive)
    {
        if (whileActiveRoot != null)
            whileActiveRoot.SetActive(isActive);
    }

    private static void SetTransientRootActive(GameObject root, bool isActive)
    {
        if (root != null)
            root.SetActive(isActive);
    }

    /// <summary>
    /// 책임 :
    /// - 선택적으로 연결된 진입/종료 원샷 루트를 짧게 켰다가 끈다.
    /// - 같은 루트를 반복 재사용할 때도 매번 확실하게 다시 재생되도록 활성 상태를 초기화한다.
    /// </summary>
    private IEnumerator PlayTransientRoot(GameObject root, float duration)
    {
        if (root == null)
            yield break;

        root.SetActive(false);
        yield return null;
        root.SetActive(true);

        float resolvedDuration = Mathf.Max(0.01f, duration);
        yield return new WaitForSeconds(resolvedDuration);
        root.SetActive(false);
    }
}
