using System;

namespace CapstonePresentation
{
    public enum PresentationReferenceMode
    {
        Inline,
        Cue,
        InlineThenCue
    }

    [Serializable]
    public struct PresentationReference
    {
        public PresentationReferenceMode mode;
        public WorldPresentationHook inlinePresentation;
        public CueRef cue;

        public bool HasAnyContent => mode switch
        {
            PresentationReferenceMode.Cue => cue.IsSet,
            PresentationReferenceMode.InlineThenCue => inlinePresentation.HasAnyContent || cue.IsSet,
            _ => inlinePresentation.HasAnyContent
        };
    }
}
