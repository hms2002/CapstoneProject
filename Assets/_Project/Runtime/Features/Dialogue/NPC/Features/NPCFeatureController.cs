using System;
using UnityEngine;

public class NPCFeatureController : MonoBehaviour
{
    public Action RequestDialogueExit;

    private INPCFeature[] features;

    private void Awake()
    {
        features = GetComponents<INPCFeature>();
    }

    public void ExecuteFeature(string featureName, Action onComplete = null)
    {
        if (features == null)
        {
            onComplete?.Invoke(); // 방어: 컴포넌트가 없으면 바로 대화 속행
            return;
        }

        foreach (var feature in features)
        {
            if (feature.FeatureName.ToLower() == featureName.ToLower())
            {
                feature.Execute(onComplete);
                return;
            }
        }

        Debug.LogWarning($"[NPCFeatureController] '{featureName}' 기능을 찾을 수 없습니다.");
        
        // [핵심 방어코드] 스펠링 실수 등으로 기능을 못 찾았을 때 대화가 영원히 멈추는 것을 방지!
        onComplete?.Invoke(); 
    }
}