using UnityEngine;
using System.Collections.Generic;

public class UnlockResultUI : MonoBehaviour
{
    public static UnlockResultUI Instance { get; private set; }

    [Header("References")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private Transform slotsParent;
    [SerializeField] private GameObject unlockSlotPrefab;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        panelRoot.SetActive(false);
    }

    // -----------------------------------------------------------
    // [Fix 2] 인자 타입을 string 리스트에서 Definition 리스트로 변경
    // -----------------------------------------------------------
    public void ShowUnlockResult(List<WeaponDefinition> weapons, List<RelicDefinition> relics)
    {
        // 1. 초기화
        foreach (Transform child in slotsParent) Destroy(child.gameObject);

        // 2. 무기 슬롯 생성 (ID 검색 없이 바로 사용)
        if (weapons != null)
        {
            foreach (var weapon in weapons)
            {
                if (weapon != null) CreateSlot(weapon);
            }
        }

        // 3. 유물 슬롯 생성
        if (relics != null)
        {
            foreach (var relic in relics)
            {
                if (relic != null) CreateSlot(relic);
            }
        }

        panelRoot.SetActive(true);
    }

    private void CreateSlot(ScriptableObject itemDef)
    {
        GameObject go = Instantiate(unlockSlotPrefab, slotsParent);
        var slot = go.GetComponent<UnlockSlotUI>();
        slot.Setup(itemDef);
    }

    public void Close()
    {
        panelRoot.SetActive(false);
        if (UIHoverManager.Instance != null)
            UIHoverManager.Instance.HideImmediate();
    }
}