using System;
using UnityEngine;

/// <summary>도감은 프로필 누적값과 현재 런 대기분을 읽기만 한다. 저장 커밋은 기존 런 종료 정책이 소유한다.</summary>
public static class MonsterEncyclopediaProgress
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RegisterDeathHook()
    {
        Enemy.AnyDeathStarted -= HandleDeathStarted;
        Enemy.AnyDeathStarted += HandleDeathStarted;
    }

    public static int GetKillCount(string monsterId)
    {
        int committed = GameDataStore.Data?.monsterEncyclopediaData?.GetKillCount(monsterId) ?? 0;
        int pending = RunSessionStore.GetPendingMonsterEncyclopediaKillCount(monsterId);
        return (int)Math.Min(int.MaxValue, (long)committed + pending);
    }

    public static bool RecordKill(string monsterId)
    {
        return GameDataStore.Data != null && RunSessionStore.RecordMonsterEncyclopediaKill(monsterId);
    }

    public static void RecordEncounter(GameObject monster)
    {
        if (monster == null || !RunSessionStore.IsRunActive ||
            monster.scene.name.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
            !monster.TryGetComponent(out MonsterEncyclopediaIdentity identity)) return;
        EncyclopediaDiscoveryProgress.Record(EncyclopediaDiscoveryKind.Monster, identity.MonsterId, monster.scene.name);
    }

    private static void HandleDeathStarted(Enemy enemy)
    {
        if (!(enemy is Mob) || !RunSessionStore.IsRunActive)
            return;

        // Editor direct starts can activate a run in these scenes; they are not profile combat progress.
        string sceneName = enemy.gameObject.scene.name;
        if (sceneName.IndexOf("Tutorial", StringComparison.OrdinalIgnoreCase) >= 0 ||
            sceneName.IndexOf("Test", StringComparison.OrdinalIgnoreCase) >= 0)
            return;

        if (enemy.TryGetComponent(out MonsterEncyclopediaIdentity identity))
            RecordKill(identity.MonsterId);
        // Enemy emits this once after its isDead guard. Environmental deaths count too; killer attribution is absent.
    }
}
