using UnityEngine;
using System.Collections.Generic;

public class GraveSpawner : MonoBehaviour
{
    [Header("프리팹 연결")]
    public GameObject weaponGravePrefab;
    public GameObject relicGravePrefab;

    [Header("스폰 위치 후보 (겹치지 않게)")]
    public List<Transform> spawnPoints;

    [Header("초기 설정")]
    public int baseWeaponGraveCount = 1;
    public int baseRelicGraveCount = 2;

    private void Start()
    {
        SpawnGraves();
    }

    private void SpawnGraves()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[GraveSpawner] 스폰 위치가 설정되지 않았습니다!");
            return;
        }

        // 1. 업그레이드 수치 읽어오기 (향후 UpgradeManager와 연결)
        int extraWeaponGrave = 0;       // 필드에 깔리는 무기 유해 개수 추가
        int extraRelicGrave = 0;        // 필드에 깔리는 유물 유해 개수 추가

        int extraWeaponDropCount = 0;   // 무기 유해에서 무기가 추가로 떨어질 보너스
        int extraRelicDropCount = 0;    // 유물 유해에서 유물이 추가로 떨어질 보너스

        float extraRareChance = 0f;     // 레어 등장 확률 증가
        float extraEpicChance = 0f;     // 에픽 등장 확률 증가

        int totalWeaponCount = baseWeaponGraveCount + extraWeaponGrave;
        int totalRelicCount = baseRelicGraveCount + extraRelicGrave;

        // 2. 스폰 위치 섞기 (겹침 방지)
        List<Transform> shuffledPoints = new List<Transform>(spawnPoints);
        for (int i = 0; i < shuffledPoints.Count; i++)
        {
            Transform temp = shuffledPoints[i];
            int randomIndex = Random.Range(i, shuffledPoints.Count);
            shuffledPoints[i] = shuffledPoints[randomIndex];
            shuffledPoints[randomIndex] = temp;
        }

        int spawnIndex = 0;

        // 3. 무기 유해 스폰
        for (int i = 0; i < totalWeaponCount; i++)
        {
            if (spawnIndex >= shuffledPoints.Count) break;

            GameObject go = Instantiate(weaponGravePrefab, shuffledPoints[spawnIndex].position, Quaternion.identity);
            go.transform.SetParent(this.transform);

            // 무기 유해에도 추가 드롭 개수 보너스를 쥐어줍니다!
            var interactable = go.GetComponent<GraveInteractable>();
            if (interactable != null)
            {
                interactable.bonusDropCount = extraWeaponDropCount;
            }

            spawnIndex++;
        }

        // 4. 유물 유해 스폰
        for (int i = 0; i < totalRelicCount; i++)
        {
            if (spawnIndex >= shuffledPoints.Count) break;

            GameObject go = Instantiate(relicGravePrefab, shuffledPoints[spawnIndex].position, Quaternion.identity);
            go.transform.SetParent(this.transform);

            // 유물 유해에 스탯 주입
            var interactable = go.GetComponent<GraveInteractable>();
            if (interactable != null)
            {
                interactable.bonusDropCount = extraRelicDropCount;
                interactable.bonusRareChance = extraRareChance;
                interactable.bonusEpicChance = extraEpicChance;
            }

            spawnIndex++;
        }
    }
}