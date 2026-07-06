using UnityEngine;

namespace CapstonePresentation
{
    /// <summary>
    /// 책임: 재사용 가능한 월드 presentation 요청 데이터를 ScriptableObject 에셋으로 보관한다.
    /// </summary>
    [CreateAssetMenu(fileName = "PresentationCue", menuName = "Presentation/Presentation Cue")]
    public sealed class PresentationCueSO : ScriptableObject
    {
        [SerializeField] private WorldPresentationHook presentation;

        public WorldPresentationHook Presentation => presentation;
        public bool HasAnyContent => presentation.HasAnyContent;
    }
}
