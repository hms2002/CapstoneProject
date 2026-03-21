[System.Serializable]
public sealed class SceneTransitionContext
{
    public string fromScene;
    public string toScene;

    public string exitPointId;
    public string entryPointId;

    public TransitionType transitionType = TransitionType.None;

    public bool fullyHealPlayer;
    public bool resetCooldowns;
    public bool clearAllEffects;
    public bool clearCombatOnlyEffects;
}