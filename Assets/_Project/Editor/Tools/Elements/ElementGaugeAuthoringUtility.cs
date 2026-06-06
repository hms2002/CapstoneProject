using UnityEditor;
using UnityEngine;
using UnityGAS;

/// <summary>
/// 책임:
/// - 전투 본체 프리팹에 공통 속성 게이지 런타임/뷰 authoring을 일관된 기본값으로 보강한다.
/// - 생성기와 validator가 같은 catalog/view prefab 기준을 공유하게 한다.
/// </summary>
public static class ElementGaugeAuthoringUtility
{
    private const string CatalogPath = "Assets/_Project/Data/Attributes/ElementGauges/Element Gauge Catalog.asset";
    private const string ViewPrefabPath = "Assets/_Project/Prefabs/UI/ElementGaugeUI/Canvas_MonstarElementGaugeView.prefab";

    public static ElementGaugeCatalog LoadCatalog()
    {
        return AssetDatabase.LoadAssetAtPath<ElementGaugeCatalog>(CatalogPath);
    }

    public static MonsterElementGaugeView LoadViewPrefab()
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ViewPrefabPath);
        return prefab != null ? prefab.GetComponent<MonsterElementGaugeView>() : null;
    }

    public static void EnsureDefaultElementGaugeAuthoring(GameObject root)
    {
        if (root == null)
            return;

        ConfigureElementGauge(root);
        ConfigureGaugeViewInstaller(root);
    }

    public static bool IsElementGaugeAuthoringComplete(GameObject root)
    {
        if (root == null)
            return false;

        ElementGaugeSystem gauge = root.GetComponent<ElementGaugeSystem>();
        MonsterElementGaugeViewInstaller installer = root.GetComponent<MonsterElementGaugeViewInstaller>();
        if (gauge == null || installer == null)
            return false;

        SerializedObject gaugeSerialized = new(gauge);
        SerializedObject installerSerialized = new(installer);
        return HasObjectReference(gaugeSerialized, "catalog") &&
               HasObjectReference(installerSerialized, "viewPrefab");
    }

    private static void ConfigureElementGauge(GameObject root)
    {
        ElementGaugeSystem gauge = root.GetComponent<ElementGaugeSystem>();
        if (gauge == null)
            gauge = root.AddComponent<ElementGaugeSystem>();

        ElementGaugeCatalog catalog = LoadCatalog();
        SerializedObject serialized = new(gauge);
        SetObject(serialized, "catalog", catalog);
        SetBool(serialized, "useDecay", true);
        SetFloat(serialized, "decayDelaySeconds", 3f);
        SetFloat(serialized, "decayPercentPerSecond", 0.02f);
        SetBool(serialized, "allowOverflowCarry", true);
        SetBool(serialized, "logWhenTriggered", false);
        SetBool(serialized, "logMissingDefinition", true);
        ConfigureRuntimeStates(serialized, catalog);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(gauge);
    }

    private static void ConfigureGaugeViewInstaller(GameObject root)
    {
        MonsterElementGaugeViewInstaller installer = root.GetComponent<MonsterElementGaugeViewInstaller>();
        if (installer == null)
            installer = root.AddComponent<MonsterElementGaugeViewInstaller>();

        SerializedObject serialized = new(installer);
        SetObject(serialized, "viewPrefab", LoadViewPrefab());
        SetObject(serialized, "uiParentOverride", root.transform);
        SetBool(serialized, "installOnStart", true);
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(installer);
    }

    private static void ConfigureRuntimeStates(SerializedObject serialized, ElementGaugeCatalog catalog)
    {
        SerializedProperty runtimeStates = serialized.FindProperty("runtimeStates");
        if (runtimeStates == null || !runtimeStates.isArray || catalog == null || catalog.definitions == null)
            return;

        runtimeStates.arraySize = catalog.definitions.Length;
        for (int i = 0; i < catalog.definitions.Length; i++)
        {
            SerializedProperty state = runtimeStates.GetArrayElementAtIndex(i);
            SetRelativeObject(state, "definition", catalog.definitions[i]);
            SetRelativeFloat(state, "currentBuildUp", 0f);
            SetRelativeFloat(state, "lastBuildUpTime", -999f);
            SetRelativeObject(state, "sustainVfxInstance", null);
            SetRelativeBool(state, "uiVisible", true);
        }
    }

    private static bool HasObjectReference(SerializedObject serialized, string propertyName)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        return property != null && property.objectReferenceValue != null;
    }

    private static void SetObject(SerializedObject serialized, string propertyName, Object value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetBool(SerializedObject serialized, string propertyName, bool value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetFloat(SerializedObject serialized, string propertyName, float value)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property != null)
            property.floatValue = value;
    }

    private static void SetRelativeObject(SerializedProperty parent, string propertyName, Object value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
            property.objectReferenceValue = value;
    }

    private static void SetRelativeBool(SerializedProperty parent, string propertyName, bool value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
            property.boolValue = value;
    }

    private static void SetRelativeFloat(SerializedProperty parent, string propertyName, float value)
    {
        SerializedProperty property = parent.FindPropertyRelative(propertyName);
        if (property != null)
            property.floatValue = value;
    }
}


