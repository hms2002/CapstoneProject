using System.Collections.Generic;
using UnityEngine;

public sealed class DialogueService : MonoBehaviour
{
    public static DialogueService Instance { get; private set; }

    private static bool s_isQuitting;

    private DialogueController activeController;
    private bool wasDialoguePlaying;

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

    private void Update()
    {
        SyncRunTimerPauseState();
    }

    private void OnDestroy()
    {
        ReleaseRunTimerPause();

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
        return TryStartDialogue(inkJSON, participants, null, featureController);
    }

    public bool TryStartDialogue(
        TextAsset inkJSON,
        List<NPCData> participants,
        string startPath,
        NPCFeatureController featureController = null)
    {
        if (activeController == null)
        {
            Debug.LogError("[DialogueService] 현재 씬에 등록된 DialogueController가 없어 대화를 시작할 수 없습니다.");
            return false;
        }

        activeController.EnterDialogueMode(inkJSON, participants, featureController, startPath);
        SyncRunTimerPauseState();
        return true;
    }

    /// <summary>
    /// 책임 :
    /// - 대화 재생 상태 변화에 맞춰 런 제한 시간 외부 pause를 자동 동기화한다.
    /// - 개별 대화 연출 코드가 타이머 시스템을 직접 알지 않아도 되도록 공통 경계를 제공한다.
    /// </summary>
    private void SyncRunTimerPauseState()
    {
        bool isDialoguePlaying = IsPlaying;
        if (wasDialoguePlaying == isDialoguePlaying)
            return;

        wasDialoguePlaying = isDialoguePlaying;

        if (RunTimeLimitSystem.Instance == null)
            return;

        RunTimeLimitSystem.Instance.SetExternalPause(this, isDialoguePlaying);
    }

    /// <summary>
    /// 책임 :
    /// - DialogueService가 파괴되거나 비활성 흐름으로 빠질 때 남아 있을 수 있는 타이머 pause를 정리한다.
    /// - 대화 서비스 수명과 무관하게 런 타이머가 영구 정지되지 않도록 안전하게 복구한다.
    /// </summary>
    private void ReleaseRunTimerPause()
    {
        if (RunTimeLimitSystem.Instance == null)
            return;

        RunTimeLimitSystem.Instance.SetExternalPause(this, false);
        wasDialoguePlaying = false;
    }
}
