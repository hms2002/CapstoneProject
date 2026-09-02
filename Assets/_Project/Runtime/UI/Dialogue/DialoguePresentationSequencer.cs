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
        Action onFramesOpened,
        Action onOpened)
    {
        if (view == null)
        {
            onOpened?.Invoke();
            return;
        }

        if (!presentationOptions.SuppressOpeningIntroSound)
            view.PlayOpeningIntroSound();

        Action completeOpening = onOpened;
        Action playPortraitIntro = () =>
        {
            if (presentationOptions.SuppressPortraitIntro || director == null)
            {
                completeOpening?.Invoke();
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
                    completeOpening);
                return;
            }

            director.PlayIntro(participants, completeOpening, openingPortraitLabel);
        };

        Action showDialogueFrames = () =>
        {
            view.ShowUI(
                isBoss && !presentationOptions.ForceDialogueBoxOnly,
                () =>
                {
                    onFramesOpened?.Invoke();
                    playPortraitIntro();
                });
        };

        if (isBoss && !presentationOptions.SkipBossPrelude && !presentationOptions.ForceDialogueBoxOnly)
        {
            view.PlayBossPrelude(showDialogueFrames);
            return;
        }

        showDialogueFrames();
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

        Action hideDialogueFrames = () => view.HideUI(onClosed);

        if (director == null || presentationOptions.SuppressPortraitOutro)
        {
            hideDialogueFrames();
            return;
        }

        director.PlayOutro(hideDialogueFrames);
    }

    private static NPCData GetPrimaryParticipant(List<NPCData> participants)
    {
        if (participants == null || participants.Count == 0)
            return null;

        return participants[0];
    }
}
