using UnityEngine;

/// <summary>
/// 책임 :
/// - 태양도 열기 스택의 시간 경과 감쇠 규칙을 persistent runtime data 바깥에서 적용한다.
/// - 비활성 슬롯에서도 열기 상태가 같은 규칙으로 줄어들게 만들어 truly inventory-owned runtime data 구조를 유지한다.
/// </summary>
public sealed class SunBladeRuntimeProcessor : WeaponRuntimeProcessor
{
    public override void Tick(in WeaponRuntimeProcessContext context, float deltaTime)
    {
        if (deltaTime <= 0f || context.RuntimeData is not SunBladeRuntimeData data)
            return;

        if (data.HeatStacks <= 0 || data.HeatDecaySeconds <= 0f)
            return;

        float remaining = data.HeatDecayRemaining - deltaTime;
        if (remaining > 0f)
        {
            data.SetHeatDecayRemaining(remaining);
            return;
        }

        data.ConsumeOneHeatStack();
        Debug.Log($"[SunBladeRuntimeProcessor] Heat stack decayed: {data.HeatStacks}/{data.MaxHeatStacks}, decay={data.HeatDecayRemaining:0.00}s");
    }
}
