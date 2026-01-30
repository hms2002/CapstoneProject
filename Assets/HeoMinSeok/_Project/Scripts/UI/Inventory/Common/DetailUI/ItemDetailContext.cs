using UnityEngine;
using UnityGAS;

/// <summary>
/// Detail panel context: allows showing values based on the current player state.
/// (e.g., final attack power after buffs)
/// </summary>
public sealed class ItemDetailContext
{
    public GameObject owner;
    public AbilitySystem abilitySystem;
    public TagSystem tagSystem;
    public GameplayEffectRunner effectRunner;
    public AttributeSet attributeSet;

    public static ItemDetailContext FromOwner(GameObject owner)
    {
        var ctx = new ItemDetailContext();
        ctx.owner = owner;
        if (owner != null)
        {
            ctx.abilitySystem = owner.GetComponent<AbilitySystem>();
            ctx.tagSystem = owner.GetComponent<TagSystem>();
            ctx.effectRunner = owner.GetComponent<GameplayEffectRunner>();
            ctx.attributeSet = owner.GetComponent<AttributeSet>();
        }
        return ctx;
    }
}
