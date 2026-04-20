using UnityEngine;

/// <summary>
/// 책임 :
/// - 처형총이 연 반격 검격 개방 창의 시간 경과 만료 규칙을 persistent runtime data 바깥에서 적용한다.
/// - 총을 비활성 슬롯으로 내려도 반격 창이 같은 규칙으로 닫히게 만들어 쌍무기 상태가 truly inventory-owned가 되게 한다.
/// </summary>
public sealed class ExecutionGunRuntimeProcessor : WeaponRuntimeProcessor
{
    public override void Tick(in WeaponRuntimeProcessContext context, float deltaTime)
    {
        if (deltaTime <= 0f || context.RuntimeData is not ExecutionGunRuntimeData data)
            return;

        if (!data.ReboundSlashReady || data.ReboundWindowSeconds <= 0f)
            return;

        float remaining = data.ReboundWindowRemaining - deltaTime;
        if (remaining > 0f)
        {
            data.SetReboundWindowRemaining(remaining);
            return;
        }

        data.CloseReboundSlashWindow();
    }
}
