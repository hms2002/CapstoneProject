/// <summary>
/// 책임:
/// - 씬 로드 직후 Monster spawn director가 실행할 scene-local 수집/정리 정책을 묶는다.
/// - 전역 MonsterSpawner의 serialized 설정을 director로 전달하는 값 객체다.
/// </summary>
internal readonly struct SceneMonsterSpawnPolicy
{
    public readonly bool RecollectSpawnPoints;
    public readonly bool ClearAliveMonstersBeforeSceneSpawn;

    public SceneMonsterSpawnPolicy(
        bool recollectSpawnPoints,
        bool clearAliveMonstersBeforeSceneSpawn)
    {
        RecollectSpawnPoints = recollectSpawnPoints;
        ClearAliveMonstersBeforeSceneSpawn = clearAliveMonstersBeforeSceneSpawn;
    }
}
