#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Ink.Runtime;
using Ink.UnityIntegration;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.U2D.Animation;
using Object = UnityEngine.Object;

public sealed class NpcCustomizationHubWindow : EditorWindow
{
    private const string WindowTitle = "NPC Customization Hub";
    private const string DraftInkFolder = "Assets/_Project/Data/Dialogue/Ink/NpcDrafts";
    private const string InkRootFolder = "Assets/_Project/Data/Dialogue/Ink";
    private const string PortraitSpriteCategory = "Face";
    private const float SidebarWidth = 300f;
    private const float IssuePanelHeight = 170f;

    private static readonly string[] TabNames =
    {
        "Profile",
        "Dialogue",
        "Presentation",
        "Affection",
        "Usage/Validation",
        "RunSpecial",
    };

    private static readonly HashSet<string> KnownDialogueTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "speaker",
        "anim",
        "dialogue_anim",
        "camerashake",
        "enter",
        "face",
        "emote",
        "pos",
        "move",
        "action",
        "exit",
        "feature",
        "add_aff",
        "choice_fail",
        "aff_fail",
        "fail_aff",
    };

    private static readonly HashSet<string> SupportedAnimTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "normal",
        "slow",
        "angry",
        "whisper",
        "cold",
    };

    private static readonly HashSet<string> SupportedCameraShakeTags = new(StringComparer.OrdinalIgnoreCase)
    {
        "low",
        "middle",
        "high",
    };

    private static readonly HashSet<string> SupportedInlineEffects = new(StringComparer.OrdinalIgnoreCase)
    {
        "shake",
        "tremble",
        "jitter",
        "punch",
        "pop",
        "emphasis",
        "wave",
        "wobble",
        "float",
        "drift",
        "rand_size",
        "random_size",
        "randomsize",
        "size_jitter",
        "sizejitter",
        "drunk_size",
        "drunksize",
        "slowshake",
        "slow_shake",
        "drunkshake",
        "drunk_shake",
    };

    private static readonly HashSet<string> RandomSizeInlineEffects = new(StringComparer.OrdinalIgnoreCase)
    {
        "rand_size",
        "random_size",
        "randomsize",
        "size_jitter",
        "sizejitter",
        "drunk_size",
        "drunksize",
    };

    private static readonly HashSet<string> KnownNpcFeatureNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Upgrade",
    };

    private enum HubTab
    {
        Profile,
        Dialogue,
        Presentation,
        Affection,
        UsageValidation,
        RunSpecial,
    }

    private enum IssueSeverity
    {
        Info,
        Warning,
        Error,
    }

    private sealed class NpcRecord
    {
        public NPCData Npc;
        public string Path;
        public bool InSelectedDatabase;
    }

    private sealed class InkCandidate
    {
        public TextAsset Asset;
        public string Path;
        public int Score;
        public string Reason;
    }

    private sealed class ValidationIssue
    {
        public ValidationIssue(IssueSeverity severity, string scope, string message, Object context)
        {
            Severity = severity;
            Scope = scope;
            Message = message;
            Context = context;
        }

        public IssueSeverity Severity { get; }
        public string Scope { get; }
        public string Message { get; }
        public Object Context { get; }
    }

    private sealed class UsageRecord
    {
        public string Kind;
        public string Path;
        public string Detail;
        public Object Context;
    }

    private sealed class RunSpecialRecord
    {
        public RunSpecialNpcDialogueSetSO Asset;
        public string Path;
    }

    private readonly List<NpcRecord> npcRecords = new();
    private readonly List<NPCDatabase> npcDatabases = new();
    private readonly List<RunSpecialRecord> runSpecialRecords = new();
    private readonly List<ValidationIssue> issues = new();
    private readonly List<UsageRecord> usageRecords = new();

    private NPCDatabase selectedDatabase;
    private NPCData selectedNpc;
    private RunSpecialNpcDialogueSetSO selectedRunSpecial;
    private HubTab currentTab;
    private string npcSearch = string.Empty;
    private string lastTemplateStatus = string.Empty;
    private Vector2 npcListScroll;
    private Vector2 tabScroll;
    private Vector2 issueScroll;
    private Vector2 candidateScroll;
    private Vector2 usageScroll;
    private Vector2 runSpecialListScroll;

    [MenuItem("Tools/NPC/NPC Customization Hub")]
    public static void Open()
    {
        NpcCustomizationHubWindow window = GetWindow<NpcCustomizationHubWindow>(WindowTitle);
        window.minSize = new Vector2(980f, 640f);
        window.Show();
    }

    private void OnEnable()
    {
        RefreshAll();
    }

    private void OnGUI()
    {
        DrawToolbar();

        EditorGUILayout.BeginVertical();
        EditorGUILayout.BeginHorizontal();
        DrawNpcSidebar();
        DrawMainPanel();
        EditorGUILayout.EndHorizontal();
        DrawIssuePanel();
        EditorGUILayout.EndVertical();
    }

    private void DrawToolbar()
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

        if (GUILayout.Button("Refresh", EditorStyles.toolbarButton, GUILayout.Width(70f)))
            RefreshAll();

        EditorGUILayout.LabelField("NPC Database", GUILayout.Width(90f));
        EditorGUI.BeginChangeCheck();
        selectedDatabase = EditorGUILayout.ObjectField(
            selectedDatabase,
            typeof(NPCDatabase),
            false,
            GUILayout.Width(260f)) as NPCDatabase;
        if (EditorGUI.EndChangeCheck())
            RefreshAll(false);

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Save Assets", EditorStyles.toolbarButton, GUILayout.Width(90f)))
        {
            AssetDatabase.SaveAssets();
            RefreshAll(false);
        }

        EditorGUILayout.EndHorizontal();
    }

    private void DrawNpcSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(SidebarWidth));

        EditorGUILayout.LabelField("NPCData", EditorStyles.boldLabel);
        npcSearch = EditorGUILayout.TextField("Search", npcSearch);

        npcListScroll = EditorGUILayout.BeginScrollView(npcListScroll);
        IReadOnlyList<NpcRecord> filteredRecords = GetFilteredNpcRecords();
        for (int i = 0; i < filteredRecords.Count; i++)
        {
            NpcRecord record = filteredRecords[i];
            if (record.Npc == null)
                continue;

            string label = BuildNpcLabel(record);
            GUIStyle style = record.Npc == selectedNpc ? EditorStyles.helpBox : EditorStyles.miniButton;
            if (GUILayout.Button(label, style))
            {
                selectedNpc = record.Npc;
                RebuildUsageRecords();
                RefreshIssues();
                GUI.FocusControl(null);
            }
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space();
        if (selectedNpc != null)
        {
            EditorGUILayout.ObjectField("Selected", selectedNpc, typeof(NPCData), false);
            EditorGUILayout.SelectableLabel(
                AssetDatabase.GetAssetPath(selectedNpc),
                EditorStyles.wordWrappedMiniLabel,
                GUILayout.Height(34f));
        }
        else
        {
            EditorGUILayout.HelpBox("Select an NPCData asset.", MessageType.Info);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawMainPanel()
    {
        EditorGUILayout.BeginVertical();

        currentTab = (HubTab)GUILayout.Toolbar((int)currentTab, TabNames);
        EditorGUILayout.Space(4f);

        tabScroll = EditorGUILayout.BeginScrollView(tabScroll);
        switch (currentTab)
        {
            case HubTab.Profile:
                DrawProfileTab();
                break;
            case HubTab.Dialogue:
                DrawDialogueTab();
                break;
            case HubTab.Presentation:
                DrawPresentationTab();
                break;
            case HubTab.Affection:
                DrawAffectionTab();
                break;
            case HubTab.UsageValidation:
                DrawUsageValidationTab();
                break;
            case HubTab.RunSpecial:
                DrawRunSpecialTab();
                break;
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void DrawProfileTab()
    {
        if (!RequireSelectedNpc())
            return;

        SerializedObject serialized = new(selectedNpc);
        serialized.Update();

        EditorGUILayout.LabelField("Profile", EditorStyles.boldLabel);
        DrawSerializedProperty(serialized, "id");
        DrawSerializedProperty(serialized, "npcName");
        DrawSerializedProperty(serialized, "isBoss");
        ApplySerializedChanges(serialized, selectedNpc);

        EditorGUILayout.Space(12f);
        DrawDatabaseMembership();
    }

    private void DrawDialogueTab()
    {
        if (!RequireSelectedNpc())
            return;

        SerializedObject serialized = new(selectedNpc);
        serialized.Update();

        EditorGUILayout.LabelField("Dialogue References", EditorStyles.boldLabel);
        DrawTextAssetProperty(serialized, "primaryInk", "Primary Ink JSON");
        DrawTextAssetProperty(serialized, "bossEncounterInk", "Boss Encounter Ink JSON");
        ApplySerializedChanges(serialized, selectedNpc);

        EditorGUILayout.Space(8f);
        DrawInkTemplateControls();

        EditorGUILayout.Space(12f);
        DrawInkRecommendations();

        EditorGUILayout.Space(12f);
        DrawBossEncounterReadOnly();
    }

    private void DrawPresentationTab()
    {
        if (!RequireSelectedNpc())
            return;

        SerializedObject serialized = new(selectedNpc);
        serialized.Update();

        EditorGUILayout.LabelField("Presentation", EditorStyles.boldLabel);
        DrawSerializedProperty(serialized, "dialogueTheme");
        DrawSerializedProperty(serialized, "spriteLibraryAsset");
        DrawSerializedProperty(serialized, "emoteOffset");
        ApplySerializedChanges(serialized, selectedNpc);

        EditorGUILayout.Space(8f);
        DrawSpriteLibrarySummary(selectedNpc.spriteLibraryAsset);

        if (selectedNpc.DialogueTheme != null)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.ObjectField("Theme", selectedNpc.DialogueTheme, typeof(DialogueThemeSO), false);
        }
    }

    private void DrawAffectionTab()
    {
        if (!RequireSelectedNpc())
            return;

        SerializedObject serialized = new(selectedNpc);
        serialized.Update();

        EditorGUILayout.LabelField("Affection Rewards", EditorStyles.boldLabel);
        DrawSerializedProperty(serialized, "affectionRewards", true);
        ApplySerializedChanges(serialized, selectedNpc);
    }

    private void DrawUsageValidationTab()
    {
        if (!RequireSelectedNpc())
            return;

        EditorGUILayout.LabelField("Project Usage", EditorStyles.boldLabel);
        if (GUILayout.Button("Rescan Usage", GUILayout.Width(120f)))
            RebuildUsageRecords();

        usageScroll = EditorGUILayout.BeginScrollView(usageScroll, GUILayout.MinHeight(160f));
        if (usageRecords.Count == 0)
        {
            EditorGUILayout.HelpBox("No project-wide serialized usage was found for the selected NPCData.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < usageRecords.Count; i++)
                DrawUsageRecord(usageRecords[i]);
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.Space(12f);
        DrawSelectedNpcIssues();
    }

    private void DrawRunSpecialTab()
    {
        EditorGUILayout.LabelField("RunSpecial NPC Dialogue Sets", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "RunSpecial NPCs use SpeechBubble and RunSpecialNpcDialogueSetSO, not NPCData/Ink. V1 shows and validates them read-only.",
            MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        DrawRunSpecialList();
        DrawRunSpecialInspector();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawIssuePanel()
    {
        EditorGUILayout.BeginVertical(EditorStyles.helpBox, GUILayout.Height(IssuePanelHeight));
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Issues", EditorStyles.boldLabel);
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("Refresh Issues", GUILayout.Width(110f)))
            RefreshIssues();

        if (GUILayout.Button("Copy Report", GUILayout.Width(100f)))
            EditorGUIUtility.systemCopyBuffer = BuildIssueReport();

        EditorGUILayout.EndHorizontal();

        issueScroll = EditorGUILayout.BeginScrollView(issueScroll);
        if (issues.Count == 0)
        {
            EditorGUILayout.HelpBox("No validation issues.", MessageType.Info);
        }
        else
        {
            for (int i = 0; i < issues.Count; i++)
                DrawIssue(issues[i]);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawDatabaseMembership()
    {
        EditorGUILayout.LabelField("NPC Database Membership", EditorStyles.boldLabel);
        if (selectedDatabase == null)
        {
            EditorGUILayout.HelpBox("Assign an NPCDatabase in the toolbar to edit membership.", MessageType.Info);
            return;
        }

        bool inDatabase = selectedDatabase.npcList != null && selectedDatabase.npcList.Contains(selectedNpc);
        EditorGUILayout.ObjectField("Database", selectedDatabase, typeof(NPCDatabase), false);
        EditorGUILayout.LabelField("Current State", inDatabase ? "Registered" : "Not registered");

        EditorGUILayout.BeginHorizontal();
        EditorGUI.BeginDisabledGroup(inDatabase);
        if (GUILayout.Button("Add Existing NPCData"))
        {
            if (EditorUtility.DisplayDialog(
                    "Add NPCData",
                    $"Add {selectedNpc.name} to {selectedDatabase.name}?",
                    "Add",
                    "Cancel"))
            {
                Undo.RecordObject(selectedDatabase, "Add NPCData to database");
                selectedDatabase.npcList ??= new List<NPCData>();
                selectedDatabase.npcList.Add(selectedNpc);
                EditorUtility.SetDirty(selectedDatabase);
                AssetDatabase.SaveAssets();
                RefreshAll(false);
            }
        }
        EditorGUI.EndDisabledGroup();

        EditorGUI.BeginDisabledGroup(!inDatabase);
        if (GUILayout.Button("Remove From Database"))
        {
            if (EditorUtility.DisplayDialog(
                    "Remove NPCData",
                    $"Remove {selectedNpc.name} from {selectedDatabase.name}?",
                    "Remove",
                    "Cancel"))
            {
                Undo.RecordObject(selectedDatabase, "Remove NPCData from database");
                selectedDatabase.npcList.Remove(selectedNpc);
                EditorUtility.SetDirty(selectedDatabase);
                AssetDatabase.SaveAssets();
                RefreshAll(false);
            }
        }
        EditorGUI.EndDisabledGroup();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawInkTemplateControls()
    {
        EditorGUILayout.LabelField("Ink Template", EditorStyles.boldLabel);

        if (!string.IsNullOrWhiteSpace(lastTemplateStatus))
            EditorGUILayout.HelpBox(lastTemplateStatus, MessageType.Info);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Create Template For Primary Ink"))
            CreateInkTemplateAndAssign("primaryInk", "Create primary NPC dialogue template");

        if (GUILayout.Button("Create Template For Boss Encounter Ink"))
            CreateInkTemplateAndAssign("bossEncounterInk", "Create boss encounter NPC dialogue template");
        EditorGUILayout.EndHorizontal();
    }

    private void DrawInkRecommendations()
    {
        EditorGUILayout.LabelField("Likely Ink JSON Candidates", EditorStyles.boldLabel);
        List<InkCandidate> candidates = BuildInkCandidates(selectedNpc, 10);
        if (candidates.Count == 0)
        {
            EditorGUILayout.HelpBox("No .json TextAssets were found under Assets/_Project/Data/Dialogue/Ink.", MessageType.Warning);
            return;
        }

        candidateScroll = EditorGUILayout.BeginScrollView(candidateScroll, GUILayout.MinHeight(130f), GUILayout.MaxHeight(240f));
        for (int i = 0; i < candidates.Count; i++)
        {
            InkCandidate candidate = candidates[i];
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.ObjectField(candidate.Asset, typeof(TextAsset), false);
            if (GUILayout.Button("Primary", GUILayout.Width(80f)))
                AssignNpcObjectReference("primaryInk", candidate.Asset, "Assign primary Ink JSON");
            if (GUILayout.Button("Encounter", GUILayout.Width(90f)))
                AssignNpcObjectReference("bossEncounterInk", candidate.Asset, "Assign boss encounter Ink JSON");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField($"{candidate.Path} | score {candidate.Score} | {candidate.Reason}", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.EndVertical();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawBossEncounterReadOnly()
    {
        EditorGUILayout.LabelField("Boss Encounter Dialogues (Read Only)", EditorStyles.boldLabel);

        IReadOnlyList<BossEncounterDialogueEntry> entries = selectedNpc.BossEncounterDialogues;
        if (entries == null || entries.Count == 0)
        {
            EditorGUILayout.HelpBox("No boss encounter dialogue entries are authored on this NPCData.", MessageType.Info);
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            BossEncounterDialogueEntry entry = entries[i];
            if (entry == null)
                continue;

            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField($"Entry {i}", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Label", string.IsNullOrWhiteSpace(entry.Label) ? "(empty)" : entry.Label);
            EditorGUILayout.LabelField("Priority", entry.Priority.ToString(CultureInfo.InvariantCulture));
            EditorGUILayout.LabelField("Start Path", string.IsNullOrWhiteSpace(entry.StartPath) ? "(root)" : entry.StartPath);
            EditorGUILayout.ObjectField("Ink Override", entry.InkOverride, typeof(TextAsset), false);
            EditorGUILayout.EndVertical();
        }

        SerializedObject serialized = new(selectedNpc);
        SerializedProperty list = serialized.FindProperty("bossEncounterDialogues");
        if (list != null)
        {
            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(list, includeChildren: true);
            EditorGUI.EndDisabledGroup();
        }
    }

    private void DrawSpriteLibrarySummary(SpriteLibraryAsset library)
    {
        if (library == null)
        {
            EditorGUILayout.HelpBox("No SpriteLibraryAsset assigned.", MessageType.Warning);
            return;
        }

        string[] categories = library.GetCategoryNames().OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();
        if (categories.Length == 0)
        {
            EditorGUILayout.HelpBox("SpriteLibraryAsset has no categories.", MessageType.Warning);
            return;
        }

        EditorGUILayout.LabelField("Sprite Library Categories", EditorStyles.boldLabel);
        for (int i = 0; i < categories.Length; i++)
        {
            string category = categories[i];
            string[] labels = library.GetCategoryLabelNames(category)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray();

            EditorGUILayout.LabelField(category, string.Join(", ", labels));
        }
    }

    private void DrawUsageRecord(UsageRecord record)
    {
        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(record.Kind, EditorStyles.boldLabel);
        EditorGUILayout.LabelField(record.Path, EditorStyles.wordWrappedMiniLabel);
        if (!string.IsNullOrWhiteSpace(record.Detail))
            EditorGUILayout.LabelField(record.Detail, EditorStyles.wordWrappedMiniLabel);
        EditorGUILayout.EndVertical();
        if (record.Context != null && GUILayout.Button("Ping", GUILayout.Width(50f)))
            EditorGUIUtility.PingObject(record.Context);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSelectedNpcIssues()
    {
        EditorGUILayout.LabelField("Selected NPC Validation", EditorStyles.boldLabel);
        List<ValidationIssue> selectedIssues = issues
            .Where(issue => issue.Context == selectedNpc || string.Equals(issue.Scope, GetNpcScope(selectedNpc), StringComparison.Ordinal))
            .ToList();

        if (selectedIssues.Count == 0)
        {
            EditorGUILayout.HelpBox("No selected NPC validation issues.", MessageType.Info);
            return;
        }

        for (int i = 0; i < selectedIssues.Count; i++)
            DrawIssue(selectedIssues[i]);
    }

    private void DrawRunSpecialList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(260f));
        runSpecialListScroll = EditorGUILayout.BeginScrollView(runSpecialListScroll);
        for (int i = 0; i < runSpecialRecords.Count; i++)
        {
            RunSpecialRecord record = runSpecialRecords[i];
            if (record.Asset == null)
                continue;

            GUIStyle style = record.Asset == selectedRunSpecial ? EditorStyles.helpBox : EditorStyles.miniButton;
            if (GUILayout.Button(record.Asset.name, style))
                selectedRunSpecial = record.Asset;
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawRunSpecialInspector()
    {
        EditorGUILayout.BeginVertical();
        if (selectedRunSpecial == null)
        {
            EditorGUILayout.HelpBox("Select a RunSpecialNpcDialogueSetSO.", MessageType.Info);
            EditorGUILayout.EndVertical();
            return;
        }

        EditorGUILayout.ObjectField("Selected", selectedRunSpecial, typeof(RunSpecialNpcDialogueSetSO), false);
        EditorGUILayout.SelectableLabel(
            AssetDatabase.GetAssetPath(selectedRunSpecial),
            EditorStyles.wordWrappedMiniLabel,
            GUILayout.Height(34f));

        SerializedObject serialized = new(selectedRunSpecial);
        serialized.Update();
        EditorGUI.BeginDisabledGroup(true);
        DrawSerializedProperty(serialized, "featureKind");
        RunSpecialNpcFeatureKind kind = selectedRunSpecial.FeatureKind;
        if (kind == RunSpecialNpcFeatureKind.Construction)
        {
            DrawSerializedProperty(serialized, "constructionNotStarted", true);
            DrawSerializedProperty(serialized, "constructionInsufficientFunds", true);
            DrawSerializedProperty(serialized, "constructionPending", true);
            DrawSerializedProperty(serialized, "constructionCompleted", true);
        }
        else
        {
            DrawSerializedProperty(serialized, "teleportAvailable", true);
            DrawSerializedProperty(serialized, "teleportLocked", true);
            DrawSerializedProperty(serialized, "teleportUnavailable", true);
        }
        EditorGUI.EndDisabledGroup();

        EditorGUILayout.Space(8f);
        List<ValidationIssue> runSpecialIssues = issues
            .Where(issue => issue.Context == selectedRunSpecial)
            .ToList();
        if (runSpecialIssues.Count == 0)
            EditorGUILayout.HelpBox("No RunSpecial validation issues.", MessageType.Info);
        else
            for (int i = 0; i < runSpecialIssues.Count; i++)
                DrawIssue(runSpecialIssues[i]);

        EditorGUILayout.EndVertical();
    }

    private void DrawIssue(ValidationIssue issue)
    {
        Color previousColor = GUI.color;
        GUI.color = issue.Severity switch
        {
            IssueSeverity.Error => new Color(1f, 0.55f, 0.55f),
            IssueSeverity.Warning => new Color(1f, 0.85f, 0.45f),
            _ => new Color(0.75f, 0.9f, 1f),
        };

        EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
        GUI.color = previousColor;
        EditorGUILayout.LabelField(issue.Severity.ToString(), GUILayout.Width(64f));
        EditorGUILayout.LabelField(issue.Scope, GUILayout.Width(180f));
        EditorGUILayout.LabelField(issue.Message, EditorStyles.wordWrappedLabel);
        if (issue.Context != null && GUILayout.Button("Ping", GUILayout.Width(48f)))
            EditorGUIUtility.PingObject(issue.Context);
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSerializedProperty(SerializedObject serialized, string propertyName, bool includeChildren = false)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(property, includeChildren);
    }

    private void DrawTextAssetProperty(SerializedObject serialized, string propertyName, string label)
    {
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            EditorGUILayout.HelpBox($"Missing serialized property: {propertyName}", MessageType.Error);
            return;
        }

        EditorGUILayout.PropertyField(property, new GUIContent(label));
        if (property.objectReferenceValue is TextAsset textAsset)
        {
            string path = AssetDatabase.GetAssetPath(textAsset);
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                EditorGUILayout.HelpBox($"{label} should reference a compiled Ink .json TextAsset.", MessageType.Warning);
        }
    }

    private bool ApplySerializedChanges(SerializedObject serialized, Object target)
    {
        bool changed = serialized.ApplyModifiedProperties();
        if (!changed)
            return false;

        EditorUtility.SetDirty(target);
        AssetDatabase.SaveAssets();
        RefreshAll(false);
        return true;
    }

    private bool RequireSelectedNpc()
    {
        if (selectedNpc != null)
            return true;

        EditorGUILayout.HelpBox("Select an NPCData asset from the left panel.", MessageType.Info);
        return false;
    }

    private IReadOnlyList<NpcRecord> GetFilteredNpcRecords()
    {
        if (string.IsNullOrWhiteSpace(npcSearch))
            return npcRecords;

        string query = npcSearch.Trim();
        return npcRecords
            .Where(record =>
                record.Npc != null &&
                (record.Npc.name.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 record.Npc.npcName != null &&
                 record.Npc.npcName.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0 ||
                 record.Npc.id.ToString(CultureInfo.InvariantCulture).Contains(query)))
            .ToList();
    }

    private string BuildNpcLabel(NpcRecord record)
    {
        string name = string.IsNullOrWhiteSpace(record.Npc.npcName) ? record.Npc.name : record.Npc.npcName;
        string databaseMarker = record.InSelectedDatabase ? "DB" : "--";
        string bossMarker = record.Npc.isBoss ? "Boss" : "NPC";
        return $"[{databaseMarker}] {record.Npc.id} {name} ({bossMarker})";
    }

    private void AssignNpcObjectReference(string propertyName, Object value, string undoName)
    {
        if (selectedNpc == null)
            return;

        Undo.RecordObject(selectedNpc, undoName);
        SerializedObject serialized = new(selectedNpc);
        serialized.Update();
        SerializedProperty property = serialized.FindProperty(propertyName);
        if (property == null)
        {
            Debug.LogError($"[NpcCustomizationHub] Missing NPCData property: {propertyName}");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedProperties();
        EditorUtility.SetDirty(selectedNpc);
        AssetDatabase.SaveAssets();
        RefreshAll(false);
    }

    private void CreateInkTemplateAndAssign(string propertyName, string undoName)
    {
        if (selectedNpc == null)
            return;

        try
        {
            EnsureDraftFolder();

            string assetName = SanitizeFileName(selectedNpc.name);
            string path = AssetDatabase.GenerateUniqueAssetPath(
                $"{DraftInkFolder}/NPC_{selectedNpc.id}_{assetName}_Dialogue.ink");

            string template = BuildInkTemplate(selectedNpc);
            DefaultAsset inkAsset = InkEditorUtils.CreateNewInkFileAtPath(path, template);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            TextAsset jsonAsset = TryCompileInkAndLoadJson(inkAsset);
            if (jsonAsset != null)
            {
                AssignNpcObjectReference(propertyName, jsonAsset, undoName);
                Selection.activeObject = jsonAsset;
                lastTemplateStatus = $"Created and assigned {AssetDatabase.GetAssetPath(jsonAsset)}.";
                Debug.Log($"[NpcCustomizationHub] Created and assigned Ink JSON: {AssetDatabase.GetAssetPath(jsonAsset)}");
            }
            else
            {
                Selection.activeObject = inkAsset;
                lastTemplateStatus = $"Created {path}. JSON is pending. Let Unity import/compile Ink, then assign the generated JSON.";
                Debug.LogWarning($"[NpcCustomizationHub] Created {path}, but compiled JSON was not available yet.");
            }

            RefreshAll(false);
        }
        catch (Exception ex)
        {
            lastTemplateStatus = $"Ink template creation failed: {ex.Message}";
            Debug.LogException(ex);
        }
    }

    private static TextAsset TryCompileInkAndLoadJson(DefaultAsset inkAsset)
    {
        if (inkAsset == null)
            return null;

        InkFile inkFile = InkLibrary.GetInkFileWithFile(inkAsset, true);
        if (inkFile == null)
            return null;

        InkCompiler.CompileInk(new[] { inkFile }, true, null);
        AssetDatabase.ImportAsset(inkFile.jsonPath, ImportAssetOptions.ForceUpdate);
        inkFile.FindCompiledJSONAsset();
        return inkFile.jsonAsset != null
            ? inkFile.jsonAsset
            : AssetDatabase.LoadAssetAtPath<TextAsset>(inkFile.jsonPath);
    }

    private static string BuildInkTemplate(NPCData npc)
    {
        string displayName = string.IsNullOrWhiteSpace(npc.npcName) ? npc.name : npc.npcName;
        return
            $"// Draft dialogue for {displayName}\n" +
            $"# speaker: {npc.id}\n" +
            "# anim: normal\n" +
            "Draft dialogue line.\n" +
            "-> END\n";
    }

    private static void EnsureDraftFolder()
    {
        if (AssetDatabase.IsValidFolder(DraftInkFolder))
            return;

        if (!AssetDatabase.IsValidFolder(InkRootFolder))
            throw new DirectoryNotFoundException($"Ink root folder is missing: {InkRootFolder}");

        AssetDatabase.CreateFolder(InkRootFolder, "NpcDrafts");
    }

    private List<InkCandidate> BuildInkCandidates(NPCData npc, int maxCount)
    {
        List<InkCandidate> candidates = new();
        if (npc == null)
            return candidates;

        string npcName = NormalizeSearchToken(npc.npcName);
        string assetName = NormalizeSearchToken(npc.name);
        string assetNameWithoutNpc = assetName
            .Replace("npcdata", string.Empty)
            .Replace("npc", string.Empty)
            .Replace("boss", string.Empty);
        string idText = npc.id.ToString(CultureInfo.InvariantCulture);

        string[] guids = AssetDatabase.FindAssets("t:TextAsset", new[] { InkRootFolder });
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                continue;

            TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
            if (asset == null)
                continue;

            string normalizedPath = NormalizeSearchToken(path);
            int score = 0;
            List<string> reasons = new();

            if (!string.IsNullOrEmpty(assetName) && normalizedPath.Contains(assetName))
            {
                score += 80;
                reasons.Add("asset name");
            }

            if (!string.IsNullOrEmpty(assetNameWithoutNpc) && normalizedPath.Contains(assetNameWithoutNpc))
            {
                score += 55;
                reasons.Add("trimmed asset name");
            }

            if (!string.IsNullOrEmpty(npcName) && normalizedPath.Contains(npcName))
            {
                score += 45;
                reasons.Add("npc name");
            }

            if (normalizedPath.Contains(idText))
            {
                score += 30;
                reasons.Add("id");
            }

            if (normalizedPath.Contains("animatedvariants"))
            {
                score += 10;
                reasons.Add("animated variant");
            }

            if (score == 0)
            {
                score = 1;
                reasons.Add("fallback");
            }

            candidates.Add(new InkCandidate
            {
                Asset = asset,
                Path = path,
                Score = score,
                Reason = string.Join(", ", reasons),
            });
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Path, StringComparer.OrdinalIgnoreCase)
            .Take(maxCount)
            .ToList();
    }

    private void RefreshAll(bool keepSelection = true)
    {
        if (!keepSelection)
        {
            selectedNpc = selectedNpc != null ? selectedNpc : null;
            selectedRunSpecial = selectedRunSpecial != null ? selectedRunSpecial : null;
        }

        RefreshDatabases();
        RefreshNpcRecords();
        RefreshRunSpecialRecords();
        EnsureValidSelections();
        RebuildUsageRecords();
        RefreshIssues();
        Repaint();
    }

    private void RefreshDatabases()
    {
        npcDatabases.Clear();
        string[] guids = AssetDatabase.FindAssets("t:NPCDatabase");
        for (int i = 0; i < guids.Length; i++)
        {
            NPCDatabase database = AssetDatabase.LoadAssetAtPath<NPCDatabase>(AssetDatabase.GUIDToAssetPath(guids[i]));
            if (database != null)
                npcDatabases.Add(database);
        }

        npcDatabases.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        if (selectedDatabase == null && npcDatabases.Count > 0)
            selectedDatabase = npcDatabases[0];
    }

    private void RefreshNpcRecords()
    {
        npcRecords.Clear();
        HashSet<NPCData> databaseSet = selectedDatabase != null && selectedDatabase.npcList != null
            ? new HashSet<NPCData>(selectedDatabase.npcList.Where(npc => npc != null))
            : new HashSet<NPCData>();

        string[] guids = AssetDatabase.FindAssets("t:NPCData");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            NPCData npc = AssetDatabase.LoadAssetAtPath<NPCData>(path);
            if (npc == null)
                continue;

            npcRecords.Add(new NpcRecord
            {
                Npc = npc,
                Path = path,
                InSelectedDatabase = databaseSet.Contains(npc),
            });
        }

        npcRecords.Sort((a, b) =>
        {
            int idCompare = a.Npc.id.CompareTo(b.Npc.id);
            if (idCompare != 0)
                return idCompare;

            return string.Compare(a.Npc.name, b.Npc.name, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void RefreshRunSpecialRecords()
    {
        runSpecialRecords.Clear();
        string[] guids = AssetDatabase.FindAssets("t:RunSpecialNpcDialogueSetSO");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            RunSpecialNpcDialogueSetSO asset = AssetDatabase.LoadAssetAtPath<RunSpecialNpcDialogueSetSO>(path);
            if (asset == null)
                continue;

            runSpecialRecords.Add(new RunSpecialRecord
            {
                Asset = asset,
                Path = path,
            });
        }

        runSpecialRecords.Sort((a, b) => string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase));
    }

    private void EnsureValidSelections()
    {
        if (selectedNpc == null || !npcRecords.Any(record => record.Npc == selectedNpc))
            selectedNpc = npcRecords.Count > 0 ? npcRecords[0].Npc : null;

        if (selectedRunSpecial == null || !runSpecialRecords.Any(record => record.Asset == selectedRunSpecial))
            selectedRunSpecial = runSpecialRecords.Count > 0 ? runSpecialRecords[0].Asset : null;
    }

    private void RefreshIssues()
    {
        issues.Clear();
        ValidateNpcDatabase();
        ValidateNpcRecords();
        ValidateRunSpecialRecords();

        issues.Sort((a, b) =>
        {
            int severityCompare = b.Severity.CompareTo(a.Severity);
            if (severityCompare != 0)
                return severityCompare;

            int scopeCompare = string.Compare(a.Scope, b.Scope, StringComparison.OrdinalIgnoreCase);
            if (scopeCompare != 0)
                return scopeCompare;

            return string.Compare(a.Message, b.Message, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void ValidateNpcDatabase()
    {
        if (npcDatabases.Count == 0)
        {
            AddIssue(IssueSeverity.Warning, "NPC Database", "No NPCDatabase assets were found.", null);
            return;
        }

        if (selectedDatabase == null)
            AddIssue(IssueSeverity.Warning, "NPC Database", "No NPCDatabase is selected in the toolbar.", null);
    }

    private void ValidateNpcRecords()
    {
        Dictionary<int, List<NPCData>> byId = npcRecords
            .Where(record => record.Npc != null)
            .GroupBy(record => record.Npc.id)
            .ToDictionary(group => group.Key, group => group.Select(record => record.Npc).ToList());

        foreach (KeyValuePair<int, List<NPCData>> pair in byId)
        {
            if (pair.Key > 0 && pair.Value.Count <= 1)
                continue;

            string names = string.Join(", ", pair.Value.Select(npc => npc.name));
            IssueSeverity severity = pair.Key <= 0 ? IssueSeverity.Warning : IssueSeverity.Error;
            AddIssue(severity, "NPCData", $"NPC id {pair.Key} is used by: {names}", pair.Value.FirstOrDefault());
        }

        for (int i = 0; i < npcRecords.Count; i++)
        {
            NPCData npc = npcRecords[i].Npc;
            if (npc == null)
                continue;

            ValidateNpc(npc);
        }
    }

    private void ValidateNpc(NPCData npc)
    {
        string scope = GetNpcScope(npc);
        if (string.IsNullOrWhiteSpace(npc.npcName))
            AddIssue(IssueSeverity.Warning, scope, "npcName is empty.", npc);

        if (npc.PrimaryInk == null)
        {
            AddIssue(IssueSeverity.Error, scope, "Primary Ink JSON is missing.", npc);
        }
        else
        {
            ValidateInkReference(npc, npc.PrimaryInk, "primaryInk");
        }

        IReadOnlyList<BossEncounterDialogueEntry> entries = npc.BossEncounterDialogues;
        if (npc.isBoss && entries != null && entries.Count > 0)
        {
            bool hasMissingSharedInk = npc.BossEncounterInk == null &&
                                       entries.Any(entry => entry != null && entry.InkOverride == null);
            if (hasMissingSharedInk)
                AddIssue(IssueSeverity.Error, scope, "Boss encounter entries exist but shared Boss Encounter Ink is missing for entries without overrides.", npc);
        }

        if (npc.BossEncounterInk != null)
            ValidateInkReference(npc, npc.BossEncounterInk, "bossEncounterInk");

        ValidateBossEncounterStartPaths(npc);
        ValidateAffectionRewards(npc);
    }

    private void ValidateInkReference(NPCData ownerNpc, TextAsset inkJson, string fieldName)
    {
        string scope = $"{GetNpcScope(ownerNpc)}.{fieldName}";
        string path = AssetDatabase.GetAssetPath(inkJson);
        if (string.IsNullOrWhiteSpace(path))
        {
            AddIssue(IssueSeverity.Error, scope, "Ink reference has no AssetDatabase path.", ownerNpc);
            return;
        }

        if (!path.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            AddIssue(IssueSeverity.Warning, scope, "Ink reference should be a compiled .json TextAsset.", inkJson);

        if (!IsValidInkJson(inkJson, out string jsonError))
        {
            AddIssue(IssueSeverity.Error, scope, $"Invalid Ink JSON: {jsonError}", inkJson);
            return;
        }

        string source = TryLoadInkSource(inkJson, out string sourcePath);
        if (string.IsNullOrEmpty(source))
        {
            AddIssue(IssueSeverity.Info, scope, "Matching .ink source was not found; source tag validation is limited.", inkJson);
            return;
        }

        ValidateInkSource(ownerNpc, inkJson, source, sourcePath, scope);
    }

    private void ValidateInkSource(
        NPCData ownerNpc,
        TextAsset inkJson,
        string source,
        string sourcePath,
        string scope)
    {
        string[] lines = SplitLines(source);
        for (int i = 0; i < lines.Length; i++)
        {
            string trimmed = lines[i].Trim();
            if (trimmed.StartsWith("#", StringComparison.Ordinal))
                ValidateInkLineTag(ownerNpc, trimmed, i + 1, sourcePath, scope, inkJson);

            ValidateInlinePauseTags(trimmed, i + 1, sourcePath, scope, inkJson);
            ValidateInlineEffectTags(trimmed, i + 1, sourcePath, scope, inkJson);
        }
    }

    private void ValidateInkLineTag(
        NPCData ownerNpc,
        string trimmedLine,
        int lineNumber,
        string sourcePath,
        string scope,
        TextAsset context)
    {
        string rawTag = trimmedLine.Substring(1).Trim();
        if (string.IsNullOrWhiteSpace(rawTag))
            return;

        string[] parts = rawTag.Split(':');
        string command = parts[0].Trim();
        string value = parts.Length >= 2 ? parts[1].Trim() : string.Empty;

        if (!KnownDialogueTags.Contains(command))
        {
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} unknown dialogue tag '# {command}'.", context);
            return;
        }

        if (command.Equals("anim", StringComparison.OrdinalIgnoreCase) ||
            command.Equals("dialogue_anim", StringComparison.OrdinalIgnoreCase))
        {
            if (!SupportedAnimTags.Contains(value))
                AddIssue(IssueSeverity.Error, scope, $"{sourcePath}:{lineNumber} unsupported anim tag '{value}'.", context);
            return;
        }

        if (command.Equals("camerashake", StringComparison.OrdinalIgnoreCase))
        {
            if (!SupportedCameraShakeTags.Contains(value))
                AddIssue(IssueSeverity.Error, scope, $"{sourcePath}:{lineNumber} unsupported CameraShake tag '{value}'. Expected Low, Middle, or High.", context);
            return;
        }

        if (command.Equals("speaker", StringComparison.OrdinalIgnoreCase))
        {
            ValidateSpeakerTag(value, sourcePath, lineNumber, scope, context);
            return;
        }

        if (command.Equals("face", StringComparison.OrdinalIgnoreCase))
        {
            ValidateFaceTag(parts, sourcePath, lineNumber, scope, context);
            return;
        }

        if (command.Equals("feature", StringComparison.OrdinalIgnoreCase))
        {
            if (!KnownNpcFeatureNames.Contains(value))
                AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} feature tag '{value}' has no known INPCFeature implementation in the current scan.", context);
            return;
        }

        if (command.Equals("add_aff", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
                AddIssue(IssueSeverity.Error, scope, $"{sourcePath}:{lineNumber} add_aff value '{value}' is not an integer.", context);

            if (ownerNpc == null)
                AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} add_aff tag has no owner NPC context.", context);
        }
    }

    private void ValidateSpeakerTag(string value, string sourcePath, int lineNumber, string scope, Object context)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} speaker tag is empty.", context);
            return;
        }

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int npcId))
            return;

        if (!TryFindNpcById(npcId, out _))
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} speaker id {npcId} has no matching NPCData asset.", context);
    }

    private void ValidateFaceTag(string[] parts, string sourcePath, int lineNumber, string scope, Object context)
    {
        if (parts.Length < 3)
        {
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} face tag should use '# face: npcId: label'.", context);
            return;
        }

        string npcIdText = parts[1].Trim();
        string label = string.Join(":", parts.Skip(2)).Trim();
        if (!int.TryParse(npcIdText, NumberStyles.Integer, CultureInfo.InvariantCulture, out int npcId))
            return;

        if (!TryFindNpcById(npcId, out NPCData targetNpc))
        {
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} face target id {npcId} has no matching NPCData asset.", context);
            return;
        }

        if (targetNpc.spriteLibraryAsset == null)
        {
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} face target {targetNpc.name} has no SpriteLibraryAsset.", targetNpc);
            return;
        }

        if (!SpriteLibraryHasLabel(targetNpc.spriteLibraryAsset, PortraitSpriteCategory, label))
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} SpriteLibraryAsset does not contain category '{PortraitSpriteCategory}' label '{label}'.", targetNpc.spriteLibraryAsset);
    }

    private void ValidateInlinePauseTags(string line, int lineNumber, string sourcePath, string scope, Object context)
    {
        int searchIndex = 0;
        while (searchIndex < line.Length)
        {
            int start = line.IndexOf("[pause=", searchIndex, StringComparison.OrdinalIgnoreCase);
            if (start < 0)
                return;

            int end = line.IndexOf(']', start + 7);
            if (end < 0)
            {
                AddIssue(IssueSeverity.Error, scope, $"{sourcePath}:{lineNumber} pause tag is missing ']'.", context);
                return;
            }

            string value = line.Substring(start + 7, end - start - 7);
            if (!float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float seconds) || seconds < 0f)
                AddIssue(IssueSeverity.Error, scope, $"{sourcePath}:{lineNumber} pause value '{value}' is invalid.", context);

            searchIndex = end + 1;
        }
    }

    private void ValidateInlineEffectTags(string line, int lineNumber, string sourcePath, string scope, Object context)
    {
        Stack<string> openTags = new();
        int searchIndex = 0;

        while (searchIndex < line.Length)
        {
            int start = line.IndexOf('[', searchIndex);
            if (start < 0)
                break;

            int end = line.IndexOf(']', start + 1);
            if (end < 0)
                break;

            string tag = line.Substring(start + 1, end - start - 1).Trim();
            if (tag.StartsWith("pause=", StringComparison.OrdinalIgnoreCase))
            {
                searchIndex = end + 1;
                continue;
            }

            bool isClosing = tag.StartsWith("/", StringComparison.Ordinal);
            string effectExpression = isClosing ? tag.Substring(1).Trim() : tag;
            string effectName = ExtractInlineEffectName(effectExpression);
            if (!SupportedInlineEffects.Contains(effectName))
            {
                searchIndex = end + 1;
                continue;
            }

            if (!isClosing)
            {
                openTags.Push(effectName);
                ValidateInlineEffectArguments(effectName, effectExpression, lineNumber, sourcePath, scope, context);
            }
            else if (openTags.Count == 0 ||
                     !string.Equals(openTags.Pop(), effectName, StringComparison.OrdinalIgnoreCase))
            {
                AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} effect tag '[/{effectName}]' is unmatched or out of order.", context);
            }

            searchIndex = end + 1;
        }

        while (openTags.Count > 0)
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} effect tag '[{openTags.Pop()}]' is not closed on the same line.", context);
    }

    private void ValidateInlineEffectArguments(
        string effectName,
        string effectExpression,
        int lineNumber,
        string sourcePath,
        string scope,
        Object context)
    {
        if (!RandomSizeInlineEffects.Contains(effectName))
            return;

        int equalsIndex = effectExpression.IndexOf('=');
        if (equalsIndex < 0 || equalsIndex + 1 >= effectExpression.Length)
            return;

        string value = effectExpression.Substring(equalsIndex + 1);
        string[] parts = value.Split(',', ';', '|', '~');
        if (parts.Length < 2)
        {
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} random size tag '{effectExpression}' should use a min,max range such as 95,110.", context);
            return;
        }

        if (!TryParseInlineScale(parts[0], out float minScale) ||
            !TryParseInlineScale(parts[1], out float maxScale))
        {
            AddIssue(IssueSeverity.Warning, scope, $"{sourcePath}:{lineNumber} random size tag '{effectExpression}' has an invalid scale range.", context);
            return;
        }

        RandomSizeSettings randomSizeSettings =
            DialogueTextAnimationProfileSO.LoadDefaultOrFallback().RandomSize;
        float lower = Mathf.Min(minScale, maxScale);
        float upper = Mathf.Max(minScale, maxScale);
        if (lower < randomSizeSettings.ClampMinScale || upper > randomSizeSettings.ClampMaxScale)
        {
            AddIssue(
                IssueSeverity.Warning,
                scope,
                $"{sourcePath}:{lineNumber} random size tag '{effectExpression}' is outside the configured random size clamp range.",
                context);
        }
    }

    private static string ExtractInlineEffectName(string expression)
    {
        if (string.IsNullOrWhiteSpace(expression))
            return string.Empty;

        int equalsIndex = expression.IndexOf('=');
        return equalsIndex >= 0
            ? expression.Substring(0, equalsIndex).Trim()
            : expression.Trim();
    }

    private static bool TryParseInlineScale(string value, out float scale)
    {
        scale = 1f;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        string trimmed = value.Trim();
        if (trimmed.EndsWith("%", StringComparison.Ordinal))
            trimmed = trimmed.Substring(0, trimmed.Length - 1);

        if (!float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed))
            return false;

        scale = parsed > 2f ? parsed / 100f : parsed;
        return scale > 0f;
    }

    private void ValidateBossEncounterStartPaths(NPCData npc)
    {
        IReadOnlyList<BossEncounterDialogueEntry> entries = npc.BossEncounterDialogues;
        if (entries == null || entries.Count == 0)
            return;

        string scope = GetNpcScope(npc);
        for (int i = 0; i < entries.Count; i++)
        {
            BossEncounterDialogueEntry entry = entries[i];
            if (entry == null)
                continue;

            TextAsset ink = entry.InkOverride != null ? entry.InkOverride : npc.BossEncounterInk;
            if (ink == null)
            {
                AddIssue(IssueSeverity.Error, scope, $"Boss encounter entry {i} has no Ink source.", npc);
                continue;
            }

            if (string.IsNullOrWhiteSpace(entry.StartPath))
                continue;

            if (!CanChoosePath(ink, entry.StartPath, out string error))
                AddIssue(IssueSeverity.Error, scope, $"Boss encounter entry {i} startPath '{entry.StartPath}' is invalid: {error}", ink);
        }
    }

    private void ValidateAffectionRewards(NPCData npc)
    {
        if (npc.affectionRewards == null)
            return;

        string scope = GetNpcScope(npc);
        for (int i = 0; i < npc.affectionRewards.Count; i++)
        {
            AffectionReward reward = npc.affectionRewards[i];
            if (reward.targetLevel <= 0)
                AddIssue(IssueSeverity.Warning, scope, $"Affection reward {i} has targetLevel <= 0.", npc);

            if (reward.effect == null)
                AddIssue(IssueSeverity.Warning, scope, $"Affection reward {i} has no effect asset.", npc);
        }
    }

    private void ValidateRunSpecialRecords()
    {
        for (int i = 0; i < runSpecialRecords.Count; i++)
        {
            RunSpecialRecord record = runSpecialRecords[i];
            if (record.Asset == null)
                continue;

            ValidateRunSpecial(record.Asset);
        }
    }

    private void ValidateRunSpecial(RunSpecialNpcDialogueSetSO asset)
    {
        SerializedObject serialized = new(asset);
        serialized.Update();

        string scope = $"RunSpecial/{asset.name}";
        RunSpecialNpcFeatureKind kind = asset.FeatureKind;
        if (kind == RunSpecialNpcFeatureKind.Construction)
        {
            ValidateRunSpecialBranch(serialized, "constructionNotStarted", scope, asset);
            ValidateRunSpecialBranch(serialized, "constructionInsufficientFunds", scope, asset);
            ValidateRunSpecialBranch(serialized, "constructionPending", scope, asset);
            ValidateRunSpecialBranch(serialized, "constructionCompleted", scope, asset);
        }
        else if (kind == RunSpecialNpcFeatureKind.SameSceneTeleport)
        {
            ValidateRunSpecialBranch(serialized, "teleportAvailable", scope, asset);
            ValidateRunSpecialBranch(serialized, "teleportLocked", scope, asset);
            ValidateRunSpecialBranch(serialized, "teleportUnavailable", scope, asset);
        }
        else
        {
            AddIssue(IssueSeverity.Warning, scope, $"Unsupported RunSpecial feature kind: {kind}.", asset);
        }
    }

    private void ValidateRunSpecialBranch(
        SerializedObject serialized,
        string branchPropertyName,
        string scope,
        RunSpecialNpcDialogueSetSO context)
    {
        SerializedProperty branch = serialized.FindProperty(branchPropertyName);
        if (branch == null)
        {
            AddIssue(IssueSeverity.Error, scope, $"Missing branch property {branchPropertyName}.", context);
            return;
        }

        SerializedProperty lines = branch.FindPropertyRelative("lines");
        SerializedProperty choices = branch.FindPropertyRelative("choices");
        bool hasLine = HasNonEmptyRunSpecialLines(lines);
        bool hasChoice = choices != null && choices.isArray && choices.arraySize > 0;
        if (!hasLine && !hasChoice)
            AddIssue(IssueSeverity.Warning, scope, $"Branch {branchPropertyName} has no lines or choices.", context);
    }

    private static bool HasNonEmptyRunSpecialLines(SerializedProperty lines)
    {
        if (lines == null || !lines.isArray)
            return false;

        for (int i = 0; i < lines.arraySize; i++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            SerializedProperty text = line?.FindPropertyRelative("text");
            if (text != null && !string.IsNullOrWhiteSpace(text.stringValue))
                return true;
        }

        return false;
    }

    private void RebuildUsageRecords()
    {
        usageRecords.Clear();
        if (selectedNpc == null)
            return;

        AddProjectGuidUsage(selectedNpc);
        AddLoadedComponentUsage(selectedNpc);
        AddPrefabComponentUsage(selectedNpc);
        AddFeatureComponentUsage();

        usageRecords.Sort((a, b) =>
        {
            int kindCompare = string.Compare(a.Kind, b.Kind, StringComparison.OrdinalIgnoreCase);
            if (kindCompare != 0)
                return kindCompare;

            return string.Compare(a.Path, b.Path, StringComparison.OrdinalIgnoreCase);
        });
    }

    private void AddProjectGuidUsage(NPCData npc)
    {
        string npcPath = AssetDatabase.GetAssetPath(npc);
        string guid = AssetDatabase.AssetPathToGUID(npcPath);
        if (string.IsNullOrWhiteSpace(guid))
            return;

        string[] assetGuids = AssetDatabase.FindAssets("t:SceneAsset")
            .Concat(AssetDatabase.FindAssets("t:Prefab"))
            .Distinct()
            .ToArray();
        HashSet<string> addedPaths = new(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < assetGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(assetGuids[i]);
            if (string.Equals(path, npcPath, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(path))
            {
                continue;
            }

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                continue;
            }

            if (text.IndexOf(guid, StringComparison.Ordinal) < 0)
                continue;

            if (addedPaths.Add(path))
            {
                usageRecords.Add(new UsageRecord
                {
                    Kind = "Serialized Reference",
                    Path = path,
                    Detail = $"Contains NPCData GUID {guid}.",
                    Context = AssetDatabase.LoadMainAssetAtPath(path),
                });
            }
        }
    }

    private void AddLoadedComponentUsage(NPCData npc)
    {
        AddLoadedComponentUsage<DialogueTrigger>(npc, "DialogueTrigger", "npcData");
        AddLoadedComponentUsage<BossDialogueRunner>(npc, "BossDialogueRunner", "npcData");
        AddLoadedComponentUsage<BossDefeatEndingSequence>(npc, "BossDefeatEndingSequence", "dialogueNpcData");
    }

    private void AddLoadedComponentUsage<T>(NPCData npc, string kind, string propertyName) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || EditorUtility.IsPersistent(component))
                continue;

            Scene scene = component.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            if (GetObjectReference(component, propertyName) != npc)
                continue;

            usageRecords.Add(new UsageRecord
            {
                Kind = kind,
                Path = $"{scene.name}/{GetHierarchyPath(component.transform)}",
                Detail = $"Loaded scene component property '{propertyName}' references selected NPCData.",
                Context = component,
            });
        }
    }

    private void AddPrefabComponentUsage(NPCData npc)
    {
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            AddPrefabComponentUsage<DialogueTrigger>(prefab, path, npc, "DialogueTrigger", "npcData");
            AddPrefabComponentUsage<BossDialogueRunner>(prefab, path, npc, "BossDialogueRunner", "npcData");
            AddPrefabComponentUsage<BossDefeatEndingSequence>(prefab, path, npc, "BossDefeatEndingSequence", "dialogueNpcData");
        }
    }

    private void AddPrefabComponentUsage<T>(
        GameObject prefab,
        string path,
        NPCData npc,
        string kind,
        string propertyName) where T : Component
    {
        T[] components = prefab.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || GetObjectReference(component, propertyName) != npc)
                continue;

            usageRecords.Add(new UsageRecord
            {
                Kind = $"{kind} Prefab",
                Path = path,
                Detail = $"Prefab component property '{propertyName}' references selected NPCData.",
                Context = component,
            });
        }
    }

    private void AddFeatureComponentUsage()
    {
        AddLoadedFeatureComponentUsage<NPCFeatureController>("NPCFeatureController");
        AddLoadedFeatureComponentUsage<UpgradeFeature>("UpgradeFeature");
        AddLoadedFeatureComponentUsage<MerchantNPC>("MerchantNPC");
        AddLoadedFeatureComponentUsage<RunSpecialNpcInteractor>("RunSpecialNpcInteractor");

        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab");
        for (int i = 0; i < prefabGuids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(prefabGuids[i]);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null)
                continue;

            AddPrefabFeatureComponentUsage<NPCFeatureController>(prefab, path, "NPCFeatureController");
            AddPrefabFeatureComponentUsage<UpgradeFeature>(prefab, path, "UpgradeFeature");
            AddPrefabFeatureComponentUsage<MerchantNPC>(prefab, path, "MerchantNPC");
            AddPrefabFeatureComponentUsage<RunSpecialNpcInteractor>(prefab, path, "RunSpecialNpcInteractor");
        }
    }

    private void AddLoadedFeatureComponentUsage<T>(string kind) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null || EditorUtility.IsPersistent(component))
                continue;

            Scene scene = component.gameObject.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                continue;

            usageRecords.Add(new UsageRecord
            {
                Kind = $"Feature Component/{kind}",
                Path = $"{scene.name}/{GetHierarchyPath(component.transform)}",
                Detail = "Read-only NPC feature component presence.",
                Context = component,
            });
        }
    }

    private void AddPrefabFeatureComponentUsage<T>(GameObject prefab, string path, string kind) where T : Component
    {
        T[] components = prefab.GetComponentsInChildren<T>(true);
        for (int i = 0; i < components.Length; i++)
        {
            T component = components[i];
            if (component == null)
                continue;

            usageRecords.Add(new UsageRecord
            {
                Kind = $"Feature Component/{kind} Prefab",
                Path = path,
                Detail = "Read-only prefab NPC feature component presence.",
                Context = component,
            });
        }
    }

    private static Object GetObjectReference(Object target, string propertyName)
    {
        SerializedObject serialized = new(target);
        SerializedProperty property = serialized.FindProperty(propertyName);
        return property != null ? property.objectReferenceValue : null;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        if (transform == null)
            return string.Empty;

        Stack<string> names = new();
        Transform current = transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }

    private static bool IsValidInkJson(TextAsset inkJson, out string error)
    {
        error = string.Empty;
        if (inkJson == null)
        {
            error = "TextAsset is null.";
            return false;
        }

        try
        {
            _ = new Story(inkJson.text);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static bool CanChoosePath(TextAsset inkJson, string startPath, out string error)
    {
        error = string.Empty;
        try
        {
            Story story = new(inkJson.text);
            story.ChoosePathString(startPath);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private static string TryLoadInkSource(TextAsset inkJson, out string sourcePath)
    {
        sourcePath = string.Empty;
        if (inkJson == null)
            return string.Empty;

        try
        {
            InkFile inkFile = InkLibrary.GetInkFileWithJSONFile(inkJson);
            if (inkFile != null && inkFile.inkAsset != null)
            {
                sourcePath = AssetDatabase.GetAssetPath(inkFile.inkAsset);
                if (File.Exists(sourcePath))
                    return File.ReadAllText(sourcePath);
            }
        }
        catch (Exception)
        {
            // Fall back to same-name source lookup below.
        }

        string jsonPath = AssetDatabase.GetAssetPath(inkJson);
        if (string.IsNullOrWhiteSpace(jsonPath))
            return string.Empty;

        string sameNameInk = System.IO.Path.ChangeExtension(jsonPath, ".ink");
        if (!File.Exists(sameNameInk))
            return string.Empty;

        sourcePath = sameNameInk;
        return File.ReadAllText(sameNameInk);
    }

    private bool TryFindNpcById(int npcId, out NPCData npc)
    {
        npc = npcRecords
            .Select(record => record.Npc)
            .FirstOrDefault(candidate => candidate != null && candidate.id == npcId);
        return npc != null;
    }

    private static bool SpriteLibraryHasLabel(SpriteLibraryAsset library, string category, string label)
    {
        if (library == null || string.IsNullOrWhiteSpace(category) || string.IsNullOrWhiteSpace(label))
            return false;

        bool hasCategory = library.GetCategoryNames()
            .Any(candidate => string.Equals(candidate, category, StringComparison.OrdinalIgnoreCase));
        if (!hasCategory)
            return false;

        return library.GetCategoryLabelNames(category)
            .Any(candidate => string.Equals(candidate, label, StringComparison.OrdinalIgnoreCase));
    }

    private void AddIssue(IssueSeverity severity, string scope, string message, Object context)
    {
        issues.Add(new ValidationIssue(severity, scope, message, context));
    }

    private static string GetNpcScope(NPCData npc)
    {
        if (npc == null)
            return "NPC/null";

        string displayName = string.IsNullOrWhiteSpace(npc.npcName) ? npc.name : npc.npcName;
        return $"NPC/{npc.id}/{displayName}";
    }

    private static string BuildIssueReport()
    {
        NpcCustomizationHubWindow window = GetWindow<NpcCustomizationHubWindow>();
        StringBuilder builder = new();
        builder.AppendLine("NPC Customization Hub Validation Report");
        builder.AppendLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        builder.AppendLine();

        if (window.issues.Count == 0)
        {
            builder.AppendLine("No validation issues.");
            return builder.ToString();
        }

        for (int i = 0; i < window.issues.Count; i++)
        {
            ValidationIssue issue = window.issues[i];
            builder.AppendLine($"[{issue.Severity}] {issue.Scope}: {issue.Message}");
        }

        return builder.ToString();
    }

    private static string[] SplitLines(string text)
    {
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private static string NormalizeSearchToken(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        StringBuilder builder = new(value.Length);
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private static string SanitizeFileName(string value)
    {
        string fallback = string.IsNullOrWhiteSpace(value) ? "Npc" : value;
        char[] invalidChars = System.IO.Path.GetInvalidFileNameChars();
        StringBuilder builder = new(fallback.Length);
        for (int i = 0; i < fallback.Length; i++)
        {
            char c = fallback[i];
            builder.Append(invalidChars.Contains(c) || char.IsWhiteSpace(c) ? '_' : c);
        }

        return builder.ToString();
    }
}
#endif

