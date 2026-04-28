#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using CapstoneAudio;
using UnityEditor;
using UnityEngine;
using UnityGAS;

namespace CapstonePresentation.EditorTools
{
    public sealed class CueCatalogWindow : EditorWindow
    {
        private const string DefaultCatalogAssetPath =
            "Assets/LeeJunMo/Datas/Resources/Presentation/DefaultCueCatalog.asset";
        private const string DefaultWorkbenchProfileAssetPath =
            "Assets/LeeJunMo/Datas/Editor/Presentation/DefaultPresentationWorkbenchProfile.asset";

        private enum WorkbenchTab
        {
            CueCatalog,
            AbilityPresentations
        }

        private enum RegisteredTargetKind
        {
            AbilityDefinition,
            AbilityLogic
        }

        private enum PreviewFieldKind
        {
            WorldPresentation,
            SpawnedPresentation,
            Sound,
            CameraShake,
            Prefab
        }

        private sealed class PreviewField
        {
            public string propertyPath;
            public string label;
            public PreviewFieldKind kind;
        }

        private sealed class PresentationSlot
        {
            public string key;
            public string displayName;
            public readonly List<PreviewField> fields = new();
        }

        private CueCatalogSO selectedCatalog;
        private SerializedObject serializedCatalog;
        private SerializedProperty entriesProperty;

        private WorkbenchTab selectedTab;
        private string searchQuery = string.Empty;
        private int selectedIndex = -1;
        private Vector2 listScroll;
        private Vector2 detailScroll;

        private PresentationWorkbenchProfileSO selectedProfile;
        private SerializedObject serializedProfile;
        private SerializedProperty profileDefinitionsProperty;
        private SerializedProperty profileLogicsProperty;
        private UnityEngine.Object pendingRegistrationTarget;
        private RegisteredTargetKind selectedRegisteredTargetKind;
        private int selectedRegisteredTargetIndex = -1;
        private string abilitySearchQuery = string.Empty;
        private Vector2 abilityListScroll;
        private Vector2 abilityDetailScroll;
        private Vector2 prefabInspectorScroll;
        private GameObject inspectedPrefab;
        private readonly Dictionary<string, bool> slotFoldouts = new();
        private readonly Dictionary<UnityEngine.Object, UnityEditor.Editor> inlineEditors = new();

        [MenuItem("Tools/Presentation/Cue Catalog")]
        public static void OpenWindow()
        {
            GetWindow<CueCatalogWindow>("Cue Catalog").Show();
        }

        public static void OpenWindow(CueCatalogSO catalog)
        {
            CueCatalogWindow window = GetWindow<CueCatalogWindow>("Cue Catalog");
            window.Show();
            window.SetCatalog(catalog);
        }

        private void OnEnable()
        {
            if (selectedCatalog == null)
            {
                IReadOnlyList<CueCatalogSO> catalogs = CueCatalogEditorUtility.FindCatalogs();
                if (catalogs.Count > 0)
                    SetCatalog(catalogs[0]);
            }

            if (selectedProfile == null)
                SetProfile(CreateOrLoadDefaultWorkbenchProfile());
        }

        private void OnDisable()
        {
            CueCatalogPreviewUtility.StopPreview();
            ClearInlineEditors();
        }

        private void OnGUI()
        {
            DrawTabSelector();

            switch (selectedTab)
            {
                case WorkbenchTab.CueCatalog:
                    DrawCueCatalogTab();
                    break;
                case WorkbenchTab.AbilityPresentations:
                    DrawAbilityPresentationTab();
                    break;
            }
        }

        private void DrawTabSelector()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            selectedTab = (WorkbenchTab)GUILayout.Toolbar(
                (int)selectedTab,
                new[] { "Cue Catalog", "AL Presentations" },
                EditorStyles.toolbarButton,
                GUILayout.Width(260f));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawCueCatalogTab()
        {
            DrawCatalogSelector();

            if (selectedCatalog == null)
            {
                DrawEmptyState();
                return;
            }

            BindSerializedCatalog();
            serializedCatalog.Update();

            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawEntryListPanel();
            DrawEntryDetailPanel();
            EditorGUILayout.EndHorizontal();

            if (serializedCatalog.ApplyModifiedProperties())
            {
                selectedCatalog.MarkLookupDirty();
                EditorUtility.SetDirty(selectedCatalog);
                CueCatalogEditorUtility.InvalidateCache();
            }
        }

        private void DrawCatalogSelector()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            CueCatalogSO newCatalog = (CueCatalogSO)EditorGUILayout.ObjectField(
                "Catalog",
                selectedCatalog,
                typeof(CueCatalogSO),
                false);

            if (newCatalog != selectedCatalog)
                SetCatalog(newCatalog);

            if (GUILayout.Button("Default", GUILayout.Width(72f)))
                SetCatalog(CreateOrLoadDefaultCatalog());

            if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
            {
                CueCatalogEditorUtility.InvalidateCache();
                if (selectedCatalog != null)
                    SetCatalog(selectedCatalog);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.HelpBox(
                "CueCatalogSO asset is required. Create a default catalog to organize reusable cue assets.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Create Default Catalog", GUILayout.Width(180f)))
                SetCatalog(CreateOrLoadDefaultCatalog());
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search", GUILayout.Width(44f));
            searchQuery = GUILayout.TextField(searchQuery, GUILayout.MinWidth(120f));

            if (GUILayout.Button("Add Entry", EditorStyles.toolbarButton, GUILayout.Width(72f)))
                AddEntry();

            if (GUILayout.Button("Validate", EditorStyles.toolbarButton, GUILayout.Width(64f)))
                ValidateSelectedCatalog();

