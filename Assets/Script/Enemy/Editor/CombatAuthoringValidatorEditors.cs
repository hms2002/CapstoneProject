using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임 :
/// - authoring validator가 보여줄 결과의 심각도를 공통 기준으로 표현한다.
/// - 인스펙터와 메뉴 검사 출력이 같은 우선순위 체계를 공유하게 한다.
/// </summary>
public enum AuthoringValidationSeverity
{
    Info,
    Warning,
    Error
}

/// <summary>
/// 책임 :
/// - authoring validator가 수집한 단일 경고/오류 메시지를 한 항목으로 표현한다.
/// - 인스펙터 출력과 콘솔 보고가 같은 데이터 구조를 재사용하게 한다.
/// </summary>
public readonly struct AuthoringValidationMessage
{
    public readonly AuthoringValidationSeverity Severity;
    public readonly string Message;

    public AuthoringValidationMessage(AuthoringValidationSeverity severity, string message)
    {
        Severity = severity;
        Message = message;
    }
}

/// <summary>
/// 책임 :
/// - 전투 오브젝트와 일반 몬스터 authoring에 필요한 필수/선택 구성을 공통 규칙으로 검사한다.
/// - 에디터 인스펙터와 수동 메뉴 검사가 같은 검증 로직을 공유하게 한다.
/// </summary>
public static class CombatAuthoringValidationUtility
{
    public static List<AuthoringValidationMessage> CollectCombatObjectMessages(GameObject root)
    {
        var results = new List<AuthoringValidationMessage>();
        if (root == null)
            return results;

        bool looksLikeAttackObject = IsAttackAuthoringObject(root);
        bool looksLikeCombatObject =
            root.GetComponent<Enemy>() != null ||
            root.GetComponent<AbilitySystem>() != null ||
            root.GetComponent<PlayerInteractor2D>() != null;

        if (!looksLikeCombatObject && !looksLikeAttackObject)
            return results;

        ValidateAttackObjectHurtboxRules(root, results);

        if (!looksLikeCombatObject)
            return results;

        ValidateRequiredComponent<AbilitySystem>(root, results, "AbilitySystem이 없습니다 -> ASC 기반 실행이 동작하지 않습니다.");
        ValidateRequiredComponent<TagSystem>(root, results, "TagSystem이 없습니다 -> 상태 태그 기반 분기와 제압 규칙이 흔들릴 수 있습니다.");
        ValidateRequiredComponent<AttributeSet>(root, results, "AttributeSet이 없습니다 -> 체력/스탯 기반 전투 계산이 정상 동작하지 않습니다.");
        ValidateRequiredComponent<GameplayEffectRunner>(root, results, "GameplayEffectRunner가 없습니다 -> GE 적용/갱신 경로가 비활성일 수 있습니다.");

        if (root.GetComponentInChildren<CombatHurtbox2D>(true) == null)
        {
            results.Add(new AuthoringValidationMessage(
                AuthoringValidationSeverity.Error,
                "CombatHurtbox2D가 없습니다 -> 피해 판정 시스템은 더 이상 부모 Player/Enemy를 추측하지 않으므로 명시 허트박스가 필수입니다."));
        }
        else
        {
            ValidateCombatHurtboxRules(root, results);
        }

        return results;
    }

