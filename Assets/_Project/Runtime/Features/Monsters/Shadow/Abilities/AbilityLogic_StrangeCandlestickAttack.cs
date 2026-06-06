using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - StrangeCandlestick 공격 실행의 공식 ASC 진입점이 되어 락온-발사 runner 실행을 연결한다.
/// - 실제 경고 유지, 취소, 발사 시퀀스는 runner에 위임하고 이 로직은 생명주기 연결만 담당한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_StrangeCandlestickAttack", menuName = "GAS/Ability Logic/Strange Candlestick Attack")]
public class AbilityLogic_StrangeCandlestickAttack : AbilityLogic
{
    /// <summary>
    /// 책임 :
    /// - StrangeCandlestick 발사 패턴 1회 실행에 필요한 고정 데이터(투사체, 피해, 락온 경고)를 한 묶음으로 보관한다.
    /// - helper와 runner가 owner 필드가 아니라 AL 자산을 기준으로 발사 패턴 수치를 읽게 하는 기준 컨테이너 역할을 한다.
    /// </summary>
    [System.Serializable]
    public struct PatternData
    {
        public GameObject projectilePrefab;
        public float projectileSpeed;
        public GE_Damage_Spec damageEffect;
        public float damageAmount;
        public float attackIntervalSeconds;
        public float lockOnDuration;
        public float lockOnLineWidth;
        public Color lockOnColor;
        public AttackTelegraphStyle lockOnStyleAsset;
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

        StrangeCandlestickAttackRunner runner = system.GetComponent<StrangeCandlestickAttackRunner>();
        if (runner == null)
            yield break;

        yield return runner.Run(system, spec, initialTarget);
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null)
            return;

        StrangeCandlestickAttackRunner runner = system.GetComponent<StrangeCandlestickAttackRunner>();
        runner?.Cancel();
    }
}
