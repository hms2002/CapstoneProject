using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Provides the existing NPC interaction flow and a stable profile key to room introductions.</summary>
public interface INpcRoomIntroductionSource
{
    string IntroductionKey { get; }
    bool HandlesIntroductionCamera { get; }
    bool TryStartIntroduction(IPlayerInteractor player, Action<bool> onEnded);
}

/// <summary>Owns first-visit NPC focus, dialogue completion persistence, and temporary camera/input cleanup.</summary>
public sealed class NpcRoomIntroduction : MonoBehaviour
{
    private readonly List<MonoBehaviour> sources = new();
    private Coroutine routine;
    private bool playerInside;
    private IGameplayCameraFocusSession cameraSession;
    private PlayerCinematicProtection protection;
    private GameFlowInputBlocker inputBlocker;
    private int generation;

    public void Configure(IReadOnlyList<GameObject> roomObjects)
    {
        sources.Clear();
        foreach (GameObject roomObject in roomObjects)
        {
            if (roomObject == null) continue;
            foreach (MonoBehaviour component in roomObject.GetComponentsInChildren<MonoBehaviour>(true))
                if (component is INpcRoomIntroductionSource && !sources.Contains(component))
                    sources.Add(component);
        }
    }

    public void NotifyEntered(PlayerInteractor2D player)
    {
        playerInside = true;
        if (routine == null && sources.Count > 0)
            routine = StartCoroutine(PlayIntroductions(player));
    }

    public void NotifyExited() => playerInside = false;

    private IEnumerator PlayIntroductions(PlayerInteractor2D player)
    {
        // Let room entry and scene-arrival systems acquire their locks first.
        yield return null;
        int token = ++generation;
        try
        {
            foreach (MonoBehaviour component in sources)
            {
                if (!playerInside || player == null) yield break;
                if (component == null || !component.isActiveAndEnabled) continue;
                var source = (INpcRoomIntroductionSource)component;
                string key = source.IntroductionKey;
                if (string.IsNullOrWhiteSpace(key)) continue;

                while (playerInside && player != null &&
                       (GameDataStore.Data == null || player.CurrentState != InteractState.Idle || DialoguePlayback.IsPlaying))
                    yield return null;
                if (!playerInside || player == null) yield break;

                GameData profile = GameDataStore.Data;
                if (profile.completedNpcRoomIntroductions?.Contains(key) == true) continue;
                if (!(component is IInteractable interactable)) continue;
                float readyDeadline = Time.realtimeSinceStartup + 5f;
                while (playerInside && player != null && component != null && component.isActiveAndEnabled &&
                       !interactable.CanInteract(player) && Time.realtimeSinceStartup < readyDeadline)
                    yield return null;
                if (!playerInside || player == null) yield break;
                if (component == null || !component.isActiveAndEnabled || !interactable.CanInteract(player)) continue;

                if (!source.HandlesIntroductionCamera)
                {
                    protection = player.GetComponent<PlayerCinematicProtection>();
                    if (protection == null) protection = player.gameObject.AddComponent<PlayerCinematicProtection>();
                    protection.Acquire(this);
                    inputBlocker = GameFlowInputBlocker.GetOrAdd(this);
                    inputBlocker.Acquire();
                    UiCommandPlayback.HideWorldPrompt();
                    cameraSession = GameplayCameraFocusPlayback.Capture(this);
                    cameraSession?.SetTarget(component.transform);
                    yield return new WaitForSecondsRealtime(0.35f);
                    if (cameraSession != null) yield return cameraSession.WaitForSettle(component.transform);
                    ReleaseFocusInput();
                }

                if (!playerInside || component == null || !component.isActiveAndEnabled ||
                    player == null || !ReferenceEquals(profile, GameDataStore.Data)) yield break;

                bool ended = false;
                bool completed = false;
                bool started = source.TryStartIntroduction(player, success =>
                {
                    if (token != generation) return;
                    ended = true;
                    completed = success;
                });
                while (started && !ended && component != null && component.isActiveAndEnabled &&
                       player != null && ReferenceEquals(profile, GameDataStore.Data))
                    yield return null;

                RestoreCamera();
                if (started && ended && completed && ReferenceEquals(profile, GameDataStore.Data))
                {
                    profile.completedNpcRoomIntroductions ??= new List<string>();
                    if (!profile.completedNpcRoomIntroductions.Contains(key))
                    {
                        profile.completedNpcRoomIntroductions.Add(key);
                        GameDataStore.RequestImmediateSave(this);
                    }
                }
            }
        }
        finally
        {
            ReleaseFocusInput();
            RestoreCamera();
            routine = null;
        }
    }

    private void ReleaseFocusInput()
    {
        if (protection != null) protection.Release(this);
        protection = null;
        if (inputBlocker != null) inputBlocker.Release();
    }

    private void RestoreCamera()
    {
        cameraSession?.Restore(PlayerRuntimeRegistry.GetPlayerTransform());
        cameraSession = null;
    }

    private void OnDisable()
    {
        generation++;
        playerInside = false;
        if (routine != null) StopCoroutine(routine);
        routine = null;
        ReleaseFocusInput();
        RestoreCamera();
    }
}
