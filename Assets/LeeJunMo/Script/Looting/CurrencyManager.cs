using System;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    public event Action<int> OnMagicStoneChanged;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject(nameof(CurrencyManager));
        go.AddComponent<CurrencyManager>();
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        OnMagicStoneChanged?.Invoke(GetMagicStone());
    }

    public int GetMagicStone()
    {
        return TryGetData(out GameData data) ? data.magicStone : 0;
    }

    public void AddMagicStone(int amount)
    {
        if (!TryGetData(out GameData data))
            return;

        data.magicStone += amount;
        OnMagicStoneChanged?.Invoke(data.magicStone);
    }

    public bool SpendMagicStone(int amount)
    {
        if (!TryGetData(out GameData data))
            return false;

        if (data.magicStone < amount)
        {
            Debug.Log("[CurrencyManager] 마정석이 부족합니다.");
            return false;
        }

        data.magicStone -= amount;
        OnMagicStoneChanged?.Invoke(data.magicStone);
        return true;
    }

    private static bool TryGetData(out GameData data)
    {
        data = GameDataManager.Instance != null ? GameDataManager.Instance.Data : null;
        return data != null;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }
}