    public static List<AuthoringValidationMessage> CollectMobMessages(GameObject root)
    {
        var results = CollectCombatObjectMessages(root);
        if (root == null)
            return results;

        Mob mob = root.GetComponent<Mob>();
        if (mob == null)
            return results;

        ValidateRequiredComponent<MobAbilityCoordinator>(root, results, "MobAbilityCoordinator가 없습니다 -> 일반 몬스터 FSM이 ASC 실행을 시작할 수 없습니다.");

        if (!HasComponentImplementing<IMobAttackDecisionSource>(root))
        {
            results.Add(new AuthoringValidationMessage(
                AuthoringValidationSeverity.Error,
                "IMobAttackDecisionSource 구현체가 없습니다 -> AttackState가 공격 요청을 만들 수 없습니다."));
        }

        if (!HasComponentImplementing<IEnemyChaseIntent>(root))
        {
            results.Add(new AuthoringValidationMessage(
                AuthoringValidationSeverity.Warning,
                "IEnemyChaseIntent 구현체가 없습니다 -> ChaseState는 추적 이동 없이 동작합니다."));
        }

        StaggerGaugeSystem staggerGaugeSystem = root.GetComponent<StaggerGaugeSystem>();
        if (staggerGaugeSystem == null)
        {
            results.Add(new AuthoringValidationMessage(
                AuthoringValidationSeverity.Info,
                "StaggerGaugeSystem이 없습니다 -> 이 몬스터는 Stagger 상태를 실제로 사용하지 않습니다."));
        }
        else
        {
            if (staggerGaugeSystem.staggeredEffect == null)
            {
                results.Add(new AuthoringValidationMessage(
                    AuthoringValidationSeverity.Warning,
                    "StaggerGaugeSystem의 staggeredEffect가 비어 있습니다 -> 그로기 트리거가 발생해도 FSM StaggerState가 진입하지 않을 수 있습니다."));
            }

            if (staggerGaugeSystem.resistancePercentAttribute == null)
            {
                results.Add(new AuthoringValidationMessage(
                    AuthoringValidationSeverity.Info,
                    "StaggerResistance attribute가 비어 있습니다 -> 스태거 면역/저항 authoring이 필요하면 연결하세요."));
            }
        }

        return results;
    }

    private static void ValidateRequiredComponent<T>(
        GameObject root,
        List<AuthoringValidationMessage> results,
        string missingMessage) where T : Component
    {
        if (root.GetComponent<T>() != null)
            return;

        results.Add(new AuthoringValidationMessage(AuthoringValidationSeverity.Error, missingMessage));
    }

    private static bool HasComponentImplementing<T>(GameObject root) where T : class
    {
        MonoBehaviour[] behaviours = root.GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is T)
                return true;
        }

        return false;
    }

    /// <summary>
    /// 책임 :
    /// - 선택된 authoring 루트가 피해를 주는 공격 actor인지 판정한다.
    /// - 공격 actor 아래에 허트박스가 들어가는 잘못된 프리팹 구성을 validator가 잡을 수 있게 한다.
    /// </summary>
    private static bool IsAttackAuthoringObject(GameObject root)
    {
        if (root == null)
            return false;

        return root.GetComponentInChildren<AttackBase>(true) != null ||
               root.GetComponentInChildren<FragmentShardActor>(true) != null ||
               root.name.Contains("Hitbox") ||
               root.name.Contains("AttackEffect");
    }

    /// <summary>
    /// 책임 :
    /// - 공격 actor/이펙트 계층이 피해 수신용 허트박스를 함께 들고 있는지 검사한다.
    /// - 플레이어 자식으로 붙은 공격 이펙트가 장판 피해 대상으로 오인되는 authoring을 사전에 차단한다.
    /// </summary>
    private static void ValidateAttackObjectHurtboxRules(GameObject root, List<AuthoringValidationMessage> results)
    {
        if (!IsAttackAuthoringObject(root))
            return;

        CombatHurtbox2D[] hurtboxes = root.GetComponentsInChildren<CombatHurtbox2D>(true);
        if (hurtboxes == null || hurtboxes.Length == 0)
            return;

        results.Add(new AuthoringValidationMessage(
            AuthoringValidationSeverity.Error,
            "공격 actor/이펙트 계층에 CombatHurtbox2D가 있습니다 -> 공격체는 피해를 주는 객체이며, 피해를 받는 허트박스를 소유하면 안 됩니다."));
    }

    /// <summary>
    /// 책임 :
    /// - 전투 객체의 허트박스가 실제 피해 수신용 콜라이더로 authoring 되었는지 검사한다.
    /// - 허트박스가 공격 actor 하위로 섞이는 구조적 실수를 제작 단계에서 드러낸다.
    /// </summary>
    private static void ValidateCombatHurtboxRules(GameObject root, List<AuthoringValidationMessage> results)
    {
        CombatHurtbox2D[] hurtboxes = root.GetComponentsInChildren<CombatHurtbox2D>(true);
        for (int i = 0; i < hurtboxes.Length; i++)
        {
            CombatHurtbox2D hurtbox = hurtboxes[i];
            if (hurtbox == null)
                continue;

            if (hurtbox.GetComponent<Collider2D>() == null)
            {
                results.Add(new AuthoringValidationMessage(
                    AuthoringValidationSeverity.Error,
                    $"'{hurtbox.name}' CombatHurtbox2D와 같은 GameObject에 Collider2D가 없습니다 -> resolver가 이 허트박스를 직접 피해 대상으로 인식할 수 없습니다."));
            }

            if (hurtbox.GetComponentInParent<AttackBase>() != null ||
                hurtbox.GetComponentInParent<FragmentShardActor>() != null)
            {
                results.Add(new AuthoringValidationMessage(
                    AuthoringValidationSeverity.Error,
                    $"'{hurtbox.name}' CombatHurtbox2D가 공격 actor 하위에 있습니다 -> 피해 수신 허트박스와 공격 이펙트/히트박스를 분리하세요."));
            }
        }
    }
}

