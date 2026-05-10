/// <summary>
/// 무기 프레젠테이션이 공격 중 조준 방향을 따라갈지, 시전 시점 기준으로 고정할지 결정할 책임을 가집니다.
/// </summary>
public enum WeaponAimPresentationMode
{
    FollowAim = 0,
    FacingSideOnly = 1,
    LockedAtCast = 2
}
