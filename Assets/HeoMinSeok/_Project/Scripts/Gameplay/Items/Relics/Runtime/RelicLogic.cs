using UnityEngine;

public abstract class RelicLogic : ScriptableObject
{
    public abstract void OnEquipped(RelicContext ctx);
    public abstract void OnUnequipped(RelicContext ctx);
}
