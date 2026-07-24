using UnityEngine;

/// <summary>
/// 책임 :
/// - 표식검 표식 스택의 시간 경과 감쇠 규칙을 persistent runtime data 바깥에서 적용한다.
/// - 비활성 슬롯에서도 표식이 자연스럽게 줄어들게 만들어 스택 유지 정책이 프리팹 생명주기와 분리되게 한다.
/// </summary>
public sealed class MarkSwordRuntimeProcessor : WeaponRuntimeProcessor
{
    public override void Tick(in WeaponRuntimeProcessContext context, float deltaTime)
    {
        if (deltaTime <= 0f || context.RuntimeData is not MarkSwordRuntimeData data)
            return;

        if (data.MarkStacks <= 0 || data.MarkDecaySeconds <= 0f)
            return;

        float remaining = data.MarkDecayRemaining - deltaTime;
        if (remaining > 0f)
        {
            data.SetMarkDecayRemaining(remaining);
            return;
        }

        data.ConsumeOneMarkStack();
    }
}
