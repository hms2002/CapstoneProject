using UnityEngine;

[CreateAssetMenu(fileName = "Effect_Empty", menuName = "Game/Upgrade Effects/Empty (Dummy)")]
public class EmptyEffectSO : UpgradeEffectSO
{
    [Header("Developer Memo")]
    [TextArea]
    public string memo = "효과가 아직 없는 더미 업그레이드입니다.\n기획 자리를 잡아두거나 추후 구현할 기능을 위해 비워둔 에셋입니다.";

    public override void ApplyOnPurchase(PlayerInteractor2D player)
    {
        Debug.Log($"[EmptyEffect] '{name}' effect ran, but it has no gameplay logic.");
    }
}
