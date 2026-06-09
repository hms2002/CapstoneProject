using System.Collections;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - Bishop 직선 마법의 authoring 데이터를 소유하고, 실행은 BishopLineBlastRunner에 위임한다.
/// - 패턴 취소/씬 전환 시 Runner 정리 경로를 호출한다.
/// </summary>
[CreateAssetMenu(fileName = "AL_BishopLineBlast", menuName = "GAS/Ability Logic/Bishop Line Blast")]
public class AbilityLogic_BishopLineBlast : AbilityLogic
{
    [Header("Blast Effect")]
    [SerializeField] private GameObject blastEffectPrefab;
    [SerializeField, Min(0f)] private float blastEffectScaleMultiplier = 1f;
    [SerializeField] private bool alignBlastEffectToLine;
    [SerializeField, Min(0.05f)] private float fallbackBlastEffectLifetime = 1f;
    [SerializeField] private GameObject blastParticlePrefab;
    [SerializeField] private Vector3 blastParticleOffset;
    [SerializeField, Min(0f)] private float blastParticleLifetimeSeconds = 2f;

    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        if (system == null) yield break;

        BishopLineBlastRunner runner = system.GetComponent<BishopLineBlastRunner>();
        if (runner == null) yield break;

        yield return runner.Run(system, spec, initialTarget, CreateBlastEffectConfig());
    }

    public override void CleanupForSceneTransition(AbilitySystem system, AbilitySpec spec, GameObject target)
    {
        if (system == null) return;

        BishopLineBlastRunner runner = system.GetComponent<BishopLineBlastRunner>();
        runner?.Cancel();
    }

    /// <summary>AL authoring 값을 Runner가 사용할 수 있는 불변 실행 설정으로 변환합니다.</summary>
    private BishopLineBlastRunner.BlastEffectConfig CreateBlastEffectConfig()
    {
        return new BishopLineBlastRunner.BlastEffectConfig(
            blastEffectPrefab,
            blastEffectScaleMultiplier,
            alignBlastEffectToLine,
            fallbackBlastEffectLifetime,
            blastParticlePrefab,
            blastParticleOffset,
            blastParticleLifetimeSeconds);
    }
}
