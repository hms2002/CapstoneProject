#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

/// <summary>
/// 책임: 현재 열린 씬의 상호작용 오브젝트 SpriteRenderer에 outline 머티리얼을 일괄 적용하는 Editor 전용 메뉴 도구입니다.
/// </summary>
public class InteractableTool
{
    // [핵심 1] 유니티 상단 메뉴에 우리가 만든 툴 버튼을 추가합니다.
    [MenuItem("Tools/상호작용 오브젝트/아웃라인 머테리얼 일괄 적용")]
    public static void ApplyOutlineMaterialToAll()
    {
        // 1. 적용할 목표 머테리얼을 프로젝트에서 찾습니다. 
        // (주의: "OutlineMaterial" 부분에 실제 에셋의 정확한 이름을 적어주세요!)
        string materialName = "OutlineMaterial";
        string[] guids = AssetDatabase.FindAssets("t:Material " + materialName);

        if (guids.Length == 0)
        {
            Debug.LogError($"[Tool] 프로젝트에서 '{materialName}' 머테리얼을 찾을 수 없습니다! 이름을 확인해주세요.");
            return;
        }

        // 첫 번째로 찾은 머테리얼을 로드합니다.
        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
        Material outlineMat = AssetDatabase.LoadAssetAtPath<Material>(path);

        // 2. 씬에 있는 모든 SpriteRenderer를 긁어옵니다. (비활성화된 오브젝트 포함)
        SpriteRenderer[] allRenderers = Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        int appliedCount = 0;

        foreach (SpriteRenderer sr in allRenderers)
        {
            // 3. 해당 렌더러가 붙은 오브젝트가 IInteractable을 상속받는지 확인합니다.
            IInteractable interactable = sr.GetComponent<IInteractable>();

            if (interactable != null)
            {
                // 이미 같은 머테리얼이면 건너뜁니다.
                if (sr.sharedMaterial == outlineMat) continue;

                // [핵심 2] Ctrl+Z(실행 취소)가 가능하게 만들고, 씬이 변경(Dirty)되었음을 유니티에 알립니다.
                Undo.RecordObject(sr, "Apply Outline Material");

                // 머테리얼을 덮어씌웁니다.
                sr.sharedMaterial = outlineMat;

                // 변경 사항 저장 대기열에 올립니다.
                EditorUtility.SetDirty(sr);
                appliedCount++;
            }
        }

        // 4. 완료 보고
        if (appliedCount > 0)
        {
            Debug.Log($"✨ [Tool] 완료! 총 {appliedCount}개의 상호작용 오브젝트에 아웃라인 머테리얼을 일괄 적용했습니다.");
        }
        else
        {
            Debug.Log("✔️ [Tool] 변경할 오브젝트가 없습니다. (모두 이미 적용되어 있거나 IInteractable이 없습니다)");
        }
    }
}
#endif
