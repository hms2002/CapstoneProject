using UnityEngine;
using UnityGAS;

[CreateAssetMenu(menuName = "Game/Relic Logic/Feather Orbit (Managed)")]
public class RelicLogic_FeatherOrbit_Managed : RelicLogic
{
    [Header("Prefabs")]
    public FeatherOrbitFeather featherPrefab;

    [Header("Damage")]
    public GameplayEffect damageEffect;              // GE_Damage_Spec 권장
    public StatId attackStatId = StatId.AttackFinal;      // 프로젝트에 맞게
    public float damageCoef = 1.0f;                  // ATK * coef
    public float knockbackImpulse = 0f;              // SetByCaller(knockbackKey)로 들어감
    public GameplayTag hitConfirmedTag;              // 필요 없으면 비워둬도 됨

    [Header("Orbit")]
    public int featherCount = 1;
    public float radius = 1.2f;
    [Tooltip("이속 100%(x1)일 때의 회전 속도(도/초). 360이면 1초 1회전.")]
    public float baseAngularSpeedDegPerSec = 360f;

    [Header("Hit Rate")]
    [Tooltip("같은 적 재타격 기본 쿨다운(초). 실제 쿨다운 = base / MoveSpeedFinal")]
    public float basePerTargetHitCooldown = 0.25f;

    [Header("Move Speed Source")]
    [Tooltip("이속 배수(=x1)로 쓸 StatId. 보통 MoveSpeedFinal")]
    public StatId moveSpeedFinalStatId = StatId.MoveSpeedFinal;

    public override void OnEquipped(RelicContext ctx)
    {
        if (ctx.owner == null) return;
        if (featherPrefab == null) return;
        if (damageEffect == null) return;

        var controller = ctx.owner.GetComponent<FeatherOrbitController>();
        if (controller == null)
            controller = ctx.owner.AddComponent<FeatherOrbitController>();

        controller.Setup(new FeatherOrbitController.Config
        {
            owner = ctx.owner,
            token = ctx.token,
            damageEffect = damageEffect,
            attackStatId = attackStatId,
            damageCoef = damageCoef,
            knockbackImpulse = knockbackImpulse,
            hitConfirmedTag = hitConfirmedTag,

            featherPrefab = featherPrefab,
            featherCount = Mathf.Max(1, featherCount),
            radius = radius,
            baseAngularSpeedDegPerSec = baseAngularSpeedDegPerSec,
            basePerTargetHitCooldown = Mathf.Max(0.01f, basePerTargetHitCooldown),

            moveSpeedFinalStatId = moveSpeedFinalStatId
        });

        controller.EnableForToken(ctx.token);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null) return;
        var controller = ctx.owner.GetComponent<FeatherOrbitController>();
        if (controller == null) return;

        controller.DisableForToken(ctx.token);
    }
}
