using UnityEngine;

public readonly struct DialogueResolvedReferences
{
    public DialogueResolvedReferences(DialogueView view, CinematicDirector director, PortraitController portraitController, DialogueTagHandler tagHandler)
    {
        View = view;
        Director = director;
        PortraitController = portraitController;
        TagHandler = tagHandler;
    }

    public DialogueView View { get; }
    public CinematicDirector Director { get; }
    public PortraitController PortraitController { get; }
    public DialogueTagHandler TagHandler { get; }
}

public static class DialogueRuntimeReferenceResolver
{
    public static DialogueResolvedReferences Resolve(
        MonoBehaviour owner,
        DialogueView currentView,
        CinematicDirector currentDirector,
        PortraitController currentPortraitController,
        DialogueTagHandler currentTagHandler)
    {
        CinematicDirector resolvedDirector = currentDirector != null
            ? currentDirector
            : owner.GetComponent<CinematicDirector>();

        DialogueTagHandler resolvedTagHandler = currentTagHandler != null
            ? currentTagHandler
            : owner.GetComponent<DialogueTagHandler>();

        DialogueView resolvedView = currentView;
        PortraitController resolvedPortraitController = currentPortraitController;

        Canvas dialogueCanvas = GlobalUIRoot.GetCanvas(GlobalCanvasLayer.Dialogue);
        if (dialogueCanvas != null)
        {
            DialogueView canvasView = dialogueCanvas.GetComponentInChildren<DialogueView>(true);
            if (canvasView != null)
                resolvedView = canvasView;

            PortraitController canvasPortraitController = dialogueCanvas.GetComponentInChildren<PortraitController>(true);
            if (canvasPortraitController != null)
                resolvedPortraitController = canvasPortraitController;
        }

        if (resolvedDirector != null && resolvedPortraitController != null)
            resolvedDirector.SetPortraitController(resolvedPortraitController);

        return new DialogueResolvedReferences(resolvedView, resolvedDirector, resolvedPortraitController, resolvedTagHandler);
    }
}
