using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 책임 : Gameplay 대화 요청자가 구체 대화 UI/service 구현 없이 대화 재생 상태와 시작 요청을 전달하게 하는 backend 계약이다.
/// </summary>
public interface IDialoguePlaybackBackend
{
    bool IsPlaying { get; }
    bool HasActiveController { get; }

    void AcquireNonDialogueUiSuppression(object owner, float fadeSeconds = -1f);
    void ReleaseNonDialogueUiSuppression(object owner, float fadeSeconds = -1f);
    void ReleaseNonDialogueUiSuppressionWithoutRestore(object owner);
    bool TryStartDialogue(TextAsset inkJSON, List<NPCData> participants, NPCFeatureController featureController = null);
    bool TryStartDialogue(TextAsset inkJSON, List<NPCData> participants, string startPath, NPCFeatureController featureController = null);
    bool TryStartDialogue(TextAsset inkJSON, List<NPCData> participants, NPCFeatureController featureController, string startPath);
    bool TryStartDialogueSequence(IReadOnlyList<DialogueStorySegment> storySegments, List<NPCData> participants, NPCFeatureController featureController = null);
    bool TryStartDialogueSequence(IReadOnlyList<DialogueStorySegment> storySegments, List<NPCData> participants, DialoguePresentationOptions presentationOptions);
    bool TryStartDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        NPCFeatureController featureController,
        DialoguePresentationOptions presentationOptions);
}

/// <summary>
/// 책임 : Gameplay 계층이 구체 대화 UI/service 구현을 참조하지 않고 현재 등록된 대화 backend를 호출하게 한다.
/// </summary>
public static class DialoguePlayback
{
    private static IDialoguePlaybackBackend backend;

    public static bool IsAvailable => backend != null;
    public static bool IsPlaying => backend != null && backend.IsPlaying;
    public static bool HasActiveController => backend != null && backend.HasActiveController;

    public static void RegisterBackend(IDialoguePlaybackBackend dialogueBackend)
    {
        backend = dialogueBackend;
    }

    public static void AcquireNonDialogueUiSuppression(object owner, float fadeSeconds = -1f)
    {
        backend?.AcquireNonDialogueUiSuppression(owner, fadeSeconds);
    }

    public static void ReleaseNonDialogueUiSuppression(object owner, float fadeSeconds = -1f)
    {
        backend?.ReleaseNonDialogueUiSuppression(owner, fadeSeconds);
    }

    public static void ReleaseNonDialogueUiSuppressionWithoutRestore(object owner)
    {
        backend?.ReleaseNonDialogueUiSuppressionWithoutRestore(owner);
    }

    public static bool TryStartDialogue(
        TextAsset inkJSON,
        List<NPCData> participants,
        NPCFeatureController featureController = null)
    {
        return backend != null &&
               backend.TryStartDialogue(inkJSON, participants, featureController);
    }

    public static bool TryStartDialogue(
        TextAsset inkJSON,
        List<NPCData> participants,
        string startPath,
        NPCFeatureController featureController = null)
    {
        return backend != null &&
               backend.TryStartDialogue(inkJSON, participants, startPath, featureController);
    }

    public static bool TryStartDialogue(
        TextAsset inkJSON,
        List<NPCData> participants,
        NPCFeatureController featureController,
        string startPath)
    {
        return backend != null &&
               backend.TryStartDialogue(inkJSON, participants, featureController, startPath);
    }

    public static bool TryStartDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        NPCFeatureController featureController = null)
    {
        return backend != null &&
               backend.TryStartDialogueSequence(storySegments, participants, featureController);
    }

    public static bool TryStartDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        DialoguePresentationOptions presentationOptions)
    {
        return backend != null &&
               backend.TryStartDialogueSequence(storySegments, participants, presentationOptions);
    }

    public static bool TryStartDialogueSequence(
        IReadOnlyList<DialogueStorySegment> storySegments,
        List<NPCData> participants,
        NPCFeatureController featureController,
        DialoguePresentationOptions presentationOptions)
    {
        return backend != null &&
               backend.TryStartDialogueSequence(storySegments, participants, featureController, presentationOptions);
    }
}
