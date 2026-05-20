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
            Instantiate(mediumSlimePrefab, mediumSpawnPosition, Quaternion.identity);

        if (largeSlimePrefab != null)
            Instantiate(largeSlimePrefab, largeSpawnPosition, Quaternion.identity);

        float remainingSpeechSeconds = slimeQueen.CallSlimeSpeechSeconds - slimeQueen.CallSlimeSpawnDelaySeconds;
        if (remainingSpeechSeconds > 0f)
            yield return WaitForSecondsUnlessCancelled(remainingSpeechSeconds, spec);
    }
}
