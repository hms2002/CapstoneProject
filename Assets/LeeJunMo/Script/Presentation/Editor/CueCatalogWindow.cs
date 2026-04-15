#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace CapstonePresentation.EditorTools
{
    public sealed class CueCatalogWindow : EditorWindow
    {
        private const string DefaultCatalogAssetPath =
            "Assets/LeeJunMo/Datas/Resources/Presentation/DefaultCueCatalog.asset";

        private CueCatalogSO selectedCatalog;
        private SerializedObject serializedCatalog;
        private SerializedProperty entriesProperty;

        private string searchQuery = string.Empty;
        private int selectedIndex = -1;
        private Vector2 listScroll;
        private Vector2 detailScroll;

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
        }

        private void OnDisable()
        {
            CueCatalogPreviewUtility.StopPreview();
        }

        private void OnGUI()
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
                "CueCatalogSO asset is required. Create a default catalog so reusable Cue assets can be organized in one place.",
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
                "Cue는 재사용 가능한 완성형 연출 프리셋입니다. 내부 Presentation은 이 창에서 직접 수정할 수 있고, 프리뷰는 툴 내부 렌더 패널에서 재생됩니다.",
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
                Object cueObject = entry.FindPropertyRelative("cue").objectReferenceValue;
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

            EditorGUILayout.HelpBox(
                "툴 내부 프리뷰는 현재 선택된 Cue를 오프스크린으로 렌더합니다. 사운드는 같이 재생되고, 카메라 셰이크는 이 프리뷰 카메라에 적용됩니다.",
                MessageType.None);
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

            EditorGUILayout.HelpBox(
                "프리뷰에는 사운드, 카메라 셰이크, 이펙트, 파티클이 모두 포함됩니다. 사운드는 같이 재생되고, 비주얼과 셰이크는 툴 내부 프리뷰 패널에서 확인할 수 있습니다.",
                MessageType.None);
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
                Object cueObject = entry.FindPropertyRelative("cue").objectReferenceValue;
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
    }
}
#endif
