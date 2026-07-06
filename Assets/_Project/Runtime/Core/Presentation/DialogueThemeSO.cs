using UnityEngine;

/// <summary>
/// 책임 :
/// - NPC/대화 데이터가 요청하는 대화창 색상과 효과 override 테마를 보관한다.
/// - 구체 DialogueView 렌더링은 UI가 담당하고, 이 에셋은 공유 authoring 데이터만 제공한다.
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Dialogue Theme")]
public class DialogueThemeSO : ScriptableObject
{
    [Header("Shared Outline")]
    public Color outlineColor = Color.white;

    [Header("Speaker Frame")]
    public Color speakerFrameFillColor = Color.white;

    [Header("Effect")]
    public AnimatorOverrideController effectOverride;
}
