using UnityEngine;

/// <summary>
/// 책임 :
/// - 월영도 냉기 스택의 시간 경과 감쇠 규칙을 persistent runtime data 바깥에서 적용한다.
/// - 비활성 슬롯에서도 냉기 상태가 같은 규칙으로 줄어들게 만들어 truly inventory-owned runtime data 구조를 유지한다.
/// </summary>
public sealed class MoonBladeRuntimeProcessor : WeaponRuntimeProcessor
{
    public override void Tick(in WeaponRuntimeProcessContext context, float deltaTime)
    {
        if (deltaTime <= 0f || context.RuntimeData is not MoonBladeRuntimeData data)
            return;

        if (data.ColdStacks <= 0 || data.ColdDecaySeconds <= 0f)
            return;

        float remaining = data.ColdDecayRemaining - deltaTime;
        if (remaining > 0f)
        {
            data.SetColdDecayRemaining(remaining);
            return;
        }

        data.ConsumeOneColdStack();
        Debug.Log($"[MoonBladeRuntimeProcessor] Cold stack decayed: {data.ColdStacks}/{data.MaxColdStacks}, decay={data.ColdDecayRemaining:0.00}s");
    }
}
