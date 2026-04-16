using UnityEngine;
using UnityGAS;

public sealed class WitchNormalAttack1PatternExecutor : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보스의 평타1 패턴 1회 실행에서 장판 배치, payload 생성, 타일 재생을 전담한다.

    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    /// <summary>평타1 장판 공격 실행을 시도합니다.</summary>
    public bool TryBeginPattern()
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

        WitchNormalAttack1Tile tilePrefab = owner.ResolveNormal1TilePrefabValue();
        GE_Damage_Spec damageEffect = owner.ResolveNormal1DamageEffectValue();
        if (tilePrefab == null || damageEffect == null)
        {
            Debug.LogWarning(
                $"[WitchNormalAttack1PatternExecutor] 시작 실패: tilePrefab={(tilePrefab != null)}, damageEffect={(damageEffect != null)}",
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
        Vector2 tileSize = owner.GetNormal1TileSizeValue();
        CombatHitPayload payload = owner.MakeNormal1PayloadValue();
        int tileCount = owner.GetNormal1CountValue();
        float intervalSeconds = owner.GetNormal1IntervalValue();
        float startTime = owner.GetNormal1StartTimeValue();

        for (int i = 0; i < tileCount; i++)
        {
            WitchNormalAttack1Tile tile = Instantiate(
                tilePrefab,
                owner.GetNormal1TilePointValue(aimDir, i),
                Quaternion.Euler(0f, 0f, angle));

            owner.RuntimeData.AddNormal1Tile(tile);
            tile.Play(
                owner.Target.gameObject,
                payload,
                tileSize,
                angle,
                i * intervalSeconds,
                startTime + (i * intervalSeconds));
        }

        Debug.Log($"[WitchNormalAttack1PatternExecutor] 평타1 executor 경로 실행 성공: tileCount={tileCount}, interval={intervalSeconds}", this);
        return true;
    }
}
