using UnityEngine;
using UnityGAS;

public struct RelicContext
{
    public GameObject owner;
    public AbilitySystem abilitySystem;
    public TagSystem tagSystem;
    public GameplayEffectRunner effectRunner;
    public AttributeSet attributeSet;

    // 현재 적용 중인 유물 정보
    public RelicDefinition relicDef;
    public int level;

    // 유물 인스턴스(중복/강화) 식별용 토큰
    public Object token;

    public T Get<T>() where T : Component => owner != null ? owner.GetComponent<T>() : null;
}
