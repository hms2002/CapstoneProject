using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// Unreal GAS의 FGameplayCueParameters 느낌.
    /// "누가/무엇으로/누구에게/어디서/얼마나"를 연출에 전달.
    /// </summary>
    public struct GameplayCueParams
    {
        public GameObject Instigator;    // 시전자
        public GameObject Causer;        // 원인(투사체/무기 등)
        public GameObject Target;        // 대상

        public Vector3 Position;         // 월드 위치
        public Vector3 Normal;           // 노멀(가능하면)

        public Object SourceObject;      // AbilityDefinition/WeaponData/RelicData 등
        public float Magnitude;          // 데미지/힐/스택/게이지 증가량 등(선택)

        public static GameplayCueParams FromTarget(GameObject target)
        {
            return new GameplayCueParams
            {
                Target = target,
                Position = target != null ? target.transform.position : Vector3.zero,
                Normal = Vector3.up
            };
        }
    }
}
