using CapstoneAudio;
using UnityEngine;

/// <summary>
/// 책임:
/// - 방 입장 몬스터 스폰에서 사용할 기본 VFX, 지연 시간, 위치 보정, 사운드 설정을 보관한다.
/// - 개별 MonsterSpawnRoomGroup에 override가 없을 때 전역 fallback 설정으로 사용된다.
/// </summary>
[CreateAssetMenu(fileName = "MonsterRoomEntrySpawnSettings", menuName = "Gameplay/Monster Spawn/Room Entry Spawn Settings")]
public sealed class MonsterRoomEntrySpawnSettingsSO : ScriptableObject
{
    [SerializeField] private GameObject defaultSpawnVfxPrefab;
    [SerializeField, Min(0f)] private float defaultSpawnVfxDelaySeconds = 0.35f;
    [SerializeField] private Vector3 defaultSpawnVfxOffset;
    [SerializeField] private SoundRef defaultSpawnSound;

    public GameObject DefaultSpawnVfxPrefab => defaultSpawnVfxPrefab;
    public float DefaultSpawnVfxDelaySeconds => defaultSpawnVfxDelaySeconds;
    public Vector3 DefaultSpawnVfxOffset => defaultSpawnVfxOffset;
    public SoundRef DefaultSpawnSound => defaultSpawnSound;
}
