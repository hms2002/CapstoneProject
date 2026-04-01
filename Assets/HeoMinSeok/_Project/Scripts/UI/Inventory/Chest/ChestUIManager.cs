using System.Collections.Generic;
using UnityEngine;

public class ChestUIManager : MonoBehaviour
{
    private readonly List<MonoBehaviour> disabledRuntimePlayerScripts = new();
    public static ChestUIManager Instance { get; private set; }

    [SerializeField] private ChestScreen chestScreen;
    [SerializeField] private MonoBehaviour[] playerControlScriptsToDisable; // PlayerInteractor2D 등

    private TreasureChest openedChest;
    private float prevTimeScale = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        ResolveChestScreenReference();
        // 씬 시작 시 상자 UI가 꺼져 있도록 보장
        if (chestScreen != null) chestScreen.gameObject.SetActive(false);
    }

    public void OpenChest(TreasureChest chest)
    {
        if (chest == null) return;

        ResolveChestScreenReference();
        if (chestScreen == null)
        {
            Debug.LogError("[ChestUIManager] ChestScreen reference is missing.");
            return;
        }

        openedChest = chest;

        // 1. 게임 정지 및 플레이어 조작 잠금
        prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        DisablePlayerControlsForCurrentSession();

        // 2. 데이터 바인딩
        chestScreen.Bind(chest.GetInventory());

        // 3. [핵심] 직접 켜지 않고 UIManager의 스택 명부에 정식 등록 요청! (이제 ESC가 먹힙니다)
        if (UIManager.Instance != null) UIManager.Instance.PushUI(chestScreen);
        else chestScreen.OpenUI(); // UIManager가 없을 때를 대비한 방어 코드
    }

    // [핵심] ChestScreen이 UIManager에 의해 닫혔을 때(ESC나 X버튼) 호출될 뒷수습(콜백) 함수

    private void DisablePlayerControlsForCurrentSession()
    {
        disabledRuntimePlayerScripts.Clear();

        var player = PlayerRuntimeRegistry.CurrentPlayer != null
            ? PlayerRuntimeRegistry.CurrentPlayer
            : PlayerInteractor2D.Instance;

        if (player != null)
        {
        TryDisable(player.GetComponent<PlayerInteractor2D>());
            TryDisable(player.GetComponent<PlayerIntentInput2D>());
            TryDisable(player.GetComponent<PlayerCombatInput2D>());
            TryDisable(player.GetComponent<PlayerAim2D>());
        }

        if (playerControlScriptsToDisable != null)
        {
            foreach (var script in playerControlScriptsToDisable)
                TryDisable(script);
        }
    }

    private void RestorePlayerControlsForCurrentSession()
    {
        for (int i = 0; i < disabledRuntimePlayerScripts.Count; i++)
        {
            var script = disabledRuntimePlayerScripts[i];
            if (script != null)
                script.enabled = true;
        }

        disabledRuntimePlayerScripts.Clear();
    }

    private void TryDisable(MonoBehaviour script)
    {
        if (script == null || !script.enabled)
            return;

        script.enabled = false;
        disabledRuntimePlayerScripts.Add(script);
    }

    public void HandleChestClosed()
    {
        // 상자 닫으면 플레이어 스크립트 복구
        RestorePlayerControlsForCurrentSession();

        if (PlayerInteractor2D.Instance != null)
        {
            PlayerInteractor2D.Instance.SetInteractState(InteractState.Idle);
        }

        // 시간 복구 및 상태 초기화
        Time.timeScale = prevTimeScale;
        openedChest = null;
    }
    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void ResolveChestScreenReference()
    {
        if (chestScreen != null)
            return;

        chestScreen = GetComponentInChildren<ChestScreen>(true);
    }
}
