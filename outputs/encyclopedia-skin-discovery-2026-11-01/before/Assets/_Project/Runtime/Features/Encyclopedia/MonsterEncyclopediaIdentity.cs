using UnityEngine;

/// <summary>실제 몬스터 프리팹과 도감의 안정적인 종 ID를 연결한다. 소환/분열 개체도 프리팹 ID를 상속한다.</summary>
[DisallowMultipleComponent]
public sealed class MonsterEncyclopediaIdentity : MonoBehaviour
{
    [SerializeField] private string monsterId;
    public string MonsterId => monsterId;

    public void Configure(string id)
    {
        monsterId = string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim();
    }
}
