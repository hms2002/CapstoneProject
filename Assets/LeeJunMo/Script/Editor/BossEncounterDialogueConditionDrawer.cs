using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(BossEncounterDialogueCondition))]
public sealed class BossEncounterDialogueConditionDrawer : PropertyDrawer
{
    private const float Spacing = 2f;

    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        EditorGUI.BeginProperty(position, label, property);

        Rect line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
        SerializedProperty typeProperty = property.FindPropertyRelative("type");
        BossEncounterDialogueConditionType type = GetConditionType(typeProperty);
        EditorGUI.PropertyField(line, typeProperty, Content("Type", GetTypeTooltip(type)));

        EditorGUI.indentLevel++;

        line.y += EditorGUIUtility.singleLineHeight + Spacing;

        if (NeedsTargetNpc(type))
        {
            DrawProperty(
                ref line,
                property,
                "targetNpcOverride",
                "Target NPC Override",
                "보스/호감도 조건에서 참조할 NPC 대상입니다. 비워두면 현재 NPCData를 기준으로 검사합니다.");
        }

        switch (type)
        {
            case BossEncounterDialogueConditionType.Always:
                break;
            case BossEncounterDialogueConditionType.HasMetBoss:
            case BossEncounterDialogueConditionType.BackpackIsFull:
                DrawProperty(ref line, property, "expectedBool", "Expected", GetExpectedBoolTooltip(type));
                break;
            case BossEncounterDialogueConditionType.LastRunEndReason:
                DrawProperty(
                    ref line,
                    property,
                    "runEndReason",
                    "Run End Reason",
                    "최근 런 종료 사유와 비교할 값입니다.");
                DrawProperty(ref line, property, "expectedBool", "Expected", GetExpectedBoolTooltip(type));
                break;
            case BossEncounterDialogueConditionType.PlayerHasWeapon:
            case BossEncounterDialogueConditionType.PlayerHasRelic:
            case BossEncounterDialogueConditionType.PlayerHasUnlockedWeapon:
            case BossEncounterDialogueConditionType.PlayerHasUnlockedRelic:
                DrawProperty(ref line, property, "stringValue", "Item ID", GetStringValueTooltip(type));
                DrawProperty(ref line, property, "expectedBool", "Expected", GetExpectedBoolTooltip(type));
                break;
            case BossEncounterDialogueConditionType.PlayerHealth:
                DrawProperty(
                    ref line,
                    property,
                    "attribute",
                    "Health Attribute",
                    "플레이어에게서 읽을 AttributeDefinition입니다. 현재 체력은 HealthAttribute를 사용합니다.");
                DrawFloatComparison(ref line, property, type);
                break;
            case BossEncounterDialogueConditionType.PlayerHealthRatio01:
                DrawProperty(
                    ref line,
                    property,
                    "attribute",
                    "Health Attribute",
                    "현재값으로 읽을 플레이어 AttributeDefinition입니다. 보통 HealthAttribute를 사용합니다.");
                DrawProperty(
                    ref line,
                    property,
                    "maxAttribute",
                    "Max Health Attribute",
                    "최대값으로 사용할 플레이어 AttributeDefinition입니다. 보통 MaxHealthAttribute를 사용합니다.");
                DrawFloatComparison(ref line, property, type);
                break;
            case BossEncounterDialogueConditionType.RunRemainingSeconds:
            case BossEncounterDialogueConditionType.RunRemainingRatio01:
            case BossEncounterDialogueConditionType.RunElapsedSeconds:
                DrawFloatComparison(ref line, property, type);
                break;
            default:
                DrawIntComparison(ref line, property, type);
                break;
        }

        if (type != BossEncounterDialogueConditionType.Always)
        {
            DrawProperty(
                ref line,
                property,
                "invert",
                "Invert Result",
                "활성화하면 조건 평가 후 true는 false로, false는 true로 반전됩니다.");
        }

        EditorGUI.indentLevel--;
        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        SerializedProperty typeProperty = property.FindPropertyRelative("type");
        BossEncounterDialogueConditionType type = GetConditionType(typeProperty);

        int lines = 1;

        if (NeedsTargetNpc(type))
            lines++;

