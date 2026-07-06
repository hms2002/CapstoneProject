using System;

/// <summary>
/// 책임 : 활성 GameplayEffect의 복원 가능한 최소 런타임 상태를 담는 Core 저장 스냅샷 DTO다.
/// </summary>
[Serializable]
public sealed class ActiveGameplayEffectSnapshot
{
    public string effectId;
    public float remainingTime;
    public int stackCount;
}
