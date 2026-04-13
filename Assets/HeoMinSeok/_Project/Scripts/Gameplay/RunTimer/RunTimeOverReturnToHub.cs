using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 책임 :
/// - 런 종료 사유가 TimeOver일 때 허브 복귀 경로를 실행한다.
/// - 연출이 아직 없더라도 시간 초과 실패가 실제 씬 전환으로 이어지도록 최소 동작을 보장한다.
/// </summary>
[DisallowMultipleComponent]
public sealed class RunTimeOverReturnToHub : MonoBehaviour
{
    [Header("Transition")]
    [SerializeField] private string hubSceneName = "ProtoTypeHub";
    [SerializeField] private bool useFadeTransitionService = true;

    private bool isHandlingTransition;

    private void OnEnable()
    {
        if (GamePlayDataManager.Instance != null)
        {
            GamePlayDataManager.Instance.OnRunStarted += HandleRunStarted;
            GamePlayDataManager.Instance.OnRunEnded += HandleRunEnded;
        }
    }

    private void OnDisable()
    {
        if (GamePlayDataManager.Instance != null)
        {
            GamePlayDataManager.Instance.OnRunStarted -= HandleRunStarted;
            GamePlayDataManager.Instance.OnRunEnded -= HandleRunEnded;
        }
    }

    /// <summary>
    /// 책임 :
    /// - 새 런이 시작될 때 시간 초과 전환 가드를 초기화한다.
    /// - 이전 런의 TimeOver 처리 상태가 다음 런까지 남지 않게 보장한다.
    /// </summary>
    private void HandleRunStarted()
    {
        isHandlingTransition = false;
    }

    private void HandleRunEnded(RunEndReason reason)
    {
        if (reason != RunEndReason.TimeOver || isHandlingTransition)
            return;

        if (string.IsNullOrWhiteSpace(hubSceneName))
        {
            Debug.LogWarning("[RunTimeOverReturnToHub] Hub scene name is empty. TimeOver transition was skipped.", this);
            return;
        }

        isHandlingTransition = true;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.CloseAllPopups();
            UIManager.Instance.HideHoverImmediate();
            UIManager.Instance.HideWorldPrompt();
        }

        if (useFadeTransitionService)
        {
            var transitionService = SceneFadeTransitionService.EnsureInstance();
            if (transitionService != null && transitionService.TryLoadScene(hubSceneName))
                return;
        }

        SceneManager.LoadScene(hubSceneName);
    }
}
