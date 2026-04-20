using UnityEngine;

[DisallowMultipleComponent]
public class RestrictedVisionVisualController : MonoBehaviour
{
    // 이 클래스의 책임:
    // - 플레이어에게 일정 시간 동안 시야 차단 연출을 적용한다.
    // - 전역 시야 마스크 컨트롤러에 어둠 요청을 등록/해제하는 시각 효과 브리지 역할만 맡는다.

    [SerializeField] private bool logStatusUiFlow = true;
    [SerializeField] private GlobalVisionMaskController visionMaskController;

    private float endTime;
    private bool isDark;

    private void Awake()
    {
        EnsureController();
    }

    private void Update()
    {
        if (!isDark)
            return;

        if (Time.time < endTime)
            return;

        RestoreLight();
    }

    private void OnDestroy()
    {
        RestoreLight();
    }

    /// <summary>시야 차단 연출 시간을 적용합니다.</summary>
    public void ApplyFog(float duration)
    {
        float clampedDuration = Mathf.Max(0f, duration);
        endTime = Mathf.Max(endTime, Time.time + clampedDuration);

        if (logStatusUiFlow)
        {
            Debug.Log($"[RestrictedVisionVisualController] ApplyFog called. duration={clampedDuration:0.00}", this);
        }

        if (isDark)
            return;

        SetDark();
    }

    /// <summary>전역 시야 마스크에 어둠 요청을 등록합니다.</summary>
    private void SetDark()
    {
        EnsureController();
        visionMaskController?.AcquireDarkness(this);

        isDark = true;

        if (logStatusUiFlow)
            Debug.Log($"[RestrictedVisionVisualController] Darkness acquired. controller={(visionMaskController != null ? visionMaskController.name : "null")}", this);
    }

    /// <summary>전역 시야 마스크에서 어둠 요청을 해제합니다.</summary>
    private void RestoreLight()
    {
        if (!isDark) return;

        EnsureController();
        visionMaskController?.ReleaseDarkness(this);

        isDark = false;

        if (logStatusUiFlow)
            Debug.Log("[RestrictedVisionVisualController] Darkness released.", this);
    }

    /// <summary>전역 시야 마스크 컨트롤러 참조를 확보합니다.</summary>
    private void EnsureController()
    {
        if (visionMaskController != null)
            return;

        visionMaskController = GetComponent<GlobalVisionMaskController>();
        if (visionMaskController != null)
            return;

        visionMaskController = GlobalVisionMaskController.Instance;
        if (visionMaskController == null)
            visionMaskController = FindFirstObjectByType<GlobalVisionMaskController>();
    }
}
