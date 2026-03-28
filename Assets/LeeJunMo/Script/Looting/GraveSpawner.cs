using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class GraveSpawner : MonoBehaviour
{
    [Header("Prefabs")]
    public GameObject weaponGravePrefab;
    public GameObject relicGravePrefab;

    [Header("Spawn Points")]
    public List<Transform> spawnPoints;

    [Header("Weapon Grave Count")]
    [SerializeField] private CountRangeWeightProfile weaponGraveCountProfile = new CountRangeWeightProfile();

    [Header("Relic Grave Count")]
    [SerializeField] private CountRangeWeightProfile relicGraveCountProfile = new CountRangeWeightProfile();

    [FormerlySerializedAs("weaponGraveMinCount")]
    [SerializeField, HideInInspector] private int legacyWeaponGraveMinCount = 1;
    [FormerlySerializedAs("weaponGraveMaxCount")]
    [SerializeField, HideInInspector] private int legacyWeaponGraveMaxCount = 1;
    [FormerlySerializedAs("weaponGraveCountWeights")]
    [SerializeField, HideInInspector] private List<DropCountOption> legacyWeaponGraveCountWeights = new List<DropCountOption>();

    [FormerlySerializedAs("relicGraveMinCount")]
    [SerializeField, HideInInspector] private int legacyRelicGraveMinCount = 2;
    [FormerlySerializedAs("relicGraveMaxCount")]
    [SerializeField, HideInInspector] private int legacyRelicGraveMaxCount = 2;
    [FormerlySerializedAs("relicGraveCountWeights")]
    [SerializeField, HideInInspector] private List<DropCountOption> legacyRelicGraveCountWeights = new List<DropCountOption>();

    [FormerlySerializedAs("baseWeaponGraveCount")]
    [SerializeField, HideInInspector] private int legacyBaseWeaponGraveCount = 1;

    [FormerlySerializedAs("baseRelicGraveCount")]
    [SerializeField, HideInInspector] private int legacyBaseRelicGraveCount = 2;

    private readonly LootRollService rollService = new LootRollService();

    private void OnValidate()
    {
        EnsureProfiles();
    }

    private void Start()
    {
        EnsureProfiles();
        SpawnGraves();
    }

    private void SpawnGraves()
    {
        if (spawnPoints == null || spawnPoints.Count == 0)
        {
            Debug.LogWarning("[GraveSpawner] Spawn points are not configured.");
            return;
        }

        GraveRunModifierDelta modifiers = RunModifierService.Instance != null
            ? RunModifierService.Instance.GraveModifiers
            : default;

        int totalWeaponCount = rollService.PickCountInProfile(
            weaponGraveCountProfile,
            modifiers.weaponGraveMinBonus,
            modifiers.weaponGraveMaxBonus);

        int totalRelicCount = rollService.PickCountInProfile(
            relicGraveCountProfile,
            modifiers.relicGraveMinBonus,
            modifiers.relicGraveMaxBonus);

        List<Transform> shuffledPoints = new List<Transform>(spawnPoints);
        for (int i = 0; i < shuffledPoints.Count; i++)
        {
            Transform temp = shuffledPoints[i];
            int randomIndex = Random.Range(i, shuffledPoints.Count);
            shuffledPoints[i] = shuffledPoints[randomIndex];
            shuffledPoints[randomIndex] = temp;
        }

        int spawnIndex = 0;

        for (int i = 0; i < totalWeaponCount; i++)
        {
            if (spawnIndex >= shuffledPoints.Count)
                break;

            GameObject go = Instantiate(weaponGravePrefab, shuffledPoints[spawnIndex].position, Quaternion.identity);
            go.transform.SetParent(transform);

            GraveInteractable interactable = go.GetComponent<GraveInteractable>();
            if (interactable != null)
            {
                interactable.bonusMinDropCount = modifiers.weaponDropMinBonus;
                interactable.bonusMaxDropCount = modifiers.weaponDropMaxBonus;
            }

            spawnIndex++;
        }

        for (int i = 0; i < totalRelicCount; i++)
        {
            if (spawnIndex >= shuffledPoints.Count)
                break;

            GameObject go = Instantiate(relicGravePrefab, shuffledPoints[spawnIndex].position, Quaternion.identity);
            go.transform.SetParent(transform);

            GraveInteractable interactable = go.GetComponent<GraveInteractable>();
            if (interactable != null)
            {
                interactable.bonusMinDropCount = modifiers.relicDropMinBonus;
                interactable.bonusMaxDropCount = modifiers.relicDropMaxBonus;
                interactable.bonusRareChance = modifiers.extraRareChance;
                interactable.bonusEpicChance = modifiers.extraEpicChance;
            }

            spawnIndex++;
        }
    }

    private void EnsureProfiles()
    {
        weaponGraveCountProfile ??= new CountRangeWeightProfile();
        relicGraveCountProfile ??= new CountRangeWeightProfile();

        weaponGraveCountProfile.TryInitializeFromLegacy(
            legacyWeaponGraveMinCount,
            legacyWeaponGraveMaxCount,
            legacyWeaponGraveCountWeights,
            legacyBaseWeaponGraveCount);

        relicGraveCountProfile.TryInitializeFromLegacy(
            legacyRelicGraveMinCount,
            legacyRelicGraveMaxCount,
            legacyRelicGraveCountWeights,
            legacyBaseRelicGraveCount);
    }
}
