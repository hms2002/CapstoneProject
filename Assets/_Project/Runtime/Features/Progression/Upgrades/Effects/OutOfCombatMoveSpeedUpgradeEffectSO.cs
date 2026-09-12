using UnityEngine;

[CreateAssetMenu(fileName = "Effect_OutOfCombatMoveSpeed", menuName = "Upgrade/Effect/Out Of Combat Move Speed")]
public sealed class OutOfCombatMoveSpeedUpgradeEffectSO : PlayerUpgradeEffectSO
{
    [SerializeField] private CombatBuffDebuffApplicationDefinition buff;
    protected override void ApplyToPlayer(PlayerInteractor2D player)
    {
        if (buff == null) return;
        var runtime = player.GetComponent<OutOfCombatMoveSpeedRuntime>();
        if (runtime == null) runtime = player.gameObject.AddComponent<OutOfCombatMoveSpeedRuntime>();
        runtime.Configure(buff);
    }
}
