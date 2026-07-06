using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - 보스 HUD가 한 보스 source에서 읽을 최소 전투 표시 상태를 정의한다.
/// - UI 계층이 BossControllerBase 같은 Gameplay 구체 타입을 직접 참조하지 않게 한다.
/// </summary>
public interface IBossHudSource
{
    Component HudSourceComponent { get; }
    string BossHudDisplayName { get; }
    float CurrentHealthRatio { get; }
    bool IsCombatActive { get; }
    bool IsBossHudDead { get; }
    bool HasBossHudGroggyTag { get; }
    AttributeSet AttributeSet { get; }
    BossHudHealthBarTheme HudHealthBarTheme { get; }
}

/// <summary>
/// 책임 :
/// - Gameplay 보스 코드가 HUD 등록/해제/처치 표시를 요청할 수 있는 backend 계약을 제공한다.
/// - 실제 HUD 슬롯 생성과 Canvas 제어는 UI backend가 소유한다.
/// </summary>
public interface IBossHudBackend
{
    void RegisterBoss(IBossHudSource boss, string bossDisplayNameOverride = null, BossHudHealthBarTheme healthBarTheme = null);
    void UnbindBoss(IBossHudSource boss);
    void MarkBossDefeated(IBossHudSource boss);
}

/// <summary>
/// 책임 :
/// - 보스 gameplay와 UI HUD 구현 사이의 정적 playback 진입점을 제공한다.
/// - backend가 없을 때는 조용히 실패해 전투 로직이 HUD 생성 순서에 묶이지 않게 한다.
/// </summary>
public static class BossHudPlayback
{
    private static IBossHudBackend backend;

    public static bool IsAvailable => backend != null;

    public static void RegisterBackend(IBossHudBackend newBackend)
    {
        if (newBackend != null)
            backend = newBackend;
    }

    public static void UnregisterBackend(IBossHudBackend existingBackend)
    {
        if (ReferenceEquals(backend, existingBackend))
            backend = null;
    }

    public static void RegisterBoss(
        IBossHudSource boss,
        string bossDisplayNameOverride = null,
        BossHudHealthBarTheme healthBarTheme = null)
    {
        backend?.RegisterBoss(boss, bossDisplayNameOverride, healthBarTheme);
    }

    public static void UnbindBoss(IBossHudSource boss)
    {
        backend?.UnbindBoss(boss);
    }

    public static void MarkBossDefeated(IBossHudSource boss)
    {
        backend?.MarkBossDefeated(boss);
    }
}
