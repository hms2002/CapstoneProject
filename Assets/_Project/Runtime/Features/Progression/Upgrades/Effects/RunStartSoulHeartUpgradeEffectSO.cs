using UnityEngine;
using UnityGAS;

[CreateAssetMenu(fileName = "RunStartSoulHeartEffect", menuName = "Upgrade/Effect/Run Start Soul Heart")]
public sealed class RunStartSoulHeartUpgradeEffectSO : UpgradeEffectSO, IRunStartUpgradeEffect, IUpgradeRuntimeTargetEffect
{
    [SerializeField] private AttributeDefinition soulHeartAttribute;
    [SerializeField, Min(0f)] private float amount = 1f;

    public override UpgradeEffectKind EffectKind => UpgradeEffectKind.Player;

    public void ApplyAtRunStart(PlayerInteractor2D player)
    {
        UpgradeRuntimeTargetAccumulator accumulator = new UpgradeRuntimeTargetAccumulator();
        AccumulateRuntimeTarget(accumulator);
        accumulator.Apply(player, this);
    }

    public void AccumulateRuntimeTarget(UpgradeRuntimeTargetAccumulator accumulator)
    {
        if (accumulator == null)
            return;

        if (TryGetTarget(out AttributeDefinition attribute, out float targetAmount))
            accumulator.AddExactAttributeTarget(attribute, targetAmount);
    }

    public bool TryGetTarget(out AttributeDefinition attribute, out float targetAmount)
    {
        attribute = soulHeartAttribute;
        targetAmount = Mathf.Max(0f, amount);
        return attribute != null && targetAmount > 0f;
    }

    public static bool SetSoulHeartTarget(
        PlayerInteractor2D player,
        AttributeDefinition attribute,
        float targetAmount,
        Object source)
    {
        if (player == null || attribute == null)
            return false;

        AttributeSet attributeSet = player.GetComponent<AttributeSet>();
        if (attributeSet == null)
            return false;

        return attributeSet.TrySetCurrentValue(attribute, Mathf.Max(0f, targetAmount), source);
    }
}
