using UnityEngine;

public sealed class WitchRetreatPatternExecutor : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보스의 촛대로의 피난 패턴 1회 실행에서 강화 해골 소환과 부스트 적용을 전담한다.

    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    /// <summary>촛대로의 피난 패턴 실행을 시도합니다.</summary>
    public bool TryBeginPattern()
    {
        if (owner == null)
            owner = GetComponent<Witch>();

        if (owner == null)
        {
            Debug.LogWarning("[WitchRetreatPatternExecutor] 시작 실패: owner가 없습니다.", this);
            return false;
        }

        DeadsSkeleton skeletonPrefab = owner.ResolveRetreatSkeletonPrefabValue();
        if (skeletonPrefab == null)
        {
            Debug.LogWarning("[WitchRetreatPatternExecutor] 시작 실패: skeletonPrefab이 없습니다.", this);
            return false;
        }

        owner.PlayPatternAttackMotion();
        bool spawnedLeft = SpawnRetreatSkeleton(skeletonPrefab, owner.ResolveRetreatLeftOffsetValue());
        bool spawnedRight = SpawnRetreatSkeleton(skeletonPrefab, owner.ResolveRetreatRightOffsetValue());
        Debug.Log($"[WitchRetreatPatternExecutor] 피난 executor 경로 실행 결과: left={spawnedLeft}, right={spawnedRight}", this);
        return spawnedLeft || spawnedRight;
    }

    /// <summary>지정 오프셋 위치에 강화된 해골 하나를 소환합니다.</summary>
    private bool SpawnRetreatSkeleton(DeadsSkeleton skeletonPrefab, Vector3 localOffset)
    {
        if (owner == null || skeletonPrefab == null)
        {
            Debug.LogWarning(
                $"[WitchRetreatPatternExecutor] 소환 실패: owner={(owner != null)}, skeletonPrefab={(skeletonPrefab != null)}",
                this);
            return false;
        }

        DeadsSkeleton skeleton = Instantiate(
            skeletonPrefab,
            owner.transform.TransformPoint(localOffset),
            Quaternion.identity);

        if (skeleton == null)
        {
            Debug.LogWarning("[WitchRetreatPatternExecutor] 소환 실패: Instantiate 결과가 null입니다.", this);
            return false;
        }

        skeleton.SetBoost(
            owner.Target,
            owner.ResolveRetreatExplosionDiameterValue(),
            owner.ResolveRetreatSpeedScaleValue(),
            true);
        return true;
    }
}
