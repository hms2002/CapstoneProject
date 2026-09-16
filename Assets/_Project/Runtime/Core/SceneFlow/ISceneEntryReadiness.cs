/// <summary>
/// Reports scene-content readiness and permits retry without coupling scene transitions to Gameplay.
/// A failed producer must not be treated as ready merely because its work has finished.
/// </summary>
public interface ISceneEntryReadiness
{
    bool IsSceneEntryReady { get; }
    string SceneEntryFailure { get; }
    void RetrySceneEntry();
}
