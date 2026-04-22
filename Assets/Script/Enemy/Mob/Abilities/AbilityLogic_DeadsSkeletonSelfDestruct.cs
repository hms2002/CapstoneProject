using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - Dead'sSkeleton 자폭 패턴 실행의 공식 ASC 진입점이 되어 executor 시작과 취소 정리를 연결한다.
/// - 실제 인트로, armed, 접촉 폭발 시퀀스는 executor에 위임하고 이 로직은 생명주기 연결만 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_DeadsSkeletonSelfDestruct", menuName = "GAS/Ability Logic/DeadsSkeleton Self Destruct")]
public class AbilityLogic_DeadsSkeletonSelfDestruct : AbilityLogic
{
    /// <summary>
    /// 책임 :
    /// - Dead'sSkeleton 자폭 패턴 1회 실행에 필요한 고정 데이터(폭발 반경, 피해, 연출 자산)를 한 묶음으로 보관한다.
    /// - 상태 리듬과 무관한 자폭 실행 데이터가 owner 필드가 아니라 AL 자산에 머물도록 하는 기준 컨테이너 역할을 한다.
    /// </summary>
    [System.Serializable]
    public struct PatternData
    {
        public float explosionDiameter;
        public GE_Damage_Spec damageEffect;
        public float damageAmount;
        public GameObject explosionVisualPrefab;
        public GameObject explosionParticlePrefab;
        public Vector3 explosionVisualOffset;
        public Vector3 explosionParticleOffset;
        public Vector3 explosionVisualScale;
        public Vector3 explosionParticleScale;
        public SoundRef explosionSound;
        public CameraShakeHook explosionCameraShake;
    }

    [Header("Pattern Data")]
    [SerializeField] private PatternData patternData;

    public PatternData Data => patternData;

    /// <summary>
    /// 책임 :
    /// - 인스펙터 자산이 없는 런타임 생성 ability에서도 동일한 자폭 패턴 데이터를 사용할 수 있게 초기화 데이터를 주입한다.
    /// - owner에 남아 있는 fallback authoring 값을 임시로 AL 쪽 표면으로 옮기는 마이그레이션 통로가 된다.
    /// </summary>
    public void SetPatternData(PatternData data)
    {
        patternData = data;
    }

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null)
            yield break;

        DeadsSkeletonSelfDestructPatternExecutor executor = system.GetComponent<DeadsSkeletonSelfDestructPatternExecutor>();
        if (executor == null)
            yield break;

        yield return executor.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null)
            return;

        DeadsSkeletonSelfDestructPatternExecutor executor = system.GetComponent<DeadsSkeletonSelfDestructPatternExecutor>();
        executor?.Cancel();
    }
}
