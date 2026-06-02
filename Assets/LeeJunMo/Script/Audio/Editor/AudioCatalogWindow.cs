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

        [SerializeField] private AudioCatalogSO selectedCatalog;
        private SerializedObject serializedCatalog;
        private SerializedProperty globalVolumeMultiplierProperty;
        private SerializedProperty bgmFadeInSecondsProperty;
        private SerializedProperty bgmFadeOutSecondsProperty;
        private SerializedProperty entriesProperty;

        [SerializeField] private string searchQuery = string.Empty;
        [SerializeField] private int selectedIndex = -1;
        private Vector2 listScroll;
        private Vector2 detailScroll;
        private readonly List<AudioClip> entryPreviewClips = new();

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

        private void OnDisable()
        {
            AudioCatalogPreviewUtility.StopPreview();
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

            DrawRuntimeDefaults();
            DrawToolbar();

            EditorGUILayout.BeginHorizontal();
            DrawEntryListPanel();
            DrawEntryDetailPanel();
            EditorGUILayout.EndHorizontal();

            if (serializedCatalog.ApplyModifiedProperties())
            {
                ApplyCatalogChanges();
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

        private void DrawRuntimeDefaults()
        {
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField("Runtime Defaults", EditorStyles.boldLabel);

            if (globalVolumeMultiplierProperty != null)
            {
                EditorGUILayout.PropertyField(
                    globalVolumeMultiplierProperty,
                    new GUIContent("Global Sound Multiplier"));
            }

            if (bgmFadeInSecondsProperty != null)
            {
                EditorGUILayout.PropertyField(
                    bgmFadeInSecondsProperty,
                    new GUIContent("BGM Fade In Seconds"));
            }

            if (bgmFadeOutSecondsProperty != null)
            {
                EditorGUILayout.PropertyField(
                    bgmFadeOutSecondsProperty,
                    new GUIContent("BGM Fade Out Seconds"));
            }

            EditorGUILayout.HelpBox(
                "These defaults are authored tuning values independent from Settings volume. Runtime volume resolves as entry volume x SoundRef multiplier x global sound multiplier x Settings volume.",
                MessageType.None);

            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(
                    "Play Mode live tuning is active. Changes are applied to the active SoundManager and saved to the selected catalog asset.",
                    MessageType.Info);
            }

            EditorGUILayout.EndVertical();
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

            using (new EditorGUI.DisabledScope(!AudioCatalogPreviewUtility.CanPreview))
            {
                if (GUILayout.Button("Stop Preview", EditorStyles.toolbarButton, GUILayout.Width(92f)))
                    StopSelectedPreview();
            }

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

        private void DrawEntryFields(SerializedProperty entry)
        {
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("key"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("bus"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("category"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("volume"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("playbackSpeed"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("pitchMin"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("pitchMax"));
            SerializedProperty variantPlaybackModeProperty = entry.FindPropertyRelative("variantPlaybackMode");
            if (variantPlaybackModeProperty != null)
            {
                EditorGUILayout.PropertyField(
                    variantPlaybackModeProperty,
                    new GUIContent("Variant Playback"));
            }
            DrawVariantsField(entry);
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("loop"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("spatial"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("important"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("cooldown"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("minDistance"));
            EditorGUILayout.PropertyField(entry.FindPropertyRelative("maxDistance"));

            EditorGUILayout.HelpBox(
                "Preview uses the global sound multiplier, entry volume, playback speed, and pitch range. BGM entries also use the runtime default fade-in/fade-out seconds and loop like runtime BGM.",
                MessageType.None);
        }

        private float GetPreviewGlobalVolumeMultiplier()
        {
            return globalVolumeMultiplierProperty != null
                ? Mathf.Max(0f, globalVolumeMultiplierProperty.floatValue)
                : 1f;
        }

        private float GetBgmFadeInSeconds()
        {
            return bgmFadeInSecondsProperty != null
                ? Mathf.Max(0f, bgmFadeInSecondsProperty.floatValue)
                : 0f;
        }

        private float GetBgmFadeOutSeconds()
        {
            return bgmFadeOutSecondsProperty != null
                ? Mathf.Max(0f, bgmFadeOutSecondsProperty.floatValue)
                : 0f;
        }

        private void StopSelectedPreview()
        {
            float fadeOutSeconds = IsSelectedEntryBgm() ? GetBgmFadeOutSeconds() : 0f;
            AudioCatalogPreviewUtility.StopPreview(fadeOutSeconds);
        }

        private void DrawVariantsField(SerializedProperty entry)
        {
            SerializedProperty variantsProperty = entry.FindPropertyRelative("variants");
            if (variantsProperty == null)
                return;

            variantsProperty.isExpanded = EditorGUILayout.Foldout(
                variantsProperty.isExpanded,
                "Variants",
                true);

            if (!variantsProperty.isExpanded)
                return;

            using (new EditorGUI.IndentLevelScope())
            {
                variantsProperty.arraySize = Mathf.Max(0, EditorGUILayout.IntField("Size", variantsProperty.arraySize));

                float previewVolume = entry.FindPropertyRelative("volume").floatValue * GetPreviewGlobalVolumeMultiplier();
                float previewSpeed = Mathf.Max(0.1f, entry.FindPropertyRelative("playbackSpeed").floatValue);
                float previewPitchMin = entry.FindPropertyRelative("pitchMin").floatValue;
                float previewPitchMax = entry.FindPropertyRelative("pitchMax").floatValue;
                bool isBgmPreview = IsBgmEntry(entry);
                bool isLoopEntry = entry.FindPropertyRelative("loop").boolValue;
                bool previewLoop = isLoopEntry || isBgmPreview;
                float previewFadeInSeconds = isBgmPreview ? GetBgmFadeInSeconds() : 0f;
                float previewFadeOutSeconds = isBgmPreview ? GetBgmFadeOutSeconds() : 0f;
                AudioVariantPlaybackMode playbackMode = GetVariantPlaybackMode(entry);
                int removeIndex = -1;

                EditorGUILayout.BeginHorizontal();
                using (new EditorGUI.DisabledScope(
                           !AudioCatalogPreviewUtility.CanPreview ||
                           !HasPlayablePreviewClip(variantsProperty)))
                {
                    if (GUILayout.Button("Preview Entry", GUILayout.Width(112f)))
                    {
                        PreviewEntry(
                            variantsProperty,
                            playbackMode,
                            previewVolume,
                            previewSpeed,
                            previewPitchMin,
                            previewPitchMax,
                            previewLoop,
                            previewFadeInSeconds);
                    }
                }

                if (playbackMode == AudioVariantPlaybackMode.Simultaneous && previewLoop)
                {
                    EditorGUILayout.HelpBox(
                        "Simultaneous playback is runtime-supported only for non-loop SFX. This entry previews as a single source.",
                        MessageType.Warning);
                }

                EditorGUILayout.EndHorizontal();

                for (int i = 0; i < variantsProperty.arraySize; i++)
                {
                    SerializedProperty variantProperty = variantsProperty.GetArrayElementAtIndex(i);
                    AudioClip clip = variantProperty.objectReferenceValue as AudioClip;

                    EditorGUILayout.BeginHorizontal();
                    EditorGUILayout.PropertyField(variantProperty, new GUIContent($"Element {i}"));

                    using (new EditorGUI.DisabledScope(!AudioCatalogPreviewUtility.CanPreview || clip == null))
                    {
                        if (GUILayout.Button("Play", GUILayout.Width(48f)))
                        {
                            AudioCatalogPreviewUtility.PlayVariant(
                                clip,
                                previewVolume,
                                previewSpeed,
                                previewPitchMin,
                                previewPitchMax,
                                previewLoop,
                                previewFadeInSeconds);
                        }

                        if (GUILayout.Button("Stop", GUILayout.Width(48f)))
                            AudioCatalogPreviewUtility.StopPreview(previewFadeOutSeconds);
                    }

                    if (GUILayout.Button("-", GUILayout.Width(24f)))
                        removeIndex = i;

                    EditorGUILayout.EndHorizontal();
                }

                if (GUILayout.Button("Add Variant", GUILayout.Width(96f)))
                    variantsProperty.arraySize++;

                if (removeIndex >= 0 && removeIndex < variantsProperty.arraySize)
                {
                    SerializedProperty variantProperty = variantsProperty.GetArrayElementAtIndex(removeIndex);
                    if (variantProperty.objectReferenceValue != null)
                        variantsProperty.DeleteArrayElementAtIndex(removeIndex);

                    variantsProperty.DeleteArrayElementAtIndex(removeIndex);
                }
            }
        }

        private bool IsSelectedEntryBgm()
        {
            if (entriesProperty == null ||
                selectedIndex < 0 ||
                selectedIndex >= entriesProperty.arraySize)
            {
                return false;
            }

            return IsBgmEntry(entriesProperty.GetArrayElementAtIndex(selectedIndex));
        }

        private static bool IsBgmEntry(SerializedProperty entry)
        {
            SerializedProperty busProperty = entry?.FindPropertyRelative("bus");
            return busProperty != null && busProperty.enumValueIndex == (int)AudioBus.BGM;
        }

        private static AudioVariantPlaybackMode GetVariantPlaybackMode(SerializedProperty entry)
        {
            SerializedProperty modeProperty = entry?.FindPropertyRelative("variantPlaybackMode");
            if (modeProperty == null)
                return AudioVariantPlaybackMode.Random;

            int value = modeProperty.enumValueIndex;
            return value >= 0 && value <= (int)AudioVariantPlaybackMode.Simultaneous
                ? (AudioVariantPlaybackMode)value
                : AudioVariantPlaybackMode.Random;
        }

        private void PreviewEntry(
            SerializedProperty variantsProperty,
            AudioVariantPlaybackMode playbackMode,
            float previewVolume,
            float previewSpeed,
            float previewPitchMin,
            float previewPitchMax,
            bool previewLoop,
            float previewFadeInSeconds)
        {
            if (playbackMode == AudioVariantPlaybackMode.Simultaneous && !previewLoop)
            {
                if (TryCollectPreviewClips(variantsProperty, entryPreviewClips))
                {
                    AudioCatalogPreviewUtility.PlayVariants(
                        entryPreviewClips,
                        previewVolume,
                        previewSpeed,
                        previewPitchMin,
                        previewPitchMax);
                }

                return;
            }

            if (!TryPickPreviewClip(variantsProperty, playbackMode, out AudioClip clip) || clip == null)
                return;

            AudioCatalogPreviewUtility.PlayVariant(
                clip,
                previewVolume,
                previewSpeed,
                previewPitchMin,
                previewPitchMax,
                previewLoop,
                previewFadeInSeconds);
        }

        private static bool HasPlayablePreviewClip(SerializedProperty variantsProperty)
        {
            if (variantsProperty == null || variantsProperty.arraySize == 0)
                return false;

            for (int i = 0; i < variantsProperty.arraySize; i++)
            {
                if (variantsProperty.GetArrayElementAtIndex(i).objectReferenceValue is AudioClip)
                    return true;
            }

            return false;
        }

        private static bool TryCollectPreviewClips(
            SerializedProperty variantsProperty,
            List<AudioClip> clips)
        {
            if (clips == null)
                return false;

            clips.Clear();
            if (variantsProperty == null || variantsProperty.arraySize == 0)
                return false;

            for (int i = 0; i < variantsProperty.arraySize; i++)
            {
                if (variantsProperty.GetArrayElementAtIndex(i).objectReferenceValue is AudioClip clip)
                    clips.Add(clip);
            }

            return clips.Count > 0;
        }

        private static bool TryPickPreviewClip(
            SerializedProperty variantsProperty,
            AudioVariantPlaybackMode playbackMode,
            out AudioClip clip)
        {
            clip = null;
            if (variantsProperty == null || variantsProperty.arraySize == 0)
                return false;

            if (playbackMode == AudioVariantPlaybackMode.First)
                return TryPickFirstPreviewClip(variantsProperty, out clip);

            int startIndex = Random.Range(0, variantsProperty.arraySize);
            for (int i = 0; i < variantsProperty.arraySize; i++)
            {
                int index = (startIndex + i) % variantsProperty.arraySize;
                clip = variantsProperty.GetArrayElementAtIndex(index).objectReferenceValue as AudioClip;
                if (clip != null)
                    return true;
            }

            clip = null;
            return false;
        }

        private static bool TryPickFirstPreviewClip(
            SerializedProperty variantsProperty,
            out AudioClip clip)
        {
            clip = null;
            if (variantsProperty == null || variantsProperty.arraySize == 0)
                return false;

            for (int i = 0; i < variantsProperty.arraySize; i++)
            {
                clip = variantsProperty.GetArrayElementAtIndex(i).objectReferenceValue as AudioClip;
                if (clip != null)
                    return true;
            }

            clip = null;
            return false;
        }

        private void AddEntry()
        {
            entriesProperty.arraySize++;
            selectedIndex = entriesProperty.arraySize - 1;

            SerializedProperty entry = entriesProperty.GetArrayElementAtIndex(selectedIndex);
            entry.FindPropertyRelative("key").stringValue = string.Empty;
            entry.FindPropertyRelative("volume").floatValue = 1f;
            entry.FindPropertyRelative("playbackSpeed").floatValue = 1f;
            entry.FindPropertyRelative("pitchMin").floatValue = 1f;
            entry.FindPropertyRelative("pitchMax").floatValue = 1f;
            SerializedProperty variantPlaybackModeProperty = entry.FindPropertyRelative("variantPlaybackMode");
            if (variantPlaybackModeProperty != null)
                variantPlaybackModeProperty.enumValueIndex = (int)AudioVariantPlaybackMode.Random;
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
            ApplyCatalogChanges();

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

                if (entry.HasUnsupportedSimultaneousPlayback)
                {
                    issues.Add(
                        $"{entry.key} uses Simultaneous variant playback, but Simultaneous is only supported for non-loop SFX.");
                }
            }

            string message = issues.Count == 0
                ? "No catalog issues were found."
                : string.Join("\n", issues);

            EditorUtility.DisplayDialog("Audio Catalog Validation", message, "OK");
        }

        private void ApplyCatalogChanges()
        {
            if (selectedCatalog == null)
                return;

            selectedCatalog.MarkLookupDirty();
            EditorUtility.SetDirty(selectedCatalog);
            AudioCatalogEditorUtility.InvalidateCache();

            if (EditorApplication.isPlaying && SoundManager.Instance != null)
                SoundManager.Instance.RefreshCatalogRuntime(selectedCatalog);

            AssetDatabase.SaveAssetIfDirty(selectedCatalog);
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
                globalVolumeMultiplierProperty = null;
                bgmFadeInSecondsProperty = null;
                bgmFadeOutSecondsProperty = null;
                entriesProperty = null;
                selectedIndex = -1;
                return;
            }

            serializedCatalog = new SerializedObject(selectedCatalog);
            globalVolumeMultiplierProperty = serializedCatalog.FindProperty("globalVolumeMultiplier");
            bgmFadeInSecondsProperty = serializedCatalog.FindProperty("bgmFadeInSeconds");
            bgmFadeOutSecondsProperty = serializedCatalog.FindProperty("bgmFadeOutSeconds");
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
