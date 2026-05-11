using UnityEngine;

/// <summary>
/// 무기 공격 중 조준 프레젠테이션 고정 방식과 최소 유지 시간을 보관할 책임을 가집니다.
/// </summary>
[System.Serializable]
public sealed class WeaponAimPresentationSettings
{
    [SerializeField] private WeaponAimPresentationMode mode = WeaponAimPresentationMode.FollowAim;
    [SerializeField, Min(0f)] private float minimumHoldTime;

    public WeaponAimPresentationMode Mode => mode;
    public float MinimumHoldTime => Mathf.Max(0f, minimumHoldTime);
}
