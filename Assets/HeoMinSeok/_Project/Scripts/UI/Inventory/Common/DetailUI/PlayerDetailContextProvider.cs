using UnityEngine;
using UnityGAS;
public interface IItemDetailContextProvider
{
    ItemDetailContext BuildContext();
}

public class PlayerDetailContextProvider : MonoBehaviour, IItemDetailContextProvider
{
    [SerializeField] private AttributeSet attributeSet; // 없으면 GetComponent로 찾음
    [SerializeField] private AbilitySystem abilitySystem;
    [SerializeField] private TagSystem tagSystem;
    [SerializeField] private GameplayEffectRunner effectRunner;
    private void Awake()
    {
        if (attributeSet == null) attributeSet = GetComponent<AttributeSet>();
        if (abilitySystem == null) abilitySystem = GetComponent<AbilitySystem>();
        if (tagSystem == null) tagSystem = GetComponent<TagSystem>();
        if (effectRunner == null) effectRunner = GetComponent<GameplayEffectRunner>();
    }

    public ItemDetailContext BuildContext()
    {
        return new ItemDetailContext
        {
            owner = gameObject,
            attributeSet = attributeSet,
            abilitySystem = abilitySystem,
            tagSystem = tagSystem,
            effectRunner = effectRunner
        };
    }
}
