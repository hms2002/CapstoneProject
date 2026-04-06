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
        CinematicDirector resolvedDirector = NormalizeUnityReference(currentDirector);
        if (resolvedDirector == null)
            resolvedDirector = owner.GetComponent<CinematicDirector>();

        DialogueTagHandler resolvedTagHandler = NormalizeUnityReference(currentTagHandler);
        if (resolvedTagHandler == null)
            resolvedTagHandler = owner.GetComponent<DialogueTagHandler>();

        DialogueView resolvedView = NormalizeUnityReference(currentView);
        PortraitController resolvedPortraitController = NormalizeUnityReference(currentPortraitController);

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

        if (resolvedView == null)
            resolvedView = owner.GetComponentInChildren<DialogueView>(true);

        if (resolvedPortraitController == null)
        {
            if (resolvedDirector != null)
                resolvedPortraitController = resolvedDirector.GetComponentInChildren<PortraitController>(true);

            if (resolvedPortraitController == null)
                resolvedPortraitController = owner.GetComponentInChildren<PortraitController>(true);
        }

        if (resolvedDirector == null)
            resolvedDirector = owner.GetComponentInChildren<CinematicDirector>(true);

        if (resolvedTagHandler == null)
            resolvedTagHandler = owner.GetComponentInChildren<DialogueTagHandler>(true);

        if (resolvedDirector != null && resolvedPortraitController != null)
            resolvedDirector.SetPortraitController(resolvedPortraitController);

        return new DialogueResolvedReferences(resolvedView, resolvedDirector, resolvedPortraitController, resolvedTagHandler);
    }

    private static T NormalizeUnityReference<T>(T target)
        where T : Object
    {
        return target != null ? target : null;
    }
}
