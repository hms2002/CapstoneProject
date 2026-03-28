using System.Collections.Generic;
using UnityEngine;

public sealed class DialogueService : MonoBehaviour
{
    public static DialogueService Instance { get; private set; }

    private static bool s_isQuitting;

    private DialogueController activeController;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null || s_isQuitting)
            return;

        DialogueService existing = Object.FindFirstObjectByType<DialogueService>();
        if (existing != null)
            return;

        GameObject root = new GameObject(nameof(DialogueService));
        root.AddComponent<DialogueService>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationQuit()
    {
        s_isQuitting = true;
    }

    public bool IsPlaying => activeController != null && activeController.isPlaying;
    public bool HasActiveController => activeController != null;

    public void RegisterController(DialogueController controller)
    {
        if (controller == null)
            return;

        activeController = controller;
    }

    public void UnregisterController(DialogueController controller)
    {
        if (activeController == controller)
            activeController = null;
    }

    public bool TryStartDialogue(TextAsset inkJSON, List<NPCData> participants, NPCFeatureController featureController = null)
    {
        if (activeController == null)
        {
            Debug.LogError("[DialogueService] 현재 씬에 등록된 DialogueController가 없어 대화를 시작할 수 없습니다.");
            return false;
        }

        activeController.EnterDialogueMode(inkJSON, participants, featureController);
        return true;
    }
}
