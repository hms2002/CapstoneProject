using System;
using System.Collections.Generic;

public static class DialoguePresentationSequencer
{
    public static void PlayOpening(DialogueView view, CinematicDirector director, List<NPCData> participants, bool isBoss, Action onOpened)
    {
        PlayOpening(
            view,
            director,
            participants,
            isBoss,
            DialoguePresentationOptions.Default,
            null,
            onOpened);
    }

    public static void PlayOpening(
        DialogueView view,
        CinematicDirector director,
        List<NPCData> participants,
        bool isBoss,
        DialoguePresentationOptions presentationOptions,
        string openingPortraitLabel,
        Action onOpened)
    {
        if (view == null)
        {
            onOpened?.Invoke();
            return;
        }

        Action showDialogueUi = () =>
        {
            view.ShowUI(isBoss && !presentationOptions.ForceDialogueBoxOnly, onOpened);
        };

        if (presentationOptions.SuppressPortraitIntro)
        {
            showDialogueUi();
            return;
        }

        if (director == null)
        {
            showDialogueUi();
            return;
        }

        if (presentationOptions.UseFastSilhouetteIntro)
        {
            director.PlayFastSilhouetteIntro(
                GetPrimaryParticipant(participants),
                presentationOptions.ResolvedFastSilhouettePosition,
                presentationOptions.ResolvedFastSilhouetteFadeSeconds,
                presentationOptions.FastSilhouetteColorize,
                openingPortraitLabel,
                showDialogueUi);
            return;
        }

        Action playPortraitIntro = () => director.PlayIntro(participants, showDialogueUi, openingPortraitLabel);
        if (isBoss && !presentationOptions.SkipBossPrelude && !presentationOptions.ForceDialogueBoxOnly)
        {
            view.PlayBossPrelude(playPortraitIntro);
            return;
        }

        playPortraitIntro();
    }

    public static void PlayClosing(DialogueView view, CinematicDirector director, Action onClosed)
    {
        PlayClosing(view, director, DialoguePresentationOptions.Default, onClosed);
    }

    public static void PlayClosing(
        DialogueView view,
        CinematicDirector director,
        DialoguePresentationOptions presentationOptions,
        Action onClosed)
    {
        if (view == null)
        {
            onClosed?.Invoke();
            return;
        }

        view.HideUI(() =>
        {
            if (director == null || presentationOptions.SuppressPortraitOutro)
            {
                onClosed?.Invoke();
                return;
            }

            director.PlayOutro(onClosed);
        });
    }

    private static NPCData GetPrimaryParticipant(List<NPCData> participants)
    {
        if (participants == null || participants.Count == 0)
            return null;

        return participants[0];
    }
}
