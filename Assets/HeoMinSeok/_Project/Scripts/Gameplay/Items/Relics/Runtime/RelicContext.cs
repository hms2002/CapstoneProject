using UnityEngine;
using UnityGAS;

public struct RelicContext
{
    public GameObject owner;
    public AbilitySystem abilitySystem;
    public TagSystem tagSystem;
    public GameplayEffectRunner effectRunner;
    public AttributeSet attributeSet;

    // 유물 인스턴스(중복) 식별용 토큰
    public Object token;

    public T Get<T>() where T : Component => owner != null ? owner.GetComponent<T>() : null;
}
