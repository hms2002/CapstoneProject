using System;
using UnityEngine;

public class UpgradeFeature : MonoBehaviour, INPCFeature
{
    // INPCFeature 인터페이스 구현
    public string FeatureName => "Upgrade";

    // [제거됨] 더 이상 개별 UI 캔버스를 변수로 직접 들고 있지 않습니다. (강한 결합 제거)

    public void Execute(Action onComplete)
    {
        // 1. UpgradeManager를 통해 UI를 엽니다.
        if (UpgradeManager.Instance != null)
        {
            // 현재 매니저에 구현되어 있는 ToggleUI를 사용해 창을 활성화합니다.
            // (나중에 UIManager로 분리하시면 UIManager.Instance.OpenUI("UpgradeTree") 등으로 수정하시면 됩니다.)
            UpgradeManager.Instance.ToggleUI();
        }
        else
        {
            Debug.LogWarning("[UpgradeFeature] 씬에 UpgradeManager.Instance가 존재하지 않습니다!");
        }

        // 2. 대화 시스템 종료 신호 발생 (SOLID: 단일 책임 원칙 준수)
        NPCFeatureController controller = GetComponent<NPCFeatureController>();
        if (controller != null)
        {
            controller.RequestDialogueExit?.Invoke();
        }

        // 3. 콜백 실행 (인터페이스 규격 준수)
        onComplete?.Invoke();
    }
}