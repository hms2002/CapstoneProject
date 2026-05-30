using UnityEngine;

/// <summary>
/// 책임 : 빌드 런타임에서 시연용 치트 단축키를 감지하고 DemoCheatService에 실행을 위임한다.
/// 씬 전환 중이거나 설정에서 치트가 꺼져 있으면 입력을 소비하지 않는다.
/// </summary>
public sealed class DemoCheatHotkeyController : MonoBehaviour
{
    private const string HostName = "[DemoCheatHotkeyController]";
    private const string SettingsResourcePath = "DemoCheatSettings";

    private DemoCheatSettingsSO settings;
    private DemoCheatService service;
    private bool isAwaitingBossSceneSelection;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (FindFirstObjectByType<DemoCheatHotkeyController>() != null)
            return;

        var host = new GameObject(HostName);
        DontDestroyOnLoad(host);
        host.AddComponent<DemoCheatHotkeyController>();
    }

    private void Awake()
    {
        settings = Resources.Load<DemoCheatSettingsSO>(SettingsResourcePath);
        if (settings == null)
            Debug.LogWarning($"[DemoCheat] Resources/{SettingsResourcePath} 설정 에셋을 찾지 못했습니다. 시연 치트가 비활성화됩니다.", this);

        service = new DemoCheatService(this);
    }

    private void OnDisable()
    {
        service?.RestoreMapZoomImmediate();
    }

    private void Update()
    {
        if (settings == null || !settings.EnableDemoCheats)
            return;

        if (IsSceneTransitionActive())
        {
            service.RestoreMapZoomImmediate();
            isAwaitingBossSceneSelection = false;
            return;
        }

        if (isAwaitingBossSceneSelection)
        {
            HandleBossSceneSelectionInput();
            return;
        }

        if (WasPressed(settings.WarpToBossSceneKey))
        {
            isAwaitingBossSceneSelection = true;
            ShowResult(DemoCheatResult.Succeeded(service.BuildBossSceneSelectionGuide()), settings.CheatGuideDuration);
            return;
        }

        if (WasPressed(settings.CheatGuideKey))
            ShowResult(DemoCheatResult.Succeeded(service.BuildCheatGuide(settings)), settings.CheatGuideDuration);

        if (WasPressed(settings.MapZoomToggleKey))
            ShowResult(service.ToggleMapZoom(settings, this));

        if (WasPressed(settings.WarpToRunSpecialNpcKey))
            ShowResult(service.WarpPlayerToNextRunSpecialNpc(settings));

        if (WasPressed(settings.AddMagicStoneKey))
            ShowResult(service.AddMagicStone(settings));

        if (WasPressed(settings.MaxHealthKey))
            ShowResult(service.RefillPlayerHealth(settings));

        if (WasPressed(settings.WarpToPortalKey))
            ShowResult(service.WarpPlayerToNearestPortal(settings));

        if (WasPressed(settings.ResetWeaponCooldownKey))
            ShowResult(service.ResetAllOwnedWeaponCooldowns(settings));

        if (WasPressed(settings.IncreaseAttackKey))
            ShowResult(service.IncreasePlayerAttack(settings));
    }

    private void HandleBossSceneSelectionInput()
    {
        if (WasPressed(KeyCode.F5) || WasPressed(KeyCode.Escape))
        {
            isAwaitingBossSceneSelection = false;
            ShowResult(DemoCheatResult.Succeeded("보스 씬 이동 설정을 취소했습니다."));
            return;
        }

        if (WasPressed(KeyCode.F1))
            ApplyBossScenePortalOverride(0);
        else if (WasPressed(KeyCode.F2))
            ApplyBossScenePortalOverride(1);
        else if (WasPressed(KeyCode.F3))
            ApplyBossScenePortalOverride(2);
        else if (WasPressed(KeyCode.F4))
            ApplyBossScenePortalOverride(3);
    }

    private void ApplyBossScenePortalOverride(int bossSceneIndex)
    {
        isAwaitingBossSceneSelection = false;
        ShowResult(service.PrepareNearestPortalForBossScene(settings, bossSceneIndex));
    }

    private void ShowResult(DemoCheatResult result, float durationOverride = -1f)
    {
        if (string.IsNullOrWhiteSpace(result.Message))
            return;

        float duration = durationOverride > 0f ? durationOverride : settings.NotificationDuration;
        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowWarning(result.Message, duration);
            return;
        }

        string prefix = result.Success ? "알림" : "실패";
        Debug.Log($"[DemoCheat] {prefix}: {result.Message}", this);
    }

    private static bool WasPressed(KeyCode key)
    {
        return key != KeyCode.None && Input.GetKeyDown(key);
    }

    private static bool IsSceneTransitionActive()
    {
        return SceneTransitionCoordinator.Instance != null &&
               SceneTransitionCoordinator.Instance.IsTransitionActive;
    }
}
