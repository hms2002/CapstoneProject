using System;
using UnityEngine;

public class NPCFeatureController : MonoBehaviour
{
    // 대화 종료를 Controller에 요청하는 이벤트
    public Action RequestDialogueExit;

    private INPCFeature[] features;

    private void Awake()
    {
        // 이 NPC에 붙어있는 모든 INPCFeature 컴포넌트를 배열로 가져옵니다.
        features = GetComponents<INPCFeature>();
    }

    // [수정] Action을 선택적(Optional) 매개변수로 받아 Interface와 규격을 맞춥니다.
    public void ExecuteFeature(string featureName, Action onComplete = null)
    {
        if (features == null) return;

        foreach (var feature in features)
        {
            if (feature.FeatureName.ToLower() == featureName.ToLower())
            {
                // 인터페이스 규격에 맞춰 onComplete 전달
                feature.Execute(onComplete);
                return;
            }
        }

        Debug.LogWarning($"[NPCFeatureController] '{featureName}' 기능을 찾을 수 없습니다.");
    }
}