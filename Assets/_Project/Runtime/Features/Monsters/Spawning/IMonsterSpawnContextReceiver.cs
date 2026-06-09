/// <summary>
/// 책임:
/// - MonsterSpawner가 스폰 직후 몬스터에게 스폰 문맥을 주입할 수 있는 계약을 제공한다.
/// </summary>
public interface IMonsterSpawnContextReceiver
{
    void ApplySpawnContext(MonsterSpawnContext context);
}
