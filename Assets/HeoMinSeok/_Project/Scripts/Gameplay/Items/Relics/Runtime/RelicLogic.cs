using UnityEngine;

/// <summary>
/// 책임 : 유물 장착/해제 생명주기에 반응하는 정적 로직의 공통 베이스다.
/// 일반 장착 경로와 복원 장착 경로를 분리해, 씬 복원 시 중복 효과 적용을 막는다.
/// </summary>
public abstract class RelicLogic : ScriptableObject
{
    public abstract void OnEquipped(RelicContext ctx);
    public abstract void OnUnequipped(RelicContext ctx);

    /// <summary>
    /// 책임 : 씬 복원 시 유물의 runtime hook만 다시 연결한다.
    /// modifier/effect/tag/ability를 새로 부여하지 않는 것이 원칙이다.
    /// </summary>
    public virtual void OnRestoreAttached(RelicContext ctx) { }

    /// <summary>
    /// 책임 : 복원용 runtime hook을 해제할 필요가 있을 때 사용한다.
    /// 기본 구현은 비워 두고, 필요한 유물만 override 한다.
    /// </summary>
    public virtual void OnRestoreDetached(RelicContext ctx) { }
}