using System;
using System.Collections.Generic;

public static class DialoguePresentationSequencer
{
    public static void PlayOpening(DialogueView view, CinematicDirector director, List<NPCData> participants, bool isBoss, Action onOpened)
    {
        if (view == null)
        {
            onOpened?.Invoke();
            return;
        }

        view.PlayOpeningIntroSound();

        Action showDialogueUi = () =>
        {
            view.ShowUI(isBoss, onOpened);
        };

        if (director == null)
        {
            showDialogueUi();
            return;
        }

        Action playPortraitIntro = () => director.PlayIntro(participants, showDialogueUi);
        if (isBoss)
        {
            view.PlayBossPrelude(playPortraitIntro);
            return;
        }

        playPortraitIntro();
    }

    public static void PlayClosing(DialogueView view, CinematicDirector director, Action onClosed)
    {
        if (view == null)
        {
            onClosed?.Invoke();
            return;
        }

        view.HideUI(() =>
        {
            if (director == null)
            {
                onClosed?.Invoke();
                return;
            }

            director.PlayOutro(onClosed);
        });
    }
}
