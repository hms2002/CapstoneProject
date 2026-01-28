using UnityEngine;

public class ChestUIManager : MonoBehaviour
{
    public static ChestUIManager Instance { get; private set; }

    [SerializeField] private ChestScreen chestScreen;
    [SerializeField] private MonoBehaviour[] playerControlScriptsToDisable; // SampleTopDownPlayer 등

    private TreasureChest openedChest;
    private float prevTimeScale = 1f;

    private void Awake()
    {
        Instance = this;
        chestScreen.gameObject.SetActive(false);
    }

    public void OpenChest(TreasureChest chest)
    {
        if (chest == null) return;

        openedChest = chest;

        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        // 플레이어 조작 잠금(가장 단순)
        if (playerControlScriptsToDisable != null)
            foreach (var s in playerControlScriptsToDisable)
                if (s != null) s.enabled = false;

        chestScreen.gameObject.SetActive(true);
        chestScreen.Bind(chest.GetInventory());
    }

    public void CloseChest()
    {
        chestScreen.gameObject.SetActive(false);

        if (playerControlScriptsToDisable != null)
            foreach (var s in playerControlScriptsToDisable)
                if (s != null) s.enabled = true;

        Time.timeScale = prevTimeScale;
        openedChest = null;
    }
}
