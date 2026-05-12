using UnityEngine;
using UnityGAS;
using System.Collections.Generic;

/// <summary>
/// 책임 : 플레이어 주위를 도는 깃털 오브젝트를 생성/해제하는 유물 로직이다.
/// 일반 장착과 복원 장착 모두 컨트롤러 등록은 필요하지만, 복원 시 즉시 추가 수치 효과를 새로 적용하지는 않는다.
/// </summary>
[CreateAssetMenu(menuName = "Game/Relic Logic/Feather Orbit (Managed)")]
public class RelicLogic_FeatherOrbit_Managed : RelicLogic
{
    protected override string DefaultEffectTemplate => "● 깃털 {feather_count}개가 플레이어 주위를 회전\n● 깃털 피해 계수 {damage_coef}\n● 회전 반경 {radius}\n● 재타격 기본 쿨다운 {hit_cooldown}";

    [Header("Prefabs")]
    public FeatherOrbitFeather featherPrefab;

    [Header("Damage")]
    public GameplayEffect damageEffect;
    public GE_Knockback_Spec knockbackEffect;
    public StatId attackStatId = StatId.AttackFinal;
    public float damageCoef = 1.0f;
    public float knockbackImpulse = 0f;
    public GameplayTag hitConfirmedTag;

    [Header("Orbit")]
    public int featherCount = 1;
    public float radius = 1.2f;
    [Tooltip("플레이어 루트 기준 공전 중심 local offset. 루트 피벗이 발밑이면 Y를 올려 몸 중앙으로 맞춥니다.")]
    public Vector2 orbitCenterLocalOffset = new(0f, 0.55f);
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
        EnableController(ctx);
    }

    public override void OnUnequipped(RelicContext ctx)
    {
        if (ctx.owner == null) return;
        var controller = ctx.owner.GetComponent<FeatherOrbitController>();
        if (controller == null) return;

        controller.DisableForToken(ctx.token);
    }

    public override void OnRestoreAttached(RelicContext ctx)
    {
        EnableController(ctx);
    }

    private void EnableController(RelicContext ctx)
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
            knockbackEffect = knockbackEffect,
            attackStatId = attackStatId,
            damageCoef = damageCoef,
            knockbackImpulse = knockbackImpulse,
            hitConfirmedTag = hitConfirmedTag,

            featherPrefab = featherPrefab,
            featherCount = Mathf.Max(1, featherCount),
            radius = radius,
            orbitCenterLocalOffset = orbitCenterLocalOffset,
            baseAngularSpeedDegPerSec = baseAngularSpeedDegPerSec,
            basePerTargetHitCooldown = Mathf.Max(0.01f, basePerTargetHitCooldown),

            moveSpeedFinalStatId = moveSpeedFinalStatId
        });

        controller.EnableForToken(ctx.token);
    }

    public override RelicTooltipData BuildTooltip(RelicDefinition definition, int previewLevel, ItemDetailContext ctx)
    {
        return BuildTemplatedTooltip(
            "● 깃털 {feather_count}개가 플레이어 주위를 회전\n● 깃털 피해 계수 {damage_coef}\n● 회전 반경 {radius}\n● 재타격 기본 쿨다운 {hit_cooldown}",
            new Dictionary<string, string>
            {
                ["feather_count"] = RelicTooltipFormatter.FormatUnsignedValueToken(featherCount, false),
                ["damage_coef"] = RelicTooltipFormatter.FormatUnsignedValueToken(damageCoef, false),
                ["radius"] = RelicTooltipFormatter.FormatUnsignedValueToken(radius, false),
                ["hit_cooldown"] = RelicTooltipFormatter.FormatSeconds(basePerTargetHitCooldown),
            });
    }
}
