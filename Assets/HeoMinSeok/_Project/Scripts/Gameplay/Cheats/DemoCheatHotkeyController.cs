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

    private void Update()
    {
        if (settings == null || !settings.EnableDemoCheats)
            return;

        if (IsSceneTransitionActive())
            return;

        if (WasPressed(settings.MaxHealthKey))
            ShowResult(service.RefillPlayerHealth(settings));

        if (WasPressed(settings.WarpToPortalKey))
            ShowResult(service.WarpPlayerToNearestPortal(settings));

        if (WasPressed(settings.ResetWeaponCooldownKey))
            ShowResult(service.ResetAllOwnedWeaponCooldowns(settings));

        if (WasPressed(settings.IncreaseAttackKey))
            ShowResult(service.IncreasePlayerAttack(settings));
    }

    private void ShowResult(DemoCheatResult result)
    {
        if (string.IsNullOrWhiteSpace(result.Message))
            return;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowWarning(result.Message, settings.NotificationDuration);
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
