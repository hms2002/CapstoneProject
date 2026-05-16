using UnityEngine;

[CreateAssetMenu(
    fileName = "BossBattleEndPrefabCatalog",
    menuName = "Capstone/Boss/Battle End Prefab Catalog")]
public sealed class BossBattleEndPrefabCatalogSO : ScriptableObject
{
    [Header("Rewards")]
    [SerializeField] private GameObject treasureChestPrefab;
    [SerializeField] private GameObject magicStonePrefab;

    [Header("Portal")]
    [SerializeField] private GameObject portalPrefab;

    public GameObject TreasureChestPrefab => treasureChestPrefab;
    public GameObject MagicStonePrefab => magicStonePrefab;
    public GameObject PortalPrefab => portalPrefab;
}
