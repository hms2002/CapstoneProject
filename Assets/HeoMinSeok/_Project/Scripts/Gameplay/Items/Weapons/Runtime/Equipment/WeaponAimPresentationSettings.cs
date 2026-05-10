using UnityEngine;

[System.Serializable]
public sealed class WeaponAimPresentationSettings
{
    [SerializeField] private WeaponAimPresentationMode mode = WeaponAimPresentationMode.FollowAim;
    [SerializeField, Min(0f)] private float minimumHoldTime;

    public WeaponAimPresentationMode Mode => mode;
    public float MinimumHoldTime => Mathf.Max(0f, minimumHoldTime);
}