        switch (type)
        {
            case BossEncounterDialogueConditionType.Always:
                break;
            case BossEncounterDialogueConditionType.HasMetBoss:
            case BossEncounterDialogueConditionType.BackpackIsFull:
                lines += 1;
                break;
            case BossEncounterDialogueConditionType.LastRunEndReason:
                lines += 2;
                break;
            case BossEncounterDialogueConditionType.PlayerHasWeapon:
            case BossEncounterDialogueConditionType.PlayerHasRelic:
            case BossEncounterDialogueConditionType.PlayerHasUnlockedWeapon:
            case BossEncounterDialogueConditionType.PlayerHasUnlockedRelic:
                lines += 2;
                break;
            case BossEncounterDialogueConditionType.PlayerHealth:
                lines += 1 + GetComparisonLineCount(property);
                break;
            case BossEncounterDialogueConditionType.PlayerHealthRatio01:
                lines += 2 + GetComparisonLineCount(property);
                break;
            case BossEncounterDialogueConditionType.RunRemainingSeconds:
            case BossEncounterDialogueConditionType.RunRemainingRatio01:
            case BossEncounterDialogueConditionType.RunElapsedSeconds:
                lines += GetComparisonLineCount(property);
                break;
            default:
                lines += GetComparisonLineCount(property);
                break;
        }

        if (type != BossEncounterDialogueConditionType.Always)
            lines++;

