using UnityEngine;

/// <summary>
/// 책임 : Core/Gameplay 코드가 구체 저장 매니저를 몰라도 보스 테마별 영구 클리어 진행도를 조회하고 기록하게 한다.
/// </summary>
public static class BossClearProgressStore
{
    public static bool HasClearedBoss(string bossThemeId)
    {
        GameData data = GameDataStore.Data;
        return data != null &&
               data.bossClearProgressData != null &&
               data.bossClearProgressData.HasCleared(bossThemeId);
    }

    public static bool MarkBossCleared(string bossThemeId, Object requester = null)
    {
        if (string.IsNullOrWhiteSpace(bossThemeId))
            return false;

        GameData data = GameDataStore.EnsureData();
        if (data == null)
            return false;

        data.bossClearProgressData ??= new BossClearProgressSaveData();
        bool changed = data.bossClearProgressData.MarkCleared(bossThemeId);
        if (changed)
            GameDataStore.RequestDeferredSave(requester);

        return changed;
    }
}
