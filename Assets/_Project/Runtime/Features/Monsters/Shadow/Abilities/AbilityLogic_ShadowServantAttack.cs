using System.Collections;
using CapstoneAudio;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - ShadowServant 공격 실행의 공식 ASC 진입점이 되어 runner 시작과 취소 정리를 연결한다.
/// - 복잡한 경고/대기/폭발 시퀀스는 runner에 위임하고, 이 로직은 생명주기 연결만 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_ShadowServantAttack", menuName = "GAS/Ability Logic/Shadow Servant Attack")]
public class AbilityLogic_ShadowServantAttack : AbilityLogic
{
    /// <summary>
    /// 책임 :
    /// - ShadowServant 공격 패턴 1회 실행에 필요한 고정 데이터(경고 시간, 피해, 연출 자산)를 한 묶음으로 보관한다.
    /// - FSM 상태나 runner가 이 패턴의 실행 수치를 owner 필드가 아니라 AL 자산에서 읽게 하는 기준 컨테이너 역할을 한다.
    /// </summary>
    [System.Serializable]
    public struct PatternData
    {
        public GameObject fogPrefab;
        public GE_Damage_Spec damageEffect;
        public float damageAmount;
        public float warningDuration;
        public float warningBlinkStartNormalized;
        public float warningBlinkFrequency;
        public float warningBlinkAlphaMin;
        public GameObject attackEffectPrefab;
        public Vector3 attackEffectLocalOffset;
        public float attackEffectLifetimeSeconds;
        public Vector3 attackEffectScaleMultiplier;
        public float attackEffectRotationOffsetZ;
        public GameObject attackParticlePrefab;
        public Vector3 attackParticleLocalOffset;
        public float attackParticleLifetimeOverrideSeconds;
        public bool useUnscaledAttackParticleTime;
        public Vector3 attackParticleScaleMultiplier;
        public float attackParticleRotationOffsetZ;
        public SoundRef attackSound;
        public CameraShakeHook attackCameraShake;
    }

    [Header("Pattern Data")]
    [SerializeField] private PatternData patternData;

    public PatternData Data => patternData;

    /// <summary>
    /// 책임 :
    /// - 인스펙터 자산이 없는 런타임 생성 ability에서도 동일한 패턴 데이터를 사용할 수 있게 초기화 데이터를 주입한다.
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

        ShadowServantAttackRunner runner = system.GetComponent<ShadowServantAttackRunner>();
        if (runner == null)
            yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null)
            return;

        ShadowServantAttackRunner runner = system.GetComponent<ShadowServantAttackRunner>();
        runner?.Cancel();
    }
}
