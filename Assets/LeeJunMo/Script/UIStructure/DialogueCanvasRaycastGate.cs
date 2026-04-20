using UnityEngine;

/// <summary>
/// 책임 :
/// - DialogueCanvas가 실제 대화 재생 중일 때만 GraphicRaycaster를 켠다.
/// - 대화 UI 참조 구조는 유지한 채, 평소에는 dialogue canvas가 HUD hover를 가로채지 않도록 입력만 분리한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueCanvasRaycastGate : CanvasRaycastGateBase
{
    protected override bool ShouldEnableRaycast()
    {
        DialogueService dialogueService = DialogueService.Instance;
        if (dialogueService == null)
            return false;

        return dialogueService.IsPlaying;
    }
}
