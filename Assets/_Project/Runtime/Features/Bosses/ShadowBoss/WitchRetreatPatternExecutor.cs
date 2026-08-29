using UnityEngine;

public sealed class WitchRetreatPatternExecutor : MonoBehaviour
{
    // 이 클래스의 책임:
    // 마녀 보스의 촛대로의 피난 패턴 1회 실행에서 강화 해골 소환과 부스트 적용을 전담한다.

    /// <summary>
    /// 책임 :
    /// - 촛대로의 피난 executor가 실행에 필요한 데이터를 Witch 바깥에서 한 번에 전달받도록 묶는다.
    /// - Witch가 retreat logic을 캐스팅해 값 조회 허브처럼 동작하지 않게 하고, executor는 이 문맥만 보고 해골을 소환한다.
    /// </summary>
    public readonly struct PatternContext
    {
        public readonly DeadsSkeleton SkeletonPrefab;
        public readonly Vector3 LeftOffset;
        public readonly Vector3 RightOffset;
        public readonly float ExplosionDiameter;
        public readonly float SpeedScale;

        public PatternContext(
            DeadsSkeleton skeletonPrefab,
            Vector3 leftOffset,
            Vector3 rightOffset,
            float explosionDiameter,
            float speedScale)
        {
            SkeletonPrefab = skeletonPrefab;
            LeftOffset = leftOffset;
            RightOffset = rightOffset;
            ExplosionDiameter = explosionDiameter;
            SpeedScale = speedScale;
        }
    }

    private Witch owner;

    private void Awake()
    {
        owner = GetComponent<Witch>();
    }

    /// <summary>촛대로의 피난 패턴 실행을 시도합니다.</summary>
    public bool TryBeginPattern(in PatternContext context)
    {
        if (owner == null)
            owner = GetComponent<Witch>();

        if (owner == null)
        {
            Debug.LogWarning("[WitchRetreatPatternExecutor] 시작 실패: owner가 없습니다.", this);
            return false;
        }

        if (context.SkeletonPrefab == null)
        {
            Debug.LogWarning("[WitchRetreatPatternExecutor] 시작 실패: skeletonPrefab이 없습니다.", this);
            return false;
        }

        owner.PlayPatternAttackMotion();
        bool spawnedLeft = SpawnRetreatSkeleton(context.SkeletonPrefab, context.LeftOffset, context.ExplosionDiameter, context.SpeedScale);
        bool spawnedRight = SpawnRetreatSkeleton(context.SkeletonPrefab, context.RightOffset, context.ExplosionDiameter, context.SpeedScale);
        Debug.Log($"[WitchRetreatPatternExecutor] 피난 executor 경로 실행 결과: left={spawnedLeft}, right={spawnedRight}", this);
        return spawnedLeft || spawnedRight;
    }

    /// <summary>지정 오프셋 위치에 강화된 해골 하나를 소환합니다.</summary>
    private bool SpawnRetreatSkeleton(DeadsSkeleton skeletonPrefab, Vector3 localOffset, float explosionDiameter, float speedScale)
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

        skeleton.SuppressMonsterLootDrop();
        skeleton.GetComponent<ExperienceRewardSource>()?.SetGrantExperience(false);
        skeleton.SetBoost(
            owner.Target,
            explosionDiameter,
            speedScale,
            true);
        owner.RegisterRetreatSummon(skeleton);
        return true;
    }
}
