using System;
using UnityEngine;

public class CurrencyManager : MonoBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    public event Action<int> OnMagicStoneChanged;

    private bool isHookedToGameData;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoBootstrap()
    {
        if (Instance != null)
            return;

        var go = new GameObject("CurrencyManager");
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
        TryHookGameData();
    }

    private void Start()
    {
        TryHookGameData();
        OnMagicStoneChanged?.Invoke(GetMagicStone());
    }

    public int GetMagicStone()
    {
        TryHookGameData();
        return GameDataManager.Instance != null ? GameDataManager.Instance.GetMagicStoneCount() : 0;
    }

    public void AddMagicStone(int amount)
    {
        TryHookGameData();
        if (GameDataManager.Instance == null)
            return;

        GameDataManager.Instance.AddMagicStone(amount);
        if (!isHookedToGameData)
            OnMagicStoneChanged?.Invoke(GetMagicStone());
    }

    public bool SpendMagicStone(int amount)
    {
        TryHookGameData();
        if (GameDataManager.Instance == null)
            return false;

        bool spent = GameDataManager.Instance.SpendMagicStone(amount);
        if (spent && !isHookedToGameData)
            OnMagicStoneChanged?.Invoke(GetMagicStone());
        return spent;
    }

    private void TryHookGameData()
    {
        if (isHookedToGameData || GameDataManager.Instance == null)
            return;

        GameDataManager.Instance.OnMagicStoneChanged += RelayMagicStoneChanged;
        isHookedToGameData = true;
    }

    private void RelayMagicStoneChanged(int amount)
    {
        OnMagicStoneChanged?.Invoke(amount);
    }

    private void OnDestroy()
    {
        if (isHookedToGameData && GameDataManager.Instance != null)
            GameDataManager.Instance.OnMagicStoneChanged -= RelayMagicStoneChanged;

        isHookedToGameData = false;

        if (Instance == this)
            Instance = null;
    }
}
