/// <summary>몬스터 처치의 런 대기 상태와 프로필 커밋 경계를 처리한다. 파일 저장은 기존 런 종료 정책이 소유한다.</summary>
public static class MonsterEncyclopediaRunProgress
{
    public static bool RecordKill(GamePlayData runData, string monsterId)
    {
        if (runData == null || !runData.isRunActive || string.IsNullOrWhiteSpace(monsterId))
            return false;

        runData.pendingMonsterEncyclopediaData ??= new MonsterEncyclopediaSaveData();
        runData.pendingMonsterEncyclopediaData.AddKills(monsterId, 1);
        return true;
    }

    public static int GetPendingKillCount(GamePlayData runData, string monsterId)
    {
        return runData != null && runData.isRunActive
            ? runData.pendingMonsterEncyclopediaData?.GetKillCount(monsterId) ?? 0
            : 0;
    }

    public static void Commit(GamePlayData runData, GameData profile)
    {
        if (runData == null || profile == null)
        {
            Clear(runData);
            return;
        }

        profile.monsterEncyclopediaData ??= new MonsterEncyclopediaSaveData();
        var records = runData.pendingMonsterEncyclopediaData?.killRecords;
        if (records != null)
            foreach (MonsterEncyclopediaKillRecord record in records)
                if (record != null)
                    profile.monsterEncyclopediaData.AddKills(record.monsterId, record.count);

        Clear(runData);
    }

    public static void Clear(GamePlayData runData)
    {
        runData?.pendingMonsterEncyclopediaData?.killRecords?.Clear();
    }
}
