using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public struct NPCFeature
{
    public string featureKey;    // 예: "shop", "upgrade"
    public UnityEvent onExecute; // 실행할 함수 연결
}

public class NPCFeatureController : MonoBehaviour
{
    [Header("이 NPC가 가진 기능 목록")]
    [SerializeField] private List<NPCFeature> features;

    private Dictionary<string, UnityEvent> featureDict;

    private void Awake()
    {
        featureDict = new Dictionary<string, UnityEvent>();
        foreach (var feature in features)
        {
            string key = feature.featureKey.Trim().ToLower();
            if (!string.IsNullOrEmpty(key) && !featureDict.ContainsKey(key))
            {
                featureDict.Add(key, feature.onExecute);
            }
        }
    }

    public void ExecuteFeature(string key)
    {
        string searchKey = key.Trim().ToLower();

        if (featureDict.TryGetValue(searchKey, out UnityEvent action))
        {
            Debug.Log($"[NPCFeature] 기능 실행: {searchKey}");
            action.Invoke();
        }
        else
        {
            Debug.LogWarning($"[NPCFeature] '{gameObject.name}' NPC는 '{searchKey}' 기능이 없습니다.");
        }
    }
}