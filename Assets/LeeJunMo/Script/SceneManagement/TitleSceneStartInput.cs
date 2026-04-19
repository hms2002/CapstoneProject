using UnityEngine;
using UnityEngine.SceneManagement;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[DisallowMultipleComponent]
public sealed class TitleSceneStartInput : MonoBehaviour
{
    [Header("Target Scene")]
    [SerializeField] private string targetSceneName = "ProtoTypeHub";

    [Header("Quick Start")]
    [SerializeField] private bool enableQuickStartShortcut;

    [Header("Input")]
    [SerializeField] private KeyCode startKey = KeyCode.Space;

    private bool isLoading;

    private void Update()
    {
        if (!enableQuickStartShortcut)
            return;

        if (isLoading || !WasStartPressed())
            return;

        TryStartGame();
    }

    private bool WasStartPressed()
    {
        if (Input.GetKeyDown(startKey))
            return true;

#if ENABLE_INPUT_SYSTEM
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return false;

        if (startKey == KeyCode.Space && keyboard.spaceKey.wasPressedThisFrame)
            return true;
#endif

        return false;
    }

    private void TryStartGame()
    {
        if (string.IsNullOrWhiteSpace(targetSceneName))
        {
            Debug.LogWarning("[TitleSceneStartInput] Target scene name is empty.", this);
            return;
        }

        isLoading = true;

        SceneFadeTransitionService transitionService = SceneFadeTransitionService.EnsureInstance();
        if (transitionService != null && transitionService.TryLoadScene(targetSceneName))
            return;

        SceneManager.LoadScene(targetSceneName);
    }
}