        return lines * EditorGUIUtility.singleLineHeight + (lines - 1) * Spacing;
    }

    private static BossEncounterDialogueConditionType GetConditionType(SerializedProperty typeProperty)
    {
        int value = typeProperty != null ? typeProperty.enumValueIndex : 0;
        return (BossEncounterDialogueConditionType)Mathf.Clamp(
            value,
            0,
            System.Enum.GetValues(typeof(BossEncounterDialogueConditionType)).Length - 1);
    }

    private static bool NeedsTargetNpc(BossEncounterDialogueConditionType type)
    {
        return type == BossEncounterDialogueConditionType.EncounterCount
            || type == BossEncounterDialogueConditionType.HasMetBoss
            || type == BossEncounterDialogueConditionType.BossVictoryCount
            || type == BossEncounterDialogueConditionType.BossDefeatCount
            || type == BossEncounterDialogueConditionType.Affection;
    }

    private static int GetComparisonLineCount(SerializedProperty property)
    {
        SerializedProperty comparisonProperty = property.FindPropertyRelative("comparison");
        BossDialogueComparison comparison = (BossDialogueComparison)comparisonProperty.enumValueIndex;
        return comparison == BossDialogueComparison.BetweenInclusive ? 3 : 2;
    }

    private static void DrawIntComparison(
        ref Rect line,
        SerializedProperty property,
        BossEncounterDialogueConditionType type)
    {
        SerializedProperty comparisonProperty = property.FindPropertyRelative("comparison");
        EditorGUI.PropertyField(line, comparisonProperty, Content("Comparison", GetComparisonTooltip(type)));
        Advance(ref line);

        BossDialogueComparison comparison = (BossDialogueComparison)comparisonProperty.enumValueIndex;
        if (comparison == BossDialogueComparison.BetweenInclusive)
        {
            DrawProperty(ref line, property, "minIntValue", "Min", GetMinTooltip(type));
            DrawProperty(ref line, property, "maxIntValue", "Max", GetMaxTooltip(type));
            return;
        }

        DrawProperty(ref line, property, "intValue", "Value", GetValueTooltip(type));
    }

    private static void DrawFloatComparison(
        ref Rect line,
        SerializedProperty property,
        BossEncounterDialogueConditionType type)
    {
        SerializedProperty comparisonProperty = property.FindPropertyRelative("comparison");
        EditorGUI.PropertyField(line, comparisonProperty, Content("Comparison", GetComparisonTooltip(type)));
        Advance(ref line);

        BossDialogueComparison comparison = (BossDialogueComparison)comparisonProperty.enumValueIndex;
        if (comparison == BossDialogueComparison.BetweenInclusive)
        {
            DrawProperty(ref line, property, "minFloatValue", "Min", GetMinTooltip(type));
            DrawProperty(ref line, property, "maxFloatValue", "Max", GetMaxTooltip(type));
            return;
        }

        DrawProperty(ref line, property, "floatValue", "Value", GetValueTooltip(type));
    }

    private static void DrawProperty(
        ref Rect line,
        SerializedProperty property,
        string propertyName,
        string label,
        string tooltip)
    {
        SerializedProperty child = property.FindPropertyRelative(propertyName);
        if (child == null)
            return;

        EditorGUI.PropertyField(line, child, Content(label, tooltip));
        Advance(ref line);
    }

    private static GUIContent Content(string label, string tooltip)
    {
        return new GUIContent(label, tooltip);
    }

    private static string GetTypeTooltip(BossEncounterDialogueConditionType type)
    {
        switch (type)
        {
            case BossEncounterDialogueConditionType.Always:
                return "항상 통과하는 조건입니다. 기본 조우 대사 룰에 사용할 수 있습니다.";
            case BossEncounterDialogueConditionType.EncounterCount:
                return "세이브 데이터 기준으로 대상 보스를 몇 번 조우했는지 검사합니다.";
            case BossEncounterDialogueConditionType.HasMetBoss:
                return "대상 보스의 조우 횟수가 1회 이상인지 검사합니다.";
            case BossEncounterDialogueConditionType.BossVictoryCount:
                return "대상 보스를 상대로 기록된 승리 횟수를 검사합니다.";
            case BossEncounterDialogueConditionType.BossDefeatCount:
                return "대상 보스를 상대로 기록된 패배 횟수를 검사합니다.";
            case BossEncounterDialogueConditionType.Affection:
                return "대상 NPC의 저장된 호감도와 런 중 변동 호감도를 기준으로 검사합니다.";
            case BossEncounterDialogueConditionType.RunRemainingSeconds:
                return "현재 런 타이머의 남은 시간을 초 단위로 검사합니다.";
            case BossEncounterDialogueConditionType.RunRemainingRatio01:
                return "현재 런 타이머의 남은 시간 비율을 0~1 기준으로 검사합니다.";
            case BossEncounterDialogueConditionType.RunElapsedSeconds:
                return "현재 런에서 경과한 시간을 초 단위로 검사합니다.";
            case BossEncounterDialogueConditionType.PlayerHealth:
                return "플레이어의 현재 체력 Attribute 값을 검사합니다.";
            case BossEncounterDialogueConditionType.PlayerHealthRatio01:
                return "플레이어 현재 체력 / 최대 체력 비율을 0~1 기준으로 검사합니다.";
            case BossEncounterDialogueConditionType.ClearCount:
                return "세이브 데이터에 기록된 총 클리어 횟수를 검사합니다.";
            case BossEncounterDialogueConditionType.MagicStone:
                return "저장된 마석 수와 런 중 획득 예정 마석 수를 합산해 검사합니다.";
            case BossEncounterDialogueConditionType.LastRunEndReason:
                return "런타임 데이터에 저장된 가장 최근 런 종료 사유를 검사합니다.";
            case BossEncounterDialogueConditionType.PlayerHasWeapon:
                return "플레이어가 지정한 weaponId의 무기를 현재 보유 중인지 검사합니다.";
            case BossEncounterDialogueConditionType.PlayerHasRelic:
                return "플레이어가 지정한 relicId의 유물을 현재 보유 중인지 검사합니다.";
            case BossEncounterDialogueConditionType.PlayerHasUnlockedWeapon:
                return "지정한 weaponId가 아이템 해금 데이터에 해금되어 있는지 검사합니다.";
            case BossEncounterDialogueConditionType.PlayerHasUnlockedRelic:
                return "지정한 relicId가 아이템 해금 데이터에 해금되어 있는지 검사합니다.";
            case BossEncounterDialogueConditionType.PlayerWeaponCount:
                return "플레이어의 무기 슬롯 중 채워진 슬롯 개수를 검사합니다.";
            case BossEncounterDialogueConditionType.PlayerRelicCount:
                return "플레이어 유물 인벤토리에 들어 있는 유물 개수를 검사합니다.";
            case BossEncounterDialogueConditionType.BackpackItemCount:
                return "플레이어 가방에 들어 있는 아이템 개수를 검사합니다.";
            case BossEncounterDialogueConditionType.BackpackIsFull:
                return "플레이어 가방에 빈 슬롯이 없는지 검사합니다.";
            default:
                return "이 조건이 검사할 런타임 값을 선택합니다.";
        }
    }

    private static string GetComparisonTooltip(BossEncounterDialogueConditionType type)
    {
        return $"현재 {GetMeasuredValueName(type)}을 설정한 값과 비교하는 방식입니다.";
    }

    private static string GetValueTooltip(BossEncounterDialogueConditionType type)
    {
        return $"{GetMeasuredValueName(type)} 비교에 사용할 기준값입니다.";
    }

    private static string GetMinTooltip(BossEncounterDialogueConditionType type)
    {
        return $"{GetMeasuredValueName(type)} 비교에 사용할 포함 최소값입니다.";
    }

    private static string GetMaxTooltip(BossEncounterDialogueConditionType type)
    {
        return $"{GetMeasuredValueName(type)} 비교에 사용할 포함 최대값입니다.";
    }

    private static string GetExpectedBoolTooltip(BossEncounterDialogueConditionType type)
    {
        return $"{GetMeasuredValueName(type)} 조건에서 기대하는 true/false 값입니다.";
    }

    private static string GetStringValueTooltip(BossEncounterDialogueConditionType type)
    {
        switch (type)
        {
            case BossEncounterDialogueConditionType.PlayerHasWeapon:
            case BossEncounterDialogueConditionType.PlayerHasUnlockedWeapon:
                return "검사할 무기 ID입니다. WeaponDefinition의 weaponId 값을 사용합니다.";
            case BossEncounterDialogueConditionType.PlayerHasRelic:
            case BossEncounterDialogueConditionType.PlayerHasUnlockedRelic:
                return "검사할 유물 ID입니다. RelicDefinition의 relicId 값을 사용합니다.";
            default:
                return "이 조건에서 사용할 문자열 식별자입니다.";
        }
    }

    private static string GetMeasuredValueName(BossEncounterDialogueConditionType type)
    {
        switch (type)
        {
            case BossEncounterDialogueConditionType.EncounterCount:
                return "보스 조우 횟수";
            case BossEncounterDialogueConditionType.HasMetBoss:
                return "보스 조우 여부";
            case BossEncounterDialogueConditionType.BossVictoryCount:
                return "보스 승리 횟수";
            case BossEncounterDialogueConditionType.BossDefeatCount:
                return "보스 패배 횟수";
            case BossEncounterDialogueConditionType.Affection:
                return "호감도 수치";
            case BossEncounterDialogueConditionType.RunRemainingSeconds:
                return "런 남은 시간";
            case BossEncounterDialogueConditionType.RunRemainingRatio01:
                return "런 남은 시간 비율";
            case BossEncounterDialogueConditionType.RunElapsedSeconds:
                return "런 경과 시간";
            case BossEncounterDialogueConditionType.PlayerHealth:
                return "플레이어 체력";
            case BossEncounterDialogueConditionType.PlayerHealthRatio01:
                return "플레이어 체력 비율";
            case BossEncounterDialogueConditionType.ClearCount:
                return "클리어 횟수";
            case BossEncounterDialogueConditionType.MagicStone:
                return "마석 수";
            case BossEncounterDialogueConditionType.LastRunEndReason:
                return "최근 런 종료 사유";
            case BossEncounterDialogueConditionType.PlayerHasWeapon:
                return "플레이어 무기 보유 여부";
            case BossEncounterDialogueConditionType.PlayerHasRelic:
                return "플레이어 유물 보유 여부";
            case BossEncounterDialogueConditionType.PlayerHasUnlockedWeapon:
                return "무기 해금 여부";
            case BossEncounterDialogueConditionType.PlayerHasUnlockedRelic:
                return "유물 해금 여부";
            case BossEncounterDialogueConditionType.PlayerWeaponCount:
                return "플레이어 무기 개수";
            case BossEncounterDialogueConditionType.PlayerRelicCount:
                return "플레이어 유물 개수";
            case BossEncounterDialogueConditionType.BackpackItemCount:
                return "가방 아이템 개수";
            case BossEncounterDialogueConditionType.BackpackIsFull:
                return "가방이 가득 찼는지 여부";
            default:
                return "런타임 값";
        }
    }

    private static void Advance(ref Rect line)
    {
        line.y += EditorGUIUtility.singleLineHeight + Spacing;
    }
}
