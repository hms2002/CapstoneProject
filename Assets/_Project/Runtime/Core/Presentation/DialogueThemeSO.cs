using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 책임 :
/// - NPC/대화 데이터가 요청하는 강조 색상과 효과 override 테마를 보관한다.
/// - 구체 DialogueView 렌더링은 UI가 담당하고, 이 에셋은 공유 authoring 데이터만 제공한다.
/// </summary>
[CreateAssetMenu(menuName = "Dialogue/Dialogue Theme")]
public class DialogueThemeSO : ScriptableObject
{
    [Header("Accent")]
    [FormerlySerializedAs("outlineColor")]
    public Color accentColor = Color.white;

    [Header("Effect")]
    public AnimatorOverrideController effectOverride;
}
