using UnityEngine;

[DisallowMultipleComponent]
public class FogSightLock : MonoBehaviour
{
    // 이 클래스의 책임:
    // 플레이어에게 일정 시간 동안 시야 제한을 적용하고, 전역 시야 마스크 컨트롤러에 어둠 요청을 등록/해제한다.

    private float endTime;
    private bool isDark;
    private GlobalVisionMaskController visionMaskController;

    private void Update()
    {
        if (!isDark) return;

        if (Time.time < endTime) return;

        RestoreLight();
    }

    private void OnDestroy()
    {
        RestoreLight();
    }

    /// <summary>시야 제한 시간을 적용합니다.</summary>
    public void ApplyFog(float duration)
    {
        endTime = Time.time + Mathf.Max(0f, duration);

        if (isDark) return;

        SetDark();
    }

    /// <summary>전역 시야 마스크에 어둠 요청을 등록합니다.</summary>
    private void SetDark()
    {
        EnsureController();
        visionMaskController?.AcquireDarkness(this);

        isDark = true;
    }

    /// <summary>전역 시야 마스크에서 어둠 요청을 해제합니다.</summary>
    private void RestoreLight()
    {
        if (!isDark) return;

        EnsureController();
        visionMaskController?.ReleaseDarkness(this);

        isDark = false;
    }

    /// <summary>전역 시야 마스크 컨트롤러 참조를 확보합니다.</summary>
    private void EnsureController()
    {
        if (visionMaskController != null)
            return;

        visionMaskController = GlobalVisionMaskController.Instance;
        if (visionMaskController == null)
            visionMaskController = FindFirstObjectByType<GlobalVisionMaskController>();
    }
}
