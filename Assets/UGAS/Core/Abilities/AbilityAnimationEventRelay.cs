using UnityEngine;
using UnityGAS;

public class AbilityAnimationEventRelay : MonoBehaviour
{
    [SerializeField] private AbilitySystem abilitySystem;

    private void Awake()
    {
        abilitySystem ??= GetComponentInParent<AbilitySystem>();
    }

    public void Bind(AbilitySystem system) => abilitySystem = system;

    // Animation Event에서 호출 (태그 에셋 전달 버전)
    public void SendEvent(GameplayTag tag)
    {
        if (abilitySystem == null || tag == null) return;

        var data = new AbilityEventData
        {
            Instigator = abilitySystem.gameObject,
            Target = abilitySystem.CurrentTargetGameObject,
            Spec = abilitySystem.CurrentExecSpec ?? abilitySystem.CurrentCastSpec,
            WorldPosition = transform.position,
            // Causer를 "이 이벤트를 보낸 오브젝트(무기/플레이어)"로 두면 디버깅이 쉬움
            Causer = gameObject
        };

        abilitySystem.SendGameplayEvent(tag, data);
    }
}
