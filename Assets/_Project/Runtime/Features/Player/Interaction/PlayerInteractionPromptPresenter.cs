using UnityEngine;

/// <summary>
/// 책임 :
/// - 플레이어 상호작용 상태와 현재 대상에 맞춰 월드 프롬프트 표시/숨김 명령을 요청한다.
/// - UI 명령 백엔드가 없을 때는 serialized 프롬프트 뷰 계약으로만 fallback 한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerInteractionPromptPresenter : MonoBehaviour
{
    [SerializeField] private MonoBehaviour interactionPrompt;

    private IWorldInteractionPromptView InteractionPromptView => interactionPrompt as IWorldInteractionPromptView;

    public void SetPromptController(MonoBehaviour promptController)
    {
        if (promptController != null)
            interactionPrompt = promptController;
    }

    public void RefreshPrompt(IInteractable currentTarget, InteractState currentState)
    {
        if (currentState != InteractState.Idle || currentTarget == null)
        {
            HidePrompt();
            return;
        }

        if (!UiCommandPlayback.RefreshWorldPrompt(currentTarget))
            InteractionPromptView?.Refresh(currentTarget);
    }

    public void HidePrompt()
    {
        if (!UiCommandPlayback.HideWorldPrompt())
            InteractionPromptView?.Hide();
    }
}
