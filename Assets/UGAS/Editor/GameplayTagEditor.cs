using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace UnityGAS
{
    public class GameplayTagEditor : EditorWindow
    {
        private class TagNode
        {
            public string Name;
            public string FullPath;
            public GameplayTag TagAsset;
            public List<TagNode> Children = new List<TagNode>();
            public bool IsExpanded = true;
        }

        private List<TagNode> rootNodes = new List<TagNode>();
        private string newTagName = "";
        private string search = "";
        private TagNode selectedNode;
        private Vector2 scrollPosition;

        private const string TagAssetPath = "Assets/UGAS/Resources/Tags";
        private const string GeneratedScriptPath = "Assets/UGAS/Scripts/UGAS_Tags.cs";

        [MenuItem("Window/GAS/Gameplay Tag Editor")]
        public static void ShowWindow()
        {
            GetWindow<GameplayTagEditor>("Gameplay Tags");
        }

        private void OnEnable()
        {
            LoadTags();
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private void OnDisable()
        {
            EditorApplication.projectChanged -= OnProjectChanged;
        }

        private void OnProjectChanged()
        {
            // 프로젝트 변경 시에만 리로드
            LoadTags();
            Repaint();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Gameplay Tag Editor", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Auto Link Parents + Assign Ids", GUILayout.Width(220)))
            {
                AutoLinkParentsAndAssignIds();
                LoadTags();
            }
            if (GUILayout.Button("Generate Tags Script", GUILayout.Width(180)))
            {
                GenerateTagsScript();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            // 검색
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Search", GUILayout.Width(50));
            search = EditorGUILayout.TextField(search);
            if (GUILayout.Button("Clear", GUILayout.Width(60))) search = "";
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();

            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < rootNodes.Count; i++)
            {
                DrawNode(rootNodes[i], 0);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Add New Tag", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Parent:", selectedNode != null ? selectedNode.FullPath : "None (Will create a new root tag)");
            if (GUILayout.Button("Deselect", GUILayout.Width(120)))
                selectedNode = null;
            EditorGUILayout.EndHorizontal();

            newTagName = EditorGUILayout.TextField("New Tag Name", newTagName);

            if (GUILayout.Button("Add Tag"))
            {
                AddTag(newTagName);
                newTagName = "";
            }
        }

        private void LoadTags()
        {
            rootNodes.Clear();

            if (!Directory.Exists(TagAssetPath))
                Directory.CreateDirectory(TagAssetPath);

            var tags = new Dictionary<string, TagNode>(256);
            var guids = AssetDatabase.FindAssets("t:GameplayTag", new[] { TagAssetPath });

            for (int g = 0; g < guids.Length; g++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[g]);
                var tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(path);
                if (tag == null) continue;

                string fullPath = tag.Path; // 신 구조 우선
                if (string.IsNullOrEmpty(fullPath)) fullPath = tag.name;

                // 검색 필터 (간단: path contains)
                if (!string.IsNullOrEmpty(search))
                {
                    if (fullPath.ToLowerInvariant().Contains(search.ToLowerInvariant()) == false)
                        continue;
                }

                var parts = fullPath.Split('.');
                string currentPath = "";
                TagNode parent = null;

                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath += (i > 0 ? "." : "") + parts[i];

                    if (!tags.TryGetValue(currentPath, out var node))
                    {
                        node = new TagNode { Name = parts[i], FullPath = currentPath };
                        tags[currentPath] = node;

                        if (parent != null) parent.Children.Add(node);
                        else rootNodes.Add(node);
                    }

                    parent = node;

                    // leaf = 실제 asset
                    if (i == parts.Length - 1)
                        parent.TagAsset = tag;
                }
            }

            // 간단 정렬
            SortNodes(rootNodes);
        }

        private void SortNodes(List<TagNode> nodes)
        {
            nodes.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Children.Count > 0) SortNodes(nodes[i].Children);
            }
        }

        private void DrawNode(TagNode node, int indent)
        {
            // 검색이 있을 때는 폴드아웃 펼쳐둠(사용성)
            if (!string.IsNullOrEmpty(search)) node.IsExpanded = true;

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 18);

            node.IsExpanded = EditorGUILayout.Foldout(node.IsExpanded, node.Name, true);

            if (node.TagAsset != null)
                EditorGUILayout.ObjectField(node.TagAsset, typeof(GameplayTag), false);

            if (GUILayout.Button("Select", GUILayout.Width(60)))
                selectedNode = node;

            if (GUILayout.Button("-", GUILayout.Width(28)))
            {
                if (EditorUtility.DisplayDialog("Delete Tag?",
                        $"Are you sure you want to delete the tag '{node.FullPath}' and all its children?",
                        "Yes", "No"))
                {
                    DeleteTag(node);
                }
            }

            EditorGUILayout.EndHorizontal();

            if (node.IsExpanded)
            {
                for (int i = 0; i < node.Children.Count; i++)
                    DrawNode(node.Children[i], indent + 1);
            }
        }

        private void AddTag(string name)
        {
            name = (name ?? "").Trim();
            if (string.IsNullOrEmpty(name)) return;
            if (name.Contains(".")) name = name.Replace(".", "_");

            string parentPath = selectedNode != null ? selectedNode.FullPath : null;
            string fullPath = string.IsNullOrEmpty(parentPath) ? name : $"{parentPath}.{name}";
            string assetPath = $"{TagAssetPath}/{fullPath}.asset";

            if (AssetDatabase.LoadAssetAtPath<GameplayTag>(assetPath) != null)
            {
                EditorUtility.DisplayDialog("Error", "A tag with this name already exists.", "OK");
                return;
            }

            // 부모 asset이 없다면(가상 노드) 먼저 만들어준다
            GameplayTag parentAsset = null;
            if (selectedNode != null)
            {
                parentAsset = EnsureTagAssetExists(selectedNode.FullPath);
            }

            var newTag = CreateInstance<GameplayTag>();
            newTag.name = fullPath;

            // 신 구조 세팅
            newTag.Editor_SetName(name);
            newTag.Editor_SetParent(parentAsset);

            AssetDatabase.CreateAsset(newTag, assetPath);
            EditorUtility.SetDirty(newTag);

            AssetDatabase.SaveAssets();
            LoadTags();
        }

        private GameplayTag EnsureTagAssetExists(string fullPath)
        {
            string assetPath = $"{TagAssetPath}/{fullPath}.asset";
            var existing = AssetDatabase.LoadAssetAtPath<GameplayTag>(assetPath);
            if (existing != null) return existing;

            // 중간 노드가 없으면 생성 (Parent도 재귀로 만든다)
            string parentPath = null;
            int lastDot = fullPath.LastIndexOf('.');
            if (lastDot >= 0) parentPath = fullPath.Substring(0, lastDot);
            string nodeName = lastDot >= 0 ? fullPath.Substring(lastDot + 1) : fullPath;

            GameplayTag parentAsset = null;
            if (!string.IsNullOrEmpty(parentPath))
                parentAsset = EnsureTagAssetExists(parentPath);

            var tag = CreateInstance<GameplayTag>();
            tag.name = fullPath;
            tag.Editor_SetName(nodeName);
            tag.Editor_SetParent(parentAsset);

            AssetDatabase.CreateAsset(tag, assetPath);
            EditorUtility.SetDirty(tag);
            AssetDatabase.SaveAssets();
            return tag;
        }

        private void DeleteTag(TagNode node)
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                // 자식부터 삭제
                for (int i = 0; i < node.Children.Count; i++)
                    DeleteTag(node.Children[i]);

                if (node.TagAsset != null)
                {
                    AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(node.TagAsset));
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            LoadTags();
        }

        private void AutoLinkParentsAndAssignIds()
        {
            if (!Directory.Exists(TagAssetPath))
                Directory.CreateDirectory(TagAssetPath);

            var guids = AssetDatabase.FindAssets("t:GameplayTag", new[] { TagAssetPath });

            // path -> tag
            var map = new Dictionary<string, GameplayTag>(guids.Length);
            var all = new List<GameplayTag>(guids.Length);

            for (int i = 0; i < guids.Length; i++)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
                var tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(assetPath);
                if (tag == null) continue;

                string p = tag.Path;
                if (string.IsNullOrEmpty(p)) p = tag.name;

                map[p] = tag;
                all.Add(tag);
            }

            // 안정적 정렬 후 id 부여
            all.Sort((a, b) => string.CompareOrdinal(a.Path, b.Path));

            int nextId = 1;
            var used = new HashSet<int>();

            AssetDatabase.StartAssetEditing();
            try
            {
                for (int i = 0; i < all.Count; i++)
                {
                    var tag = all[i];
                    string fullPath = tag.Path;
                    if (string.IsNullOrEmpty(fullPath)) fullPath = tag.name;

                    // name/parent 추론
                    int lastDot = fullPath.LastIndexOf('.');
                    string parentPath = lastDot >= 0 ? fullPath.Substring(0, lastDot) : null;
                    string nodeName = lastDot >= 0 ? fullPath.Substring(lastDot + 1) : fullPath;

                    tag.Editor_SetName(nodeName);
                    if (!string.IsNullOrEmpty(parentPath) && map.TryGetValue(parentPath, out var parent))
                        tag.Editor_SetParent(parent);
                    else
                        tag.Editor_SetParent(null);

                    // id 할당(중복/0 방지)
                    int id = tag.Id;
                    if (id <= 0 || used.Contains(id))
                    {
                        while (used.Contains(nextId)) nextId++;
                        id = nextId++;
                        tag.Editor_SetId(id);
                    }
                    used.Add(id);

                    EditorUtility.SetDirty(tag);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
                AssetDatabase.SaveAssets();
            }

            EditorUtility.DisplayDialog("Done", "Parents linked & Ids assigned.", "OK");
        }

        private void GenerateTagsScript()
        {
            var builder = new StringBuilder();
            builder.AppendLine("namespace UnityGAS");
            builder.AppendLine("{");
            builder.AppendLine("    // Auto-generated. Do not modify manually.");
            builder.AppendLine("    public static class UGAS_Tags");
            builder.AppendLine("    {");

            // 현재 로드된 트리에서 leaf(실제 asset)만 뽑는다
            var allNodes = new List<TagNode>(256);
            CollectNodes(rootNodes, allNodes);

            for (int i = 0; i < allNodes.Count; i++)
            {
                var node = allNodes[i];
                if (node.TagAsset == null) continue;

                string variableName = node.FullPath.Replace('.', '_');
                string path = node.FullPath;

                // 런타임에서 빠르게 id로 접근 가능하도록 생성
                builder.AppendLine($"        public static readonly int {variableName} = TagRegistry.GetIdByPath(\"{path}\");");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");

            var dir = Path.GetDirectoryName(GeneratedScriptPath);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(GeneratedScriptPath, builder.ToString(), Encoding.UTF8);
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "UGAS_Tags.cs generated successfully.", "OK");
        }

        private void CollectNodes(List<TagNode> nodes, List<TagNode> allNodes)
        {
            for (int i = 0; i < nodes.Count; i++)
            {
                allNodes.Add(nodes[i]);
                if (nodes[i].Children.Count > 0)
                    CollectNodes(nodes[i].Children, allNodes);
            }
        }
    }
}