            using (new EditorGUI.DisabledScope(!CueCatalogPreviewUtility.CanPreview))
            {
                if (GUILayout.Button("Stop Preview", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    CueCatalogPreviewUtility.StopPreview();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Cue is a reusable finished presentation preset. You can edit its internal presentation here and preview it inside this tool window.",
                MessageType.None);
        }

        private void DrawEntryListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Max(300f, position.width * 0.36f)));
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);

            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            List<int> filteredIndices = GetFilteredIndices();

            if (filteredIndices.Count == 0)
                EditorGUILayout.HelpBox("No entries matched the current filter.", MessageType.None);

            for (int i = 0; i < filteredIndices.Count; i++)
            {
                int entryIndex = filteredIndices[i];
                SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(entryIndex);
                string key = entry.FindPropertyRelative("key").stringValue;
                UnityEngine.Object cueObject = entry.FindPropertyRelative("cue").objectReferenceValue;
                string cueName = cueObject != null ? cueObject.name : "<missing cue>";

                GUIStyle style = entryIndex == selectedIndex ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (GUILayout.Button($"{key}  [{cueName}]", style))
                    selectedIndex = entryIndex;
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawEntryDetailPanel()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Entry Detail", EditorStyles.boldLabel);

            if (selectedIndex < 0 || selectedIndex >= entriesProperty.arraySize)
            {
                EditorGUILayout.HelpBox("Select an entry to edit.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(selectedIndex);
            PresentationCueSO cue = entry.FindPropertyRelative("cue").objectReferenceValue as PresentationCueSO;

            EditorGUILayout.BeginHorizontal();
            DrawPreviewPanel(cue);

            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);
            DrawEntryFields(entry);

            EditorGUILayout.Space(10f);
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete Entry"))
                DeleteSelectedEntry();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewPanel(PresentationCueSO cue)
        {
            float previewWidth = Mathf.Clamp(position.width * 0.23f, 240f, 360f);

            EditorGUILayout.BeginVertical(GUILayout.Width(previewWidth));
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            Rect previewRect = GUILayoutUtility.GetRect(previewWidth - 8f, previewWidth - 8f, GUILayout.ExpandWidth(true));
            CueCatalogPreviewUtility.DrawPreview(previewRect, cue);

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(cue == null || !CueCatalogPreviewUtility.CanPreview))
            {
                if (GUILayout.Button("Play Cue"))
                    CueCatalogPreviewUtility.PlayCue(cue);
            }

            using (new EditorGUI.DisabledScope(!CueCatalogPreviewUtility.IsPreviewing(cue)))
            {
                if (GUILayout.Button("Stop Cue"))
                    CueCatalogPreviewUtility.StopPreview();
            }

            using (new EditorGUI.DisabledScope(cue == null))
            {
                if (GUILayout.Button("Open Asset"))
                    Selection.activeObject = cue;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawEntryFields(SerializedProperty entry)
        {
            SerializedProperty keyProperty = entry.FindPropertyRelative("key");
            SerializedProperty cueProperty = entry.FindPropertyRelative("cue");

            EditorGUILayout.PropertyField(keyProperty);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(cueProperty);

            if (cueProperty.objectReferenceValue == null)
            {
                if (GUILayout.Button("Create Cue", GUILayout.Width(88f)))
                    CreateCueAssetForEntry(entry);
            }
            else
            {
                if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                    EditorGUIUtility.PingObject(cueProperty.objectReferenceValue);
            }

            EditorGUILayout.EndHorizontal();

            PresentationCueSO cue = cueProperty.objectReferenceValue as PresentationCueSO;
            if (cue == null)
            {
                EditorGUILayout.HelpBox("Cue asset is required to edit or preview this entry.", MessageType.Info);
                return;
            }

            SerializedObject cueSerializedObject = new SerializedObject(cue);
            cueSerializedObject.Update();

            SerializedProperty presentationProperty = cueSerializedObject.FindProperty("presentation");
            if (presentationProperty != null)
            {
                EditorGUILayout.Space(4f);
                EditorGUILayout.LabelField("Cue Presentation", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(presentationProperty, includeChildren: true);
            }

            if (cueSerializedObject.ApplyModifiedProperties())
            {
                EditorUtility.SetDirty(cue);
                selectedCatalog.MarkLookupDirty();
            }
        }

        private void DrawAbilityPresentationTab()
        {
            DrawWorkbenchProfileSelector();

            if (selectedProfile == null)
            {
                EditorGUILayout.HelpBox(
                    "Presentation Workbench profile is required to register AbilityDefinition or AbilityLogic assets.",
                    MessageType.Info);
                return;
            }

            BindSerializedProfile();
            serializedProfile.Update();

            DrawAbilityPresentationToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawRegisteredTargetListPanel();
            DrawRegisteredTargetDetailPanel();
            EditorGUILayout.EndHorizontal();

            if (serializedProfile.ApplyModifiedProperties())
                EditorUtility.SetDirty(selectedProfile);
        }

        private void DrawWorkbenchProfileSelector()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            PresentationWorkbenchProfileSO newProfile = (PresentationWorkbenchProfileSO)EditorGUILayout.ObjectField(
                "Profile",
                selectedProfile,
                typeof(PresentationWorkbenchProfileSO),
                false);

            if (newProfile != selectedProfile)
                SetProfile(newProfile);

            if (GUILayout.Button("Default", GUILayout.Width(72f)))
                SetProfile(CreateOrLoadDefaultWorkbenchProfile());

            if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
                BindSerializedProfile();

            EditorGUILayout.EndHorizontal();
        }

        private void DrawAbilityPresentationToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

            GUILayout.Label("Search", GUILayout.Width(44f));
            abilitySearchQuery = GUILayout.TextField(abilitySearchQuery, GUILayout.MinWidth(120f));

            GUILayout.Space(8f);
            pendingRegistrationTarget = EditorGUILayout.ObjectField(
                pendingRegistrationTarget,
                typeof(UnityEngine.Object),
                false,
                GUILayout.MinWidth(160f));

            using (new EditorGUI.DisabledScope(pendingRegistrationTarget == null))
            {
                if (GUILayout.Button("Add Target", EditorStyles.toolbarButton, GUILayout.Width(80f)))
                    RegisterTarget(pendingRegistrationTarget);
            }

            if (GUILayout.Button("Add Selection", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                RegisterTarget(Selection.activeObject);

            if (GUILayout.Button("Scan Project", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                ScanProjectForAbilityTargets();

            using (new EditorGUI.DisabledScope(!CueCatalogPreviewUtility.CanPreview))
            {
                if (GUILayout.Button("Stop Preview", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    CueCatalogPreviewUtility.StopPreview();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Register AbilityDefinition or AbilityLogic assets. The tool scans each target for editable presentation fields and groups them into previewable slots.",
                MessageType.None);
        }

        private void DrawRegisteredTargetListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Max(320f, position.width * 0.34f)));
            EditorGUILayout.LabelField("Registered Targets", EditorStyles.boldLabel);

            abilityListScroll = EditorGUILayout.BeginScrollView(abilityListScroll);

            DrawRegisteredTargetSection(
                "Ability Definitions",
                profileDefinitionsProperty,
                RegisteredTargetKind.AbilityDefinition);

            EditorGUILayout.Space(8f);

            DrawRegisteredTargetSection(
                "Ability Logics",
                profileLogicsProperty,
                RegisteredTargetKind.AbilityLogic);

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawRegisteredTargetSection(
            string title,
            SerializedProperty listProperty,
            RegisteredTargetKind kind)
        {
            EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);

            if (listProperty == null || listProperty.arraySize == 0)
            {
                EditorGUILayout.HelpBox("No registered assets.", MessageType.None);
                return;
            }

            string filter = string.IsNullOrWhiteSpace(abilitySearchQuery)
                ? string.Empty
                : abilitySearchQuery.Trim().ToLowerInvariant();

            for (int i = 0; i < listProperty.arraySize; i++)
            {
                SerializedProperty element = listProperty.GetArrayElementAtIndex(i);
                UnityEngine.Object target = element.objectReferenceValue;
                string label = BuildRegisteredTargetLabel(target);

                if (!string.IsNullOrEmpty(filter) &&
                    !label.ToLowerInvariant().Contains(filter))
                {
                    continue;
                }

                GUIStyle style = selectedRegisteredTargetKind == kind && selectedRegisteredTargetIndex == i
                    ? EditorStyles.miniButtonMid
                    : EditorStyles.miniButton;

                if (GUILayout.Button(label, style))
                {
                    selectedRegisteredTargetKind = kind;
                    selectedRegisteredTargetIndex = i;
                    inspectedPrefab = null;
                    ClearInlineEditors();
                }
            }
        }

        private void DrawRegisteredTargetDetailPanel()
        {
            EditorGUILayout.BeginVertical();
            EditorGUILayout.LabelField("Presentation Detail", EditorStyles.boldLabel);

            UnityEngine.Object target = GetSelectedRegisteredTarget();
            if (target == null)
            {
                EditorGUILayout.HelpBox("Select an AbilityDefinition or AbilityLogic to edit its presentation fields.", MessageType.Info);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            DrawAbilityPreviewPanel(target);

            abilityDetailScroll = EditorGUILayout.BeginScrollView(abilityDetailScroll);

            DrawRegisteredTargetHeader(target);

            if (target is AbilityDefinition definition)
                DrawAbilityDefinitionPresentationDetails(definition, target);
            else
                DrawPresentationObject(target, target.name, target);

            DrawInlinePrefabInspector();

            EditorGUILayout.Space(10f);
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Remove From Profile"))
                RemoveSelectedRegisteredTarget();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawAbilityPreviewPanel(UnityEngine.Object owner)
        {
            float previewWidth = Mathf.Clamp(position.width * 0.22f, 240f, 340f);

            EditorGUILayout.BeginVertical(GUILayout.Width(previewWidth));
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            Rect previewRect = GUILayoutUtility.GetRect(previewWidth - 8f, previewWidth - 8f, GUILayout.ExpandWidth(true));
            CueCatalogPreviewUtility.DrawPreview(previewRect, owner, "Preview a field or slot.");

            EditorGUILayout.Space(6f);
            using (new EditorGUI.DisabledScope(!CueCatalogPreviewUtility.IsPreviewing(owner)))
            {
                if (GUILayout.Button("Stop Preview"))
                    CueCatalogPreviewUtility.StopPreview();
            }

            using (new EditorGUI.DisabledScope(owner == null))
            {
                if (GUILayout.Button("Open Asset"))
                    Selection.activeObject = owner;
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawRegisteredTargetHeader(UnityEngine.Object target)
        {
            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Target", target, target.GetType(), false);
            }

            EditorGUILayout.Space(4f);
        }

        private void DrawAbilityDefinitionPresentationDetails(
            AbilityDefinition definition,
            UnityEngine.Object previewOwner)
        {
            SerializedObject definitionObject = new SerializedObject(definition);
            definitionObject.Update();

            SerializedProperty logicProperty = definitionObject.FindProperty("logic");
            SerializedProperty sourceObjectProperty = definitionObject.FindProperty("sourceObject");

            EditorGUILayout.LabelField("Ability Links", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(logicProperty);
            EditorGUILayout.PropertyField(sourceObjectProperty);

            if (definitionObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(definition);

            EditorGUILayout.Space(8f);
            DrawPresentationObject(definition, "Definition Phase Presentation", previewOwner);

            AbilityLogic logic = definition.logic;
            if (logic != null)
            {
                EditorGUILayout.Space(8f);
                DrawPresentationObject(logic, "Logic Presentation", previewOwner);
            }
            else
            {
                EditorGUILayout.HelpBox("This AbilityDefinition has no AbilityLogic assigned.", MessageType.None);
            }

            UnityEngine.Object sourceObject = definition.sourceObject;
            if (sourceObject != null && sourceObject != logic)
            {
                EditorGUILayout.Space(8f);
                DrawPresentationObject(sourceObject, "Source Object Presentation", previewOwner);
            }
        }

        private void DrawPresentationObject(
            UnityEngine.Object target,
            string title,
            UnityEngine.Object previewOwner)
        {
            if (target == null)
                return;

            if (target is GameObject gameObject)
            {
                DrawPrefabInspectActions(gameObject, title);
                return;
            }

            SerializedObject targetObject;
            try
            {
                targetObject = new SerializedObject(target);
            }
            catch (ArgumentException)
            {
                EditorGUILayout.HelpBox($"{title} cannot be inspected by SerializedObject.", MessageType.None);
                return;
            }

            targetObject.Update();
            List<PresentationSlot> slots = BuildPresentationSlots(targetObject);

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Asset", target, target.GetType(), false);
            }

            if (slots.Count == 0)
            {
                EditorGUILayout.HelpBox("No previewable presentation fields were found on this object.", MessageType.None);
                return;
            }

            for (int i = 0; i < slots.Count; i++)
                DrawPresentationSlot(targetObject, target, previewOwner, slots[i]);

            if (targetObject.ApplyModifiedProperties())
                EditorUtility.SetDirty(target);
        }

        private void DrawPresentationSlot(
            SerializedObject serializedTarget,
            UnityEngine.Object valueOwner,
            UnityEngine.Object previewOwner,
            PresentationSlot slot)
        {
            string foldoutKey = $"{valueOwner.GetInstanceID()}:{slot.key}";
            bool expanded = !slotFoldouts.TryGetValue(foldoutKey, out bool isExpanded) || isExpanded;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();

            expanded = EditorGUILayout.Foldout(expanded, slot.displayName, true, EditorStyles.foldoutHeader);
            slotFoldouts[foldoutKey] = expanded;

            using (new EditorGUI.DisabledScope(slot.fields.Count == 0))
            {
                if (GUILayout.Button("Preview Slot", GUILayout.Width(92f)))
                {
                    serializedTarget.ApplyModifiedProperties();
                    PlaySlotPreview(valueOwner, previewOwner, slot);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (expanded)
            {
                for (int i = 0; i < slot.fields.Count; i++)
                    DrawPreviewField(serializedTarget, valueOwner, previewOwner, slot.fields[i]);
            }

            EditorGUILayout.EndVertical();
        }

        private void DrawPreviewField(
            SerializedObject serializedTarget,
            UnityEngine.Object valueOwner,
            UnityEngine.Object previewOwner,
            PreviewField field)
        {
            SerializedProperty property = serializedTarget.FindProperty(field.propertyPath);
            if (property == null)
                return;

            bool isNested = field.kind == PreviewFieldKind.WorldPresentation ||
                            field.kind == PreviewFieldKind.SpawnedPresentation ||
                            field.kind == PreviewFieldKind.Sound ||
                            field.kind == PreviewFieldKind.CameraShake;

            if (isNested)
            {
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(field.label, EditorStyles.boldLabel);

                using (new EditorGUI.DisabledScope(!CanPreviewField(valueOwner, field)))
                {
                    if (GUILayout.Button("Preview", GUILayout.Width(72f)))
                    {
                        serializedTarget.ApplyModifiedProperties();
                        PlayFieldPreview(valueOwner, previewOwner, field);
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(property, includeChildren: true);
                DrawNestedPrefabInspectActions(property);
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(property, new GUIContent(field.label), includeChildren: true);

            using (new EditorGUI.DisabledScope(!CanPreviewField(valueOwner, field)))
            {
                if (GUILayout.Button("Preview", GUILayout.Width(72f)))
                {
                    serializedTarget.ApplyModifiedProperties();
                    PlayFieldPreview(valueOwner, previewOwner, field);
                }
            }

            if (field.kind == PreviewFieldKind.Prefab)
                DrawPrefabActionButtons(property.objectReferenceValue as GameObject);

            EditorGUILayout.EndHorizontal();
        }

        private void DrawNestedPrefabInspectActions(SerializedProperty property)
        {
            if (property == null)
                return;

            if (property.type == nameof(WorldPresentationHook))
            {
                SerializedProperty effectPrefab = property.FindPropertyRelative("effect.prefab");
                SerializedProperty particlePrefab = property.FindPropertyRelative("particle.prefab");
                DrawPrefabInlineRow("Effect Prefab", effectPrefab?.objectReferenceValue as GameObject);
                DrawPrefabInlineRow("Particle Prefab", particlePrefab?.objectReferenceValue as GameObject);
                return;
            }

            if (property.type == nameof(SpawnedPresentationHook))
            {
                SerializedProperty prefab = property.FindPropertyRelative("prefab");
                DrawPrefabInlineRow("Prefab", prefab?.objectReferenceValue as GameObject);
            }
        }

        private void DrawPrefabInlineRow(string label, GameObject prefab)
        {
            if (prefab == null)
                return;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(16f);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField(label, prefab, typeof(GameObject), false);
            DrawPrefabActionButtons(prefab);
            EditorGUILayout.EndHorizontal();
        }

        private void DrawPrefabInspectActions(GameObject prefab, string title)
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Prefab", prefab, typeof(GameObject), false);
            EditorGUILayout.BeginHorizontal();
            DrawPrefabActionButtons(prefab);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndVertical();
        }

        private void DrawPrefabActionButtons(GameObject prefab)
        {
            using (new EditorGUI.DisabledScope(prefab == null))
            {
                if (GUILayout.Button("Inspect", GUILayout.Width(64f)))
                    SetInspectedPrefab(prefab);

                if (GUILayout.Button("Ping", GUILayout.Width(48f)))
                    EditorGUIUtility.PingObject(prefab);

                if (GUILayout.Button("Open", GUILayout.Width(48f)))
                    AssetDatabase.OpenAsset(prefab);
            }
        }

        private void DrawInlinePrefabInspector()
        {
            if (inspectedPrefab == null)
                return;

            EditorGUILayout.Space(8f);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Inline Prefab Inspector", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Close", GUILayout.Width(60f)))
            {
                inspectedPrefab = null;
                ClearInlineEditors();
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();
                return;
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("Prefab", inspectedPrefab, typeof(GameObject), false);
            if (GUILayout.Button("Open Prefab", GUILayout.Width(92f)))
                AssetDatabase.OpenAsset(inspectedPrefab);
            EditorGUILayout.EndHorizontal();

            prefabInspectorScroll = EditorGUILayout.BeginScrollView(prefabInspectorScroll, GUILayout.MinHeight(220f));
            Component[] components = inspectedPrefab.GetComponentsInChildren<Component>(includeInactive: true);
            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null || component is Transform)
                    continue;

                DrawInlineComponentInspector(component);
            }

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private void DrawInlineComponentInspector(Component component)
        {
            string key = $"component:{component.GetInstanceID()}";
            string label = component.gameObject == inspectedPrefab
                ? component.GetType().Name
                : $"{component.gameObject.name} / {component.GetType().Name}";

            bool expanded = slotFoldouts.TryGetValue(key, out bool value) && value;
            expanded = EditorGUILayout.Foldout(expanded, label, true);
            slotFoldouts[key] = expanded;

            if (!expanded)
                return;

            UnityEditor.Editor editor = GetInlineEditor(component);
            if (editor == null)
                return;

            EditorGUI.indentLevel++;
            EditorGUI.BeginChangeCheck();
            editor.OnInspectorGUI();
            if (EditorGUI.EndChangeCheck())
                EditorUtility.SetDirty(component);
            EditorGUI.indentLevel--;
        }

        private UnityEditor.Editor GetInlineEditor(UnityEngine.Object target)
        {
            if (target == null)
                return null;

            if (!inlineEditors.TryGetValue(target, out UnityEditor.Editor editor) || editor == null)
            {
                editor = UnityEditor.Editor.CreateEditor(target);
                inlineEditors[target] = editor;
            }

            return editor;
        }

        private void SetInspectedPrefab(GameObject prefab)
        {
            if (inspectedPrefab == prefab)
                return;

            inspectedPrefab = prefab;
            prefabInspectorScroll = Vector2.zero;
            ClearInlineEditors();
        }

        private void ClearInlineEditors()
        {
            foreach (UnityEditor.Editor editor in inlineEditors.Values)
            {
                if (editor != null)
                    DestroyImmediate(editor);
            }

            inlineEditors.Clear();
        }

        private List<PresentationSlot> BuildPresentationSlots(SerializedObject serializedTarget)
        {
            Dictionary<string, PresentationSlot> slots = new(StringComparer.OrdinalIgnoreCase);
            SerializedProperty iterator = serializedTarget.GetIterator();
            bool enterChildren = true;

            while (iterator.NextVisible(enterChildren))
            {
                enterChildren = true;

                if (iterator.name == "m_Script")
                    continue;

                if (TryGetPreviewFieldKind(iterator, out PreviewFieldKind kind))
                {
                    string slotKey = BuildSlotKey(iterator);
                    if (!slots.TryGetValue(slotKey, out PresentationSlot slot))
                    {
                        slot = new PresentationSlot
                        {
                            key = slotKey,
                            displayName = BuildSlotDisplayName(iterator)
                        };
                        slots.Add(slotKey, slot);
                    }

                    slot.fields.Add(new PreviewField
                    {
                        propertyPath = iterator.propertyPath,
                        label = iterator.displayName,
                        kind = kind
                    });

                    enterChildren = false;
                    continue;
                }

                if (iterator.isArray && iterator.propertyType != SerializedPropertyType.String)
                    enterChildren = false;
            }

            List<PresentationSlot> ordered = new(slots.Values);
            ordered.Sort((a, b) => string.Compare(a.displayName, b.displayName, StringComparison.OrdinalIgnoreCase));
            return ordered;
        }

        private static bool TryGetPreviewFieldKind(SerializedProperty property, out PreviewFieldKind kind)
        {
            if (property.type == nameof(WorldPresentationHook))
            {
                kind = PreviewFieldKind.WorldPresentation;
                return true;
            }

            if (property.type == nameof(SpawnedPresentationHook))
            {
                kind = PreviewFieldKind.SpawnedPresentation;
                return true;
            }

            if (property.type == nameof(SoundRef))
            {
                kind = PreviewFieldKind.Sound;
                return true;
            }

            if (property.type == nameof(CameraShakeHook))
            {
                kind = PreviewFieldKind.CameraShake;
                return true;
            }

            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                (property.objectReferenceValue is GameObject ||
                 property.type.Contains("GameObject") ||
                 property.name.EndsWith("Prefab", StringComparison.OrdinalIgnoreCase)))
            {
                kind = PreviewFieldKind.Prefab;
                return true;
            }

            kind = default;
            return false;
        }

        private static string BuildSlotKey(SerializedProperty property)
        {
            string parentPath = GetParentPath(property.propertyPath);
            return $"{parentPath}/{InferSlotKey(property.name)}";
        }

        private static string BuildSlotDisplayName(SerializedProperty property)
        {
            string parentPath = GetParentPath(property.propertyPath);
            string parentDisplay = BuildParentDisplay(parentPath);
            string slotDisplay = ObjectNames.NicifyVariableName(InferSlotKey(property.name));

            if (string.IsNullOrWhiteSpace(parentDisplay) ||
                parentDisplay.Equals("Pattern Data", StringComparison.OrdinalIgnoreCase))
            {
                return slotDisplay;
            }

            return $"{parentDisplay} / {slotDisplay}";
        }

        private static string GetParentPath(string propertyPath)
        {
            int index = propertyPath.LastIndexOf('.');
            return index < 0 ? string.Empty : propertyPath[..index];
        }

        private static string BuildParentDisplay(string parentPath)
        {
            if (string.IsNullOrWhiteSpace(parentPath))
                return string.Empty;

            string[] parts = parentPath.Split('.');
            string last = parts.Length > 0 ? parts[^1] : parentPath;
            return ObjectNames.NicifyVariableName(last);
        }

        private static string InferSlotKey(string propertyName)
        {
            string key = propertyName;
            string[] leadingMarkers =
            {
                "presentation",
                "audio",
                "sound",
                "cameraShake",
                "camera",
                "effect",
                "particle",
                "visual",
                "vfx"
            };

            for (int i = 0; i < leadingMarkers.Length; i++)
            {
                string marker = leadingMarkers[i];
                if (key.StartsWith(marker, StringComparison.Ordinal) && key.Length > marker.Length)
                {
                    string remainder = key[marker.Length..];
                    if (!string.IsNullOrWhiteSpace(remainder))
                        return LowercaseFirst(remainder);
                }
            }

            string[] suffixes =
            {
                "CameraShake",
                "ParticlePrefab",
                "EffectPrefab",
                "VisualPrefab",
                "VfxPrefab",
                "Prefab",
                "Sound",
                "Audio",
                "Presentation",
                "Hook"
            };

            for (int i = 0; i < suffixes.Length; i++)
            {
                string suffix = suffixes[i];
                if (key.EndsWith(suffix, StringComparison.Ordinal) && key.Length > suffix.Length)
                    return key[..^suffix.Length];
            }

            return key;
        }

        private static string LowercaseFirst(string value)
        {
            if (string.IsNullOrEmpty(value))
                return value;

            if (value.Length == 1)
                return value.ToLowerInvariant();

            return char.ToLowerInvariant(value[0]) + value[1..];
        }

        private bool CanPreviewField(UnityEngine.Object valueOwner, PreviewField field)
        {
            object value = GetValueByPropertyPath(valueOwner, field.propertyPath);
            return field.kind switch
            {
                PreviewFieldKind.WorldPresentation => value is WorldPresentationHook hook && hook.HasAnyContent,
                PreviewFieldKind.SpawnedPresentation => value is SpawnedPresentationHook spawned && spawned.HasContent,
                PreviewFieldKind.Sound => value is SoundRef sound && sound.IsSet,
                PreviewFieldKind.CameraShake => value is CameraShakeHook shake && shake.amplitude > 0f,
                PreviewFieldKind.Prefab => value is GameObject,
                _ => false
            };
        }

        private void PlayFieldPreview(
            UnityEngine.Object valueOwner,
            UnityEngine.Object previewOwner,
            PreviewField field)
        {
            object value = GetValueByPropertyPath(valueOwner, field.propertyPath);
            UnityEngine.Object owner = previewOwner != null ? previewOwner : valueOwner;
            string label = $"{valueOwner.name} / {field.label}";

            switch (field.kind)
            {
                case PreviewFieldKind.WorldPresentation when value is WorldPresentationHook hook:
                    CueCatalogPreviewUtility.PlayWorldPresentation(hook, owner, label);
                    break;
                case PreviewFieldKind.SpawnedPresentation when value is SpawnedPresentationHook spawned:
                    CueCatalogPreviewUtility.PlaySpawnedPresentation(spawned, owner, label);
                    break;
                case PreviewFieldKind.Sound when value is SoundRef sound:
                    CueCatalogPreviewUtility.PlaySound(sound, owner, label);
                    break;
                case PreviewFieldKind.CameraShake when value is CameraShakeHook shake:
                    CueCatalogPreviewUtility.PlayCameraShake(shake, owner, label);
                    break;
                case PreviewFieldKind.Prefab when value is GameObject prefab:
                    CueCatalogPreviewUtility.PlayPrefab(prefab, owner, label);
                    break;
            }
        }

        private void PlaySlotPreview(
            UnityEngine.Object valueOwner,
            UnityEngine.Object previewOwner,
            PresentationSlot slot)
        {
            UnityEngine.Object owner = previewOwner != null ? previewOwner : valueOwner;
            CueCatalogPreviewUtility.BeginCompositePreview(owner, $"{valueOwner.name} / {slot.displayName}");

            bool playedAny = false;
            for (int i = 0; i < slot.fields.Count; i++)
                playedAny |= AddFieldPreview(valueOwner, slot.fields[i]);

            CueCatalogPreviewUtility.EndCompositePreview(playedAny);
        }

        private bool AddFieldPreview(UnityEngine.Object valueOwner, PreviewField field)
        {
            object value = GetValueByPropertyPath(valueOwner, field.propertyPath);
            return field.kind switch
            {
                PreviewFieldKind.WorldPresentation when value is WorldPresentationHook hook =>
                    CueCatalogPreviewUtility.AddWorldPresentation(hook),
                PreviewFieldKind.SpawnedPresentation when value is SpawnedPresentationHook spawned =>
                    CueCatalogPreviewUtility.AddSpawnedPresentation(spawned),
                PreviewFieldKind.Sound when value is SoundRef sound =>
                    CueCatalogPreviewUtility.AddSound(sound),
                PreviewFieldKind.CameraShake when value is CameraShakeHook shake =>
                    CueCatalogPreviewUtility.AddCameraShake(shake),
                PreviewFieldKind.Prefab when value is GameObject prefab =>
                    CueCatalogPreviewUtility.AddPrefab(prefab),
                _ => false
            };
        }

        private static object GetValueByPropertyPath(UnityEngine.Object root, string propertyPath)
        {
            if (root == null || string.IsNullOrWhiteSpace(propertyPath))
                return null;

            object current = root;
            string normalizedPath = propertyPath.Replace(".Array.data[", "[");
            string[] parts = normalizedPath.Split('.');

            for (int i = 0; i < parts.Length; i++)
            {
                if (current == null)
                    return null;

                string part = parts[i];
                int arrayIndexStart = part.IndexOf('[');
                string memberName = arrayIndexStart >= 0 ? part[..arrayIndexStart] : part;

                current = GetMemberValue(current, memberName);
                if (current == null)
                    return null;

                if (arrayIndexStart >= 0)
                {
                    int arrayIndexEnd = part.IndexOf(']', arrayIndexStart);
                    if (arrayIndexEnd < 0)
                        return null;

                    string indexText = part.Substring(arrayIndexStart + 1, arrayIndexEnd - arrayIndexStart - 1);
                    if (!int.TryParse(indexText, out int index))
                        return null;

                    current = GetIndexedValue(current, index);
                }
            }

            return current;
        }

        private static object GetMemberValue(object source, string memberName)
        {
            Type type = source.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (field != null)
                    return field.GetValue(source);

                PropertyInfo property = type.GetProperty(
                    memberName,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (property != null && property.GetIndexParameters().Length == 0)
                    return property.GetValue(source);

                type = type.BaseType;
            }

            return null;
        }

        private static object GetIndexedValue(object source, int index)
        {
            if (source is IList list)
                return index >= 0 && index < list.Count ? list[index] : null;

            if (source is Array array)
                return index >= 0 && index < array.Length ? array.GetValue(index) : null;

            return null;
        }

        private void RegisterTarget(UnityEngine.Object target)
        {
            if (selectedProfile == null || target == null)
                return;

            if (!selectedProfile.AddTarget(target))
            {
                EditorUtility.DisplayDialog(
                    "Presentation Workbench",
                    "Register an AbilityDefinition or AbilityLogic asset. Duplicate entries are ignored.",
                    "OK");
                return;
            }

            BindSerializedProfile();
            pendingRegistrationTarget = null;
        }

        private void ScanProjectForAbilityTargets()
        {
            if (selectedProfile == null)
                return;

            List<AbilityDefinition> definitions = new();
            string[] definitionGuids = AssetDatabase.FindAssets("t:AbilityDefinition");
            for (int i = 0; i < definitionGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(definitionGuids[i]);
                AbilityDefinition definition = AssetDatabase.LoadAssetAtPath<AbilityDefinition>(path);
                if (definition != null)
                    definitions.Add(definition);
            }

            List<AbilityLogic> logics = new();
            string[] logicGuids = AssetDatabase.FindAssets("t:AbilityLogic");
            for (int i = 0; i < logicGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(logicGuids[i]);
                AbilityLogic logic = AssetDatabase.LoadAssetAtPath<AbilityLogic>(path);
                if (logic != null)
                    logics.Add(logic);
            }

            selectedProfile.AddDefinitions(definitions);
            selectedProfile.AddLogics(logics);
            BindSerializedProfile();
        }

        private UnityEngine.Object GetSelectedRegisteredTarget()
        {
            SerializedProperty listProperty = selectedRegisteredTargetKind == RegisteredTargetKind.AbilityDefinition
                ? profileDefinitionsProperty
                : profileLogicsProperty;

            if (listProperty == null ||
                selectedRegisteredTargetIndex < 0 ||
                selectedRegisteredTargetIndex >= listProperty.arraySize)
            {
                return null;
            }

            return listProperty.GetArrayElementAtIndex(selectedRegisteredTargetIndex).objectReferenceValue;
        }

        private void RemoveSelectedRegisteredTarget()
        {
            SerializedProperty listProperty = selectedRegisteredTargetKind == RegisteredTargetKind.AbilityDefinition
                ? profileDefinitionsProperty
                : profileLogicsProperty;

            if (listProperty == null ||
                selectedRegisteredTargetIndex < 0 ||
                selectedRegisteredTargetIndex >= listProperty.arraySize)
            {
                return;
            }

            listProperty.DeleteArrayElementAtIndex(selectedRegisteredTargetIndex);
            selectedRegisteredTargetIndex = Mathf.Clamp(
                selectedRegisteredTargetIndex - 1,
                -1,
                listProperty.arraySize - 1);
            inspectedPrefab = null;
            ClearInlineEditors();
        }

        private static string BuildRegisteredTargetLabel(UnityEngine.Object target)
        {
            if (target == null)
                return "<missing asset>";

            if (target is AbilityDefinition definition)
            {
                string logicName = definition.logic != null ? definition.logic.name : "No Logic";
                string abilityName = string.IsNullOrWhiteSpace(definition.abilityName)
                    ? definition.name
                    : definition.abilityName;
                return $"{abilityName}  [{logicName}]";
            }

            return target.name;
        }

        private void AddEntry()
        {
            entriesProperty.arraySize++;
            selectedIndex = entriesProperty.arraySize - 1;

            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(selectedIndex);
            entry.FindPropertyRelative("key").stringValue = string.Empty;
            entry.FindPropertyRelative("cue").objectReferenceValue = null;
        }

        private void DeleteSelectedEntry()
        {
            if (selectedIndex < 0 || selectedIndex >= entriesProperty.arraySize)
                return;

            entriesProperty.DeleteArrayElementAtIndex(selectedIndex);
            selectedIndex = Mathf.Clamp(selectedIndex - 1, -1, entriesProperty.arraySize - 1);
        }

        private void ValidateSelectedCatalog()
        {
            serializedCatalog.ApplyModifiedProperties();
            CueCatalogEditorUtility.InvalidateCache();

            List<string> issues = new List<string>();
            List<string> duplicates = selectedCatalog.GetDuplicateKeys();
            for (int i = 0; i < duplicates.Count; i++)
                issues.Add($"Duplicate key: {duplicates[i]}");

            IReadOnlyList<CueCatalogEntry> entries = selectedCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                CueCatalogEntry entry = entries[i];
                if (entry == null)
                {
                    issues.Add($"Entry {i} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.key))
                    issues.Add($"Entry {i} has an empty key.");

                if (entry.cue == null)
                    issues.Add($"{entry.key} has no cue asset.");
                else if (!entry.cue.HasAnyContent)
                    issues.Add($"{entry.key} cue has no presentation content.");
            }

            string message = issues.Count == 0
                ? "No catalog issues were found."
                : string.Join("\n", issues);

            EditorUtility.DisplayDialog("Cue Catalog Validation", message, "OK");
        }

        private List<int> GetFilteredIndices()
        {
            List<int> indices = new List<int>();
            string filter = string.IsNullOrWhiteSpace(searchQuery)
                ? string.Empty
                : searchQuery.Trim().ToLowerInvariant();

            for (int i = 0; i < entriesProperty.arraySize; i++)
            {
                SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(i);
                string key = entry.FindPropertyRelative("key").stringValue;
                UnityEngine.Object cueObject = entry.FindPropertyRelative("cue").objectReferenceValue;
                string cueName = cueObject != null ? cueObject.name : string.Empty;

                if (!string.IsNullOrEmpty(filter)
                    && !(key ?? string.Empty).ToLowerInvariant().Contains(filter)
                    && !cueName.ToLowerInvariant().Contains(filter))
                {
                    continue;
                }

                indices.Add(i);
            }

            return indices;
        }

        private void CreateCueAssetForEntry(SerializedProperty entry)
        {
            if (selectedCatalog == null || entry == null)
                return;

            string catalogPath = AssetDatabase.GetAssetPath(selectedCatalog);
            string directory = Path.GetDirectoryName(catalogPath);
            if (string.IsNullOrWhiteSpace(directory))
                directory = "Assets";

            string key = entry.FindPropertyRelative("key").stringValue;
            string assetName = BuildCueAssetName(key);
            string assetPath = Path.Combine(directory, $"{assetName}.asset").Replace("\\", "/");
            assetPath = AssetDatabase.GenerateUniqueAssetPath(assetPath);

            PresentationCueSO cue = CreateInstance<PresentationCueSO>();
            AssetDatabase.CreateAsset(cue, assetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            entry.FindPropertyRelative("cue").objectReferenceValue = cue;
            EditorUtility.SetDirty(selectedCatalog);
            CueCatalogEditorUtility.InvalidateCache();
            EditorGUIUtility.PingObject(cue);
        }

        private static string BuildCueAssetName(string key)
        {
            if (string.IsNullOrWhiteSpace(key))
                return "PresentationCue";

            StringBuilder builder = new StringBuilder("Cue_");
            string trimmed = key.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                char ch = trimmed[i];
                builder.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }

            return builder.ToString();
        }

        private void SetCatalog(CueCatalogSO catalog)
        {
            selectedCatalog = catalog;
            BindSerializedCatalog();
        }

        private void SetProfile(PresentationWorkbenchProfileSO profile)
        {
            selectedProfile = profile;
            BindSerializedProfile();
        }

        private void BindSerializedCatalog()
        {
            if (selectedCatalog == null)
            {
                serializedCatalog = null;
                entriesProperty = null;
                selectedIndex = -1;
                return;
            }

            serializedCatalog = new SerializedObject(selectedCatalog);
            entriesProperty = serializedCatalog.FindProperty("entries");
            selectedIndex = Mathf.Clamp(selectedIndex, -1, entriesProperty.arraySize - 1);
        }

        private void BindSerializedProfile()
        {
            if (selectedProfile == null)
            {
                serializedProfile = null;
                profileDefinitionsProperty = null;
                profileLogicsProperty = null;
                selectedRegisteredTargetIndex = -1;
                return;
            }

            serializedProfile = new SerializedObject(selectedProfile);
            profileDefinitionsProperty = serializedProfile.FindProperty("abilityDefinitions");
            profileLogicsProperty = serializedProfile.FindProperty("abilityLogics");

            SerializedProperty selectedList = selectedRegisteredTargetKind == RegisteredTargetKind.AbilityDefinition
                ? profileDefinitionsProperty
                : profileLogicsProperty;
            int size = selectedList != null ? selectedList.arraySize : 0;
            selectedRegisteredTargetIndex = Mathf.Clamp(selectedRegisteredTargetIndex, -1, size - 1);
        }

        private static CueCatalogSO CreateOrLoadDefaultCatalog()
        {
            CueCatalogSO existing = AssetDatabase.LoadAssetAtPath<CueCatalogSO>(DefaultCatalogAssetPath);
            if (existing != null)
                return existing;

            string directory = Path.GetDirectoryName(DefaultCatalogAssetPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            CueCatalogSO catalog = CreateInstance<CueCatalogSO>();
            AssetDatabase.CreateAsset(catalog, DefaultCatalogAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            CueCatalogEditorUtility.InvalidateCache();
            return catalog;
        }

        private static PresentationWorkbenchProfileSO CreateOrLoadDefaultWorkbenchProfile()
        {
            PresentationWorkbenchProfileSO existing =
                AssetDatabase.LoadAssetAtPath<PresentationWorkbenchProfileSO>(DefaultWorkbenchProfileAssetPath);
            if (existing != null)
                return existing;

            string directory = Path.GetDirectoryName(DefaultWorkbenchProfileAssetPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            PresentationWorkbenchProfileSO profile = CreateInstance<PresentationWorkbenchProfileSO>();
            AssetDatabase.CreateAsset(profile, DefaultWorkbenchProfileAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return profile;
        }
    }
}
#endif
