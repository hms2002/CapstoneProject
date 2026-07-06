using UnityEngine;

/// <summary>
/// 책임 : 대화 실행에 필요한 UI/view/director 참조를 하나의 불변 묶음으로 전달한다.
/// </summary>
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

/// <summary>
/// 책임 : 씬/전역 캔버스에 배치된 대화 UI 구성요소를 찾아 DialogueController가 사용할 참조 묶음으로 정규화한다.
/// </summary>
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

        Canvas dialogueCanvas = GlobalCanvasPlayback.GetCanvas(GlobalCanvasLayer.Dialogue);
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