/// <summary>
/// 책임 :
/// - Enemy 및 파생 일반 몬스터 인스펙터에서 authoring 검증 결과를 즉시 보여준다.
/// - 제작 중 빠뜨린 필수/선택 구성을 문서 대신 인스펙터에서 먼저 발견하게 돕는다.
/// </summary>
[CustomEditor(typeof(Enemy), true)]
[CanEditMultipleObjects]
public class EnemyAuthoringValidatorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (serializedObject.isEditingMultipleObjects)
            return;

        if (target is not Component component)
            return;

        GameObject root = component.gameObject;
        DrawValidationSection("Combat Object Validation", CombatAuthoringValidationUtility.CollectCombatObjectMessages(root));

        if (component is Mob)
            DrawValidationSection("Mob FSM Validation", CombatAuthoringValidationUtility.CollectMobMessages(root));
    }

    private static void DrawValidationSection(string title, List<AuthoringValidationMessage> messages)
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

        if (messages == null || messages.Count == 0)
        {
            EditorGUILayout.HelpBox("문제 없음: 현재 기준으로는 빠진 필수 authoring 구성이 보이지 않습니다.", MessageType.Info);
            return;
        }

        for (int i = 0; i < messages.Count; i++)
        {
            AuthoringValidationMessage message = messages[i];
            EditorGUILayout.HelpBox(message.Message, ToMessageType(message.Severity));
        }
    }

    private static MessageType ToMessageType(AuthoringValidationSeverity severity)
    {
        return severity switch
        {
            AuthoringValidationSeverity.Error => MessageType.Error,
            AuthoringValidationSeverity.Warning => MessageType.Warning,
            _ => MessageType.Info
        };
    }
}

/// <summary>
/// 책임 :
/// - 현재 선택한 전투 오브젝트를 수동으로 일괄 검사하는 에디터 진입점을 제공한다.
/// - 인스펙터를 열지 않아도 콘솔 한 번에 combat/mob authoring 누락을 점검하게 돕는다.
/// </summary>
public static class CombatAuthoringValidationMenu
{
    [MenuItem("Tools/Validation/Validate Selected Combat Object")]
    private static void ValidateSelectedCombatObject()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("Combat Validation", "선택된 GameObject가 없습니다.", "확인");
            return;
        }

        List<AuthoringValidationMessage> combatMessages = CombatAuthoringValidationUtility.CollectCombatObjectMessages(selected);
        List<AuthoringValidationMessage> mobMessages = CombatAuthoringValidationUtility.CollectMobMessages(selected);

        var builder = new StringBuilder();
        AppendMessages(builder, "Combat Object Validation", combatMessages);
        if (selected.GetComponent<Mob>() != null)
            AppendMessages(builder, "Mob FSM Validation", mobMessages);

        if (builder.Length == 0)
            builder.AppendLine("문제 없음: 현재 기준으로는 빠진 필수 authoring 구성이 보이지 않습니다.");

        Debug.Log($"[CombatAuthoringValidation]\n{builder}", selected);
        EditorUtility.DisplayDialog("Combat Validation", "검사 결과를 콘솔에 출력했습니다.", "확인");
    }

    [MenuItem("Tools/Validation/Validate Selected Combat Object", true)]
    private static bool CanValidateSelectedCombatObject()
    {
        return Selection.activeGameObject != null;
    }

    private static void AppendMessages(StringBuilder builder, string title, List<AuthoringValidationMessage> messages)
    {
        if (messages == null || messages.Count == 0)
            return;

        builder.AppendLine(title);
        for (int i = 0; i < messages.Count; i++)
        {
            AuthoringValidationMessage message = messages[i];
            builder.Append("- [");
            builder.Append(message.Severity);
            builder.Append("] ");
            builder.AppendLine(message.Message);
        }

        builder.AppendLine();
    }
}
