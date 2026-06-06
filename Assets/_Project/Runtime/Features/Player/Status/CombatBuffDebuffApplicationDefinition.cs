using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 버프/디버프 적용에 필요한 정적 authoring 정보를 한 자산으로 묶는다.
/// - source owner가 실제 effect, HUD 정의, 수명 정책을 개별 필드로 흩뿌리지 않고 한 정의를 참조하게 만든다.
/// </summary>
[CreateAssetMenu(fileName = "NewCombatBuffDebuffDefinition", menuName = "Gameplay/Status/Combat Buff Debuff Definition")]
public sealed class CombatBuffDebuffApplicationDefinition : ScriptableObject
{
    [Header("Gameplay Effect")]
    [SerializeField] private GameplayEffect gameplayEffect;

    [Header("Status HUD")]
    [SerializeField] private StatusHudDefinition statusHudDefinition;
    [SerializeField] private bool showOnPlayerHud = true;

    [Header("Lifetime")]
    [SerializeField] private BuffDebuffLifetimePolicy lifetimePolicy = BuffDebuffLifetimePolicy.WhileSourceAlive;

    public GameplayEffect GameplayEffect => gameplayEffect;
    public StatusHudDefinition StatusHudDefinition => statusHudDefinition;
    public bool ShowOnPlayerHud => showOnPlayerHud;
    public BuffDebuffLifetimePolicy LifetimePolicy => lifetimePolicy;
}

/// <summary>
/// 책임 :
/// - 버프/디버프가 source 소멸에 종속되는지, 독립 duration으로 유지되는지 구분한다.
/// - 공용 적용 경로가 회수 모델과 독립 유지 모델을 정책 차이로 해석하게 만든다.
/// </summary>
public enum BuffDebuffLifetimePolicy
{
    WhileSourceAlive = 0,
    IndependentDuration = 1
}
