/// <summary>
/// 책임 : 씬 전환 중 다음 씬 복원 정책과 입장 지점을 전달하는 직렬화 DTO다.
/// </summary>
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
