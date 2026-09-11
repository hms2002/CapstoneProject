using CapstoneAudio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Responsibility: request authored scene-entry music without depending on portals, routes, or run state.
/// An unset sound explicitly stops music; disabling/unloading never stops the next scene's music.
/// </summary>
[DefaultExecutionOrder(-1000)]
[DisallowMultipleComponent]
public sealed class SceneBgmRequester : MonoBehaviour
{
    [Tooltip("Music requested when this scene becomes active. An empty key means silence.")]
    [SerializeField] private SoundRef sceneMusic;

    private bool hasStarted;

    public SoundRef SceneMusic => sceneMusic;

    private void OnEnable()
    {
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;
        if (hasStarted)
            RequestMusic();
    }

    private void Start()
    {
        // Start runs after sceneLoaded bootstrap/title cleanup, but before normal gameplay Start callbacks.
        hasStarted = true;
        RequestMusic();
    }

    private void OnDisable()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        if (hasStarted && next.handle == gameObject.scene.handle)
            RequestMusic();
    }

    public bool RequestMusic()
    {
        if (!isActiveAndEnabled)
            return false;

        return sceneMusic.IsSet
            ? SoundPlaybackUtility.TryPlayMusic(sceneMusic, gameObject.scene)
            : SoundPlaybackUtility.TryStopMusic(gameObject.scene);
    }
}
