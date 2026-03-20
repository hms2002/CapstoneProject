using UnityEngine;

public class NPCManager : MonoBehaviour
{
    public static NPCManager Instance { get; private set; }

    [Header("NPC 통합 데이터베이스")]
    [SerializeField] private NPCDatabase database;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 씬이 넘어가도 데이터 유지
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ID로 NPC 데이터를 요청하면 꺼내주는 함수
    public NPCData GetNPCData(int id)
    {
        if (database == null) return null;
        return database.GetNPC(id);
    }
}