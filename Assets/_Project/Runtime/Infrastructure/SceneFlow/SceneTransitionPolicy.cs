using System;

[Serializable]
public struct SceneTransitionPolicy
{
    public bool fullyHealPlayer;
    public bool resetCooldowns;
    public bool clearAllEffects;
    public bool clearCombatOnlyEffects;

    public SceneTransitionPolicy(
        bool fullyHealPlayer,
        bool resetCooldowns,
        bool clearAllEffects,
        bool clearCombatOnlyEffects)
    {
        this.fullyHealPlayer = fullyHealPlayer;
        this.resetCooldowns = resetCooldowns;
        this.clearAllEffects = clearAllEffects;
        this.clearCombatOnlyEffects = clearCombatOnlyEffects;
    }

    public void ApplyTo(SceneTransitionContext context)
    {
        if (context == null)
            return;

        context.fullyHealPlayer = fullyHealPlayer;
        context.resetCooldowns = resetCooldowns;
        context.clearAllEffects = clearAllEffects;
        context.clearCombatOnlyEffects = clearCombatOnlyEffects;
    }

    public override string ToString()
    {
        return $"Heal={fullyHealPlayer}, ResetCooldowns={resetCooldowns}, ClearAll={clearAllEffects}, ClearCombat={clearCombatOnlyEffects}";
    }
}
