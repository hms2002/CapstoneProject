using UnityEngine;

// [수정] TempPlayer -> SampleTopDownPlayer로 변경
public abstract class UpgradeEffectSO : ScriptableObject
{
    public abstract void ApplyEffect(SampleTopDownPlayer player);
}