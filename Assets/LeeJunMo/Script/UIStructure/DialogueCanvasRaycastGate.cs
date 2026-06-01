using UnityEngine;

/// <summary>
/// 책임 :
/// - DialogueCanvas가 실제 대화 재생 중일 때만 GraphicRaycaster를 켠다.
/// - DialogueService를 쓰지 않는 RunSpecialNpc 선택지 UI가 떠 있는 동안에도 클릭 raycast를 허용한다.
/// - 대화 UI 참조 구조는 유지한 채, 평소에는 dialogue canvas가 HUD hover를 가로채지 않도록 입력만 분리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueCanvasRaycastGate : CanvasRaycastGateBase
{
    protected override bool ShouldEnableRaycast()
    {
        DialogueService dialogueService = DialogueService.Instance;
        return (dialogueService != null && dialogueService.IsPlaying) ||
               RunSpecialNpcChoicePresenter.HasVisibleChoicePresenter;
    }
}
