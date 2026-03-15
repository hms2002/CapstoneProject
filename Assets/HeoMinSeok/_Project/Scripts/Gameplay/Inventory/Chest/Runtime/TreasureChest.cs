using UnityEngine;
using System.Collections.Generic;

public class TreasureChest : MonoBehaviour
{
    private ChestInventory inventory;

    // 상자 뚜껑이 열려있는지 여부 (애니메이션/스프라이트 제어용)
    // 로직(UI 열기)을 막는 용도가 아님!
    private bool isOpened = false;

    // 아이템 데이터가 생성되었는지 여부 (중복 파밍 방지용)
    private bool isGenerated = false;

    public int capacity = 16;

    private void Awake()
    {
        inventory = new ChestInventory(); // capacity는 내부 기본값 사용
    }

    // =========================================================
    // 1. 외부(보스 등)에서 아이템을 받을 때
    // =========================================================
    public void InitializeWithLoot(List<ScriptableObject> loots)
    {
        if (inventory == null) inventory = new ChestInventory();

        // [핵심 수정] 플레이어 인벤토리를 뒤져서 중복 검사하는 로직을 완전히 삭제!
        // (LootManager나 BossDrop에서 이미 중복을 걸러서 깨끗한 리스트만 넘겨줍니다)
        foreach (var item in loots)
        {
            if (item != null)
            {
                inventory.TryAdd(item);
            }
        }

        isGenerated = true;
    }

    // =========================================================
    // 2. 상호작용으로 열 때 (언제든 다시 열 수 있음)
    // =========================================================
    public void Open()
    {
        // 1. 데이터 생성 (아직 안 만들어졌을 때만 1회 실행)
        if (!isGenerated)
        {
            GenerateSelfLoot();
            isGenerated = true;
        }

        // 2. 비주얼 처리 (처음 열 때만 애니메이션 재생)
        if (!isOpened)
        {
            isOpened = true;
            // TODO: 여기서 애니메이션 재생 (예: animator.SetTrigger("Open"))
            // Debug.Log("상자 뚜껑이 열립니다!");
        }

        // 3. UI 열기 (항상 실행)
        if (ChestUIManager.Instance != null)
        {
            ChestUIManager.Instance.OpenChest(this);
        }
    }

    // =========================================================
    // 3. 스스로 아이템 생성
    // =========================================================
    private void GenerateSelfLoot()
    {
        if (LootManager.Instance != null)
        {
            // [핵심 수정] 매개변수 없이 깔끔하게 호출! 
            // (누가 중복인지 검사하는 건 LootManager가 알아서 합니다)
            List<ScriptableObject> loots = LootManager.Instance.GenerateChestLoot();

            if (loots != null)
            {
                foreach (var item in loots)
                {
                    if (item != null)
                    {
                        inventory.TryAdd(item);
                    }
                }
            }
        }
    }

    public ChestInventory GetInventory() => inventory;
}