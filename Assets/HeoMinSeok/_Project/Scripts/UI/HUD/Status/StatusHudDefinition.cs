using UnityEngine;

/// <summary>
/// 책임 :
/// - 상태 HUD와 툴팁이 공통으로 사용하는 아이콘, 이름, 서사 문장, 효과 설명 같은 표시 정의를 자산으로 제공한다.
/// - 런타임 상태 소유 계층과 표시 문구 authoring을 분리해 무기, 유물, 환경 디버프가 같은 방식으로 HUD 엔트리를 구성하게 만든다.
/// </summary>
[CreateAssetMenu(
    fileName = "SHD_NewStatusDefinition",
    menuName = "Project/Status HUD Definition")]
public sealed class StatusHudDefinition : ScriptableObject
{
    [Header("Identity")]
    [SerializeField] private string statusId = "status";
    [SerializeField] private StatusHudGroup group = StatusHudGroup.Buff;
    [SerializeField] private int priority = 0;

    [Header("Presentation")]
    [SerializeField] private Sprite icon;
    [SerializeField] private string nameText = string.Empty;
    [TextArea(2, 4)]
    [SerializeField] private string storyText = string.Empty;
    [TextArea(2, 6)]
    [SerializeField] private string effectText = string.Empty;

    [Header("Defaults")]
    [SerializeField] private bool showStacksByDefault = true;
    [SerializeField] private bool showDurationByDefault = true;

    public string StatusId => statusId;
    public StatusHudGroup Group => group;
    public int Priority => priority;
    public Sprite Icon => icon;
    public string NameText => nameText;
    public string StoryText => storyText;
    public string EffectText => effectText;
    public bool ShowStacksByDefault => showStacksByDefault;
    public bool ShowDurationByDefault => showDurationByDefault;

    public StatusHudEntry CreateEntry(
        string ownerKey,
        int stackCount,
        float remainingTime,
        float maxTime,
        bool isHighlighted,
        bool isVisible,
        string effectTextOverride = null,
        Sprite iconOverride = null,
        bool? showStacksOverride = null,
        bool? showDurationOverride = null)
    {
        return new StatusHudEntry(
            ownerKey,
            statusId,
            nameText,
            storyText,
            string.IsNullOrWhiteSpace(effectTextOverride) ? effectText : effectTextOverride,
            iconOverride != null ? iconOverride : icon,
            stackCount,
            showStacksOverride ?? showStacksByDefault,
            remainingTime,
            maxTime,
            showDurationOverride ?? showDurationByDefault,
            group,
            priority,
            isHighlighted,
            isVisible);
    }
}
