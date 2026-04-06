#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using CapstoneAudio;
using UnityEditor;
using UnityEngine;

namespace CapstoneAudio.EditorTools
{
    public sealed class AudioCatalogWindow : EditorWindow
    {
        private const string DefaultCatalogAssetPath = "Assets/LeeJunMo/Datas/Resources/Audio/DefaultAudioCatalog.asset";

        private AudioCatalogSO selectedCatalog;
        private SerializedObject serializedCatalog;
        private SerializedProperty entriesProperty;

        private string searchQuery = string.Empty;
        private int selectedIndex = -1;
        private Vector2 listScroll;
        private Vector2 detailScroll;

        [MenuItem("Tools/Audio/Audio Catalog")]
        public static void OpenWindow()
        {
            GetWindow<AudioCatalogWindow>("Audio Catalog").Show();
        }

        public static void OpenWindow(AudioCatalogSO catalog)
        {
            AudioCatalogWindow window = GetWindow<AudioCatalogWindow>("Audio Catalog");
            window.Show();
            window.SetCatalog(catalog);
        }

        private void OnEnable()
        {
            if (selectedCatalog == null)
            {
                IReadOnlyList<AudioCatalogSO> catalogs = AudioCatalogEditorUtility.FindCatalogs();
                if (catalogs.Count > 0)
                    SetCatalog(catalogs[0]);
            }
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
                AudioCatalogEditorUtility.InvalidateCache();
            }
        }

        private void DrawCatalogSelector()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);

            AudioCatalogSO newCatalog = (AudioCatalogSO)EditorGUILayout.ObjectField(
                "Catalog",
                selectedCatalog,
                typeof(AudioCatalogSO),
                false);

            if (newCatalog != selectedCatalog)
                SetCatalog(newCatalog);

            if (GUILayout.Button("Default", GUILayout.Width(72f)))
                SetCatalog(CreateOrLoadDefaultCatalog());

            if (GUILayout.Button("Refresh", GUILayout.Width(72f)))
            {
                AudioCatalogEditorUtility.InvalidateCache();
                if (selectedCatalog != null)
                    SetCatalog(selectedCatalog);
            }

            EditorGUILayout.EndHorizontal();
        }

        private void DrawEmptyState()
        {
            EditorGUILayout.HelpBox(
                "AudioCatalogSO asset is required. Create the default catalog in Resources so SoundManager can load it automatically.",
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

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.HelpBox(
                "Key rule: domain.subject.event[.phase][.qualifier]  Example: ability.fireball.cast.start",
                MessageType.None);
        }

        private void DrawEntryListPanel()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(Mathf.Max(280f, position.width * 0.38f)));
            EditorGUILayout.LabelField("Entries", EditorStyles.boldLabel);

            listScroll = EditorGUILayout.BeginScrollView(listScroll);
            List<int> filteredIndices = GetFilteredIndices();

            if (filteredIndices.Count == 0)
            {
                EditorGUILayout.HelpBox("No entries matched the current filter.", MessageType.None);
            }

            for (int i = 0; i < filteredIndices.Count; i++)
            {
                int entryIndex = filteredIndices[i];
                SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(entryIndex);
                string key = entry.FindPropertyRelative("key").stringValue;
                SerializedProperty categoryProperty = entry.FindPropertyRelative("category");
                string categoryName = categoryProperty.enumDisplayNames[categoryProperty.enumValueIndex];

                GUIStyle style = entryIndex == selectedIndex ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                if (GUILayout.Button($"{key}  [{categoryName}]", style))
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
            detailScroll = EditorGUILayout.BeginScrollView(detailScroll);

            DrawEntryFields(entry);

            EditorGUILayout.Space(10f);
            GUI.backgroundColor = new Color(0.9f, 0.4f, 0.4f);
            if (GUILayout.Button("Delete Entry"))
                DeleteSelectedEntry();
            GUI.backgroundColor = Color.white;

            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        private static void DrawEntryFields(SerializedProperty entry)
        {
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("key"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("bus"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("category"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("variants"), true);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("volume"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("pitchMin"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("pitchMax"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("loop"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("spatial"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("important"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("cooldown"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("minDistance"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("maxDistance"));
        }

        private void AddEntry()
        {
            entriesProperty.arraySize++;
            selectedIndex = entriesProperty.arraySize - 1;

            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(selectedIndex);
            entry.FindPropertyRelative("key").stringValue = string.Empty;
            entry.FindPropertyRelative("volume").floatValue = 1f;
            entry.FindPropertyRelative("pitchMin").floatValue = 1f;
            entry.FindPropertyRelative("pitchMax").floatValue = 1f;
            entry.FindPropertyRelative("minDistance").floatValue = 1f;
            entry.FindPropertyRelative("maxDistance").floatValue = 20f;
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
            AudioCatalogEditorUtility.InvalidateCache();

            List<string> issues = new List<string>();
            List<string> duplicates = selectedCatalog.GetDuplicateKeys();
            for (int i = 0; i < duplicates.Count; i++)
            {
                issues.Add($"Duplicate key: {duplicates[i]}");
            }

            IReadOnlyList<AudioCatalogEntry> entries = selectedCatalog.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                AudioCatalogEntry entry = entries[i];
                if (entry == null)
                {
                    issues.Add($"Entry {i} is null.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(entry.key))
                    issues.Add($"Entry {i} has an empty key.");

                if (!entry.HasPlayableClip)
                    issues.Add($"{entry.key} has no playable clips.");
            }

            string message = issues.Count == 0
                ? "No catalog issues were found."
                : string.Join("\n", issues);

            EditorUtility.DisplayDialog("Audio Catalog Validation", message, "OK");
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
                SerializedProperty categoryProperty = entry.FindPropertyRelative("category");
                string category = categoryProperty.enumDisplayNames[categoryProperty.enumValueIndex];

                if (!string.IsNullOrEmpty(filter)
                    && !(key ?? string.Empty).ToLowerInvariant().Contains(filter)
                    && !category.ToLowerInvariant().Contains(filter))
                {
                    continue;
                }

                indices.Add(i);
            }

            return indices;
        }

        private void SetCatalog(AudioCatalogSO catalog)
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

        private static AudioCatalogSO CreateOrLoadDefaultCatalog()
        {
            AudioCatalogSO existing = AssetDatabase.LoadAssetAtPath<AudioCatalogSO>(DefaultCatalogAssetPath);
            if (existing != null)
                return existing;

            string directory = Path.GetDirectoryName(DefaultCatalogAssetPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            AudioCatalogSO catalog = CreateInstance<AudioCatalogSO>();
            AssetDatabase.CreateAsset(catalog, DefaultCatalogAssetPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            AudioCatalogEditorUtility.InvalidateCache();
            return catalog;
        }
    }
}
#endif
