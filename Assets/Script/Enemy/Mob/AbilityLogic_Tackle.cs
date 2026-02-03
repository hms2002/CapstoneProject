using System.Collections;
using UnityEngine;
using UnityGAS;

// 에디터 메뉴: Create > GAS > Ability Logic > Tackle Logic
[CreateAssetMenu(fileName = "AL_Tackle", menuName = "GAS/Ability Logic/Tackle Logic")]
public class AL_Tackle : AbilityLogic
{
    [Header("Tackle Settings")]
    [SerializeField] private GameplayEffect damageEffect;           // GE_MobContactDamage 등
    [SerializeField] private float          damageAmount = 10.0f;   // 데미지 수치

    public override IEnumerator Activate(AbilitySystem caster, AbilitySpec spec, GameObject target)
    {
        // 타겟이 없거나 데미지 이펙트가 없으면 취소
        if (target == null || damageEffect == null) yield break;

        // 1. 타겟의 Runner 가져오기
        GameplayEffectRunner targetRunner = target.GetComponent<GameplayEffectRunner>();

        if (targetRunner != null)
        {
            // 2. Spec 생성 (Source: 시전한 잡몹)
            GameplayEffectSpec effectSpec = caster.MakeSpec(damageEffect, caster.gameObject);

            // 3. 데미지 데이터 주입 (SetByCaller)
            int         damageTagId = TagRegistry.GetIdByPath("Data.Damage");
            GameplayTag damageTag   = TagRegistry.GetTag(damageTagId);

            if (damageTag != null)
            {
                effectSpec.SetSetByCallerMagnitude(damageTag, damageAmount);

                // 4. 타겟에게 적용 (Apply)
                targetRunner.ApplyEffectSpec(effectSpec, target);

                Debug.Log($"[GAS] {caster.name} hit {target.name} for {damageAmount}");
            }
            else
            {
                Debug.LogError($"[AL_Tackle] 'Data.Damage' 태그를 찾을 수 없습니다.");
            }
        }

        yield break;
    }
}