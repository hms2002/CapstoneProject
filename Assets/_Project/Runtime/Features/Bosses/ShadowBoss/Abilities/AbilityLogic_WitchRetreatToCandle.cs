using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_Witch_RetreatToCandle", menuName = "GAS/Ability Logic/Witch Boss/AL_Witch_RetreatToCandle")]
    public class AbilityLogic_WitchRetreatToCandle : AbilityLogic
    {
        // 이 클래스의 책임:
        // 마녀 보스의 촛대로 후퇴 패턴 진입점과 해골 소환 튜닝 데이터를 제공한다.

        [Header("Retreat Skeleton")]
        [SerializeField] private DeadsSkeleton skeletonPrefab;
        [SerializeField] private Vector3 leftOffset = new Vector3(-0.5f, 0.2f, 0f);
        [SerializeField] private Vector3 rightOffset = new Vector3(0.5f, 0.2f, 0f);
        [SerializeField] private float skeletonExplosionDiameter = 6f;
        [SerializeField] private float skeletonSpeedScale = 1.5f;

        public DeadsSkeleton SkeletonPrefab => skeletonPrefab;
        public Vector3 LeftOffset => leftOffset;
        public Vector3 RightOffset => rightOffset;
        public float SkeletonExplosionDiameter => skeletonExplosionDiameter;
        public float SkeletonSpeedScale => skeletonSpeedScale;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
