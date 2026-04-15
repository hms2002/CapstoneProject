using UnityEngine;

namespace CapstonePresentation
{
    [CreateAssetMenu(fileName = "PresentationCue", menuName = "Presentation/Presentation Cue")]
    public sealed class PresentationCueSO : ScriptableObject
    {
        [SerializeField] private WorldPresentationHook presentation;

        public WorldPresentationHook Presentation => presentation;
        public bool HasAnyContent => presentation.HasAnyContent;
    }
}
