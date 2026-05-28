using UnityEngine;
using UnityGAS;

public sealed class WitchNormalAttack1PatternExecutor : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보스의 평타1 패턴 1회 실행에서 장판 배치, payload 생성, 타일 재생을 전담한다.

    /// <summary>
    /// 책임 :
    /// - 평타1 executor가 실행에 필요한 데이터를 Witch 바깥에서 한 번에 전달받도록 묶는다.
    /// - Witch가 패턴 로직을 캐스팅해 값 조회 허브처럼 동작하지 않게 하고, executor는 이 문맥만 보고 장판을 배치한다.
    /// </summary>
    public readonly struct PatternContext
    {
        public readonly WitchNormalAttack1Tile TilePrefab;
        public readonly GE_Damage_Spec DamageEffect;
        public readonly float DamageAmount;
        public readonly int TileCount;
        public readonly float IntervalSeconds;
        public readonly float HitDurationSeconds;
        public readonly Vector2 TileSize;
        public readonly AttackTelegraphStyle WarningTelegraphStyle;
        public readonly AttackTelegraphStyle HitTelegraphStyle;

        public PatternContext(
            WitchNormalAttack1Tile tilePrefab,
            GE_Damage_Spec damageEffect,
            float damageAmount,
            int tileCount,
            float intervalSeconds,
            float hitDurationSeconds,
            Vector2 tileSize,
            AttackTelegraphStyle warningTelegraphStyle,
            AttackTelegraphStyle hitTelegraphStyle)
        {
            TilePrefab = tilePrefab;
            DamageEffect = damageEffect;
            DamageAmount = damageAmount;
            TileCount = tileCount;
            IntervalSeconds = intervalSeconds;
            HitDurationSeconds = hitDurationSeconds;
            TileSize = tileSize;
            WarningTelegraphStyle = warningTelegraphStyle;
            HitTelegraphStyle = hitTelegraphStyle;
        }
    }

    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    /// <summary>평타1 장판 공격 실행을 시도합니다.</summary>
    public bool TryBeginPattern(in PatternContext context)
    {
        if (owner == null)
            owner = GetComponent<Witch>();

        if (owner == null || owner.AbilitySystem == null || owner.Target == null)
        {
            Debug.LogWarning(
                $"[WitchNormalAttack1PatternExecutor] 시작 실패: owner={(owner != null)}, abilitySystem={(owner != null && owner.AbilitySystem != null)}, target={(owner != null && owner.Target != null)}",
                this);
            return false;
        }

        if (context.TilePrefab == null || context.DamageEffect == null)
        {
            Debug.LogWarning(
                $"[WitchNormalAttack1PatternExecutor] 시작 실패: tilePrefab={(context.TilePrefab != null)}, damageEffect={(context.DamageEffect != null)}",
                this);
            return false;
        }

        Vector2 aimDir = owner.GetAimDirectionValue();
        if (aimDir == Vector2.zero)
        {
            Debug.LogWarning("[WitchNormalAttack1PatternExecutor] 시작 실패: aimDir가 zero입니다.", this);
            return false;
        }

        owner.RuntimeData.ClearNormal1Tiles();
        owner.PlayPatternAttackMotion();

        float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
        CombatHitPayload payload = owner.MakeNormal1Payload(
            context.DamageEffect,
            context.DamageAmount);
        float startTime = context.TileCount * context.IntervalSeconds;

        for (int i = 0; i < context.TileCount; i++)
        {
            Vector3 spawnPosition = owner.GetNormal1TilePoint(aimDir, i, context.TileSize);
            WitchNormalAttack1Tile tile = Instantiate(
                context.TilePrefab,
                spawnPosition,
                Quaternion.Euler(0f, 0f, angle));
            tile.name = $"{context.TilePrefab.name}_Tile{i}";

            owner.RuntimeData.AddNormal1Tile(tile);
            tile.Play(
                owner.Target.gameObject,
                payload,
                context.TileSize,
                angle,
                i * context.IntervalSeconds,
                startTime + (i * context.IntervalSeconds),
                context.WarningTelegraphStyle,
                context.HitTelegraphStyle,
                i);
        }

        Debug.Log($"[WitchNormalAttack1PatternExecutor] 평타1 executor 경로 실행 성공: tileCount={context.TileCount}, interval={context.IntervalSeconds}", this);
        return true;
    }
}
