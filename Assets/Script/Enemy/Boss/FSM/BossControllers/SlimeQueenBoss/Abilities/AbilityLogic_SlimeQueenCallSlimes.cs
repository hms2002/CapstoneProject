using System.Collections;
using UnityEngine;
using UnityGAS;

public sealed class AbilityLogic_SlimeQueenCallSlimes : AbilityLogic
{
    /// <summary>SlimeQueen이 중형 슬라임 1마리와 대형 슬라임 1마리를 서로 다른 길목에 호출합니다.</summary>
    public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
    {
        SlimeQueen slimeQueen = system != null ? system.GetComponent<SlimeQueen>() : null;
        if (slimeQueen == null)
            yield break;

        if (!slimeQueen.TryGetCallSlimeSpawnPositions(out Vector3 mediumSpawnPosition, out Vector3 largeSpawnPosition))
            yield break;

        slimeQueen.FaceCurrentTarget();
        slimeQueen.ShowCallSlimeSpeech();

        if (slimeQueen.CallSlimeSpawnDelaySeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(slimeQueen.CallSlimeSpawnDelaySeconds, spec);

        if (IsAbilityCancelled(spec))
            yield break;

        GameObject mediumSlimePrefab = slimeQueen.GetRandomMediumSlimePrefab();
        GameObject largeSlimePrefab = slimeQueen.GetRandomLargeSlimePrefab();

        if (mediumSlimePrefab != null)
            SpawnSummonedSlime(mediumSlimePrefab, mediumSpawnPosition);

        if (largeSlimePrefab != null)
            SpawnSummonedSlime(largeSlimePrefab, largeSpawnPosition);
    }

    private GameObject SpawnSummonedSlime(GameObject slimePrefab, Vector3 spawnPosition)
    {
        GameObject spawnedSlime = Instantiate(slimePrefab, spawnPosition, Quaternion.identity);
        if (spawnedSlime != null && spawnedSlime.TryGetComponent(out Mob mob))
            mob.SuppressMonsterLootDrop();

        return spawnedSlime;
    }
}
