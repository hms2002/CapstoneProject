/// <summary>
/// 책임 : ScenePortal 이동 직전에 평가할 부가 접근 조건과 실패 반응을 포탈 본체에서 분리한다.
/// </summary>
public interface IScenePortalAccessRule
{
    bool CanAccess(ScenePortal portal, IPlayerInteractor player);
    void HandleAccessDenied(ScenePortal portal, IPlayerInteractor player);
}
