using System;

namespace CapstonePresentation
{
    /// <summary>
    /// 책임: presentation 요청이 inline 데이터, catalog cue, 또는 둘의 조합 중 어떤 방식으로 해석될지 나타낸다.
    /// </summary>
    public enum PresentationReferenceMode
    {
        Inline,
        Cue,
        InlineThenCue
    }

    /// <summary>
    /// 책임: inline presentation 요청과 catalog cue 참조를 하나의 직렬화 필드로 전달한다.
    /// </summary>
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
