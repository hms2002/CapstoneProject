using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace UnityGAS
{
    public class GameplayTagEditor : EditorWindow
    {
        private class TagNode
        {
            public string Name { get; set; }
            public string FullPath { get; set; }
            public GameplayTag TagAsset { get; set; }
            public List<TagNode> Children { get; } = new List<TagNode>();
            public bool IsExpanded { get; set; } = true;
        }

        private List<TagNode> rootNodes = new List<TagNode>();
        private string newTagName = "";
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
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Gameplay Tag Editor", EditorStyles.boldLabel);

            if (GUILayout.Button("Generate Tags Script"))
            {
                GenerateTagsScript();
            }

            EditorGUILayout.Space();



            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            foreach (var node in rootNodes)
            {
                DrawNode(node, 0);
            }
            EditorGUILayout.EndScrollView();

            EditorGUILayout.Space();

            EditorGUILayout.LabelField("Add New Tag", EditorStyles.boldLabel);


            // Show the currently selected parent and add a "Deselect" button.
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Parent:", selectedNode != null ? selectedNode.FullPath : "None (Will create a new root tag)");
            if (GUILayout.Button("Deselect", GUILayout.Width(120)))
            {
                selectedNode = null;
            }
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
            var tags = new Dictionary<string, TagNode>();
            if (!Directory.Exists(TagAssetPath))
            {
                Directory.CreateDirectory(TagAssetPath);
            }
            var guids = AssetDatabase.FindAssets("t:GameplayTag", new[] { TagAssetPath });

            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var tag = AssetDatabase.LoadAssetAtPath<GameplayTag>(path);
                var parts = tag.name.Split('.');
                string currentPath = "";

                TagNode parent = null;
                for (int i = 0; i < parts.Length; i++)
                {
                    currentPath += (i > 0 ? "." : "") + parts[i];
                    if (!tags.ContainsKey(currentPath))
                    {
                        var newNode = new TagNode { Name = parts[i], FullPath = currentPath };
                        if (parent != null)
                        {
                            parent.Children.Add(newNode);
                        }
                        else
                        {
                            rootNodes.Add(newNode);
                        }
                        tags[currentPath] = newNode;
                    }
                    parent = tags[currentPath];
                    if (i == parts.Length - 1)
                    {
                        parent.TagAsset = tag;
                    }
                }
            }
        }

        private void DrawNode(TagNode node, int indent)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(indent * 20);
            node.IsExpanded = EditorGUILayout.Foldout(node.IsExpanded, node.Name, true);

            if (node.TagAsset != null)
            {
                EditorGUILayout.ObjectField(node.TagAsset, typeof(GameplayTag), false);
            }

            if (GUILayout.Button("Select"))
            {
                selectedNode = node;
            }
            if (GUILayout.Button("-"))
            {
                if (EditorUtility.DisplayDialog("Delete Tag?", $"Are you sure you want to delete the tag '{node.FullPath}' and all its children?", "Yes", "No"))
                {
                    DeleteTag(node);
                }
            }
            EditorGUILayout.EndHorizontal();

            if (node.IsExpanded)
            {
                foreach (var child in node.Children)
                {
                    DrawNode(child, indent + 1);
                }
            }
        }

        private void AddTag(string name)
        {
            if (string.IsNullOrEmpty(name)) return;

            string path = selectedNode != null ? $"{selectedNode.FullPath}.{name}" : name;
            string assetPath = $"{TagAssetPath}/{path}.asset";

            if (AssetDatabase.LoadAssetAtPath<GameplayTag>(assetPath) != null)
            {
                EditorUtility.DisplayDialog("Error", "A tag with this name already exists.", "OK");
                return;
            }

            var newTag = CreateInstance<GameplayTag>();
            newTag.name = path;

            AssetDatabase.CreateAsset(newTag, assetPath);
            AssetDatabase.SaveAssets();
            LoadTags();
        }

        private void DeleteTag(TagNode node)
        {
            if (node.TagAsset != null)
            {
                AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(node.TagAsset));
            }

            foreach (var child in node.Children)
            {
                DeleteTag(child);
            }

            LoadTags();
        }

        private void GenerateTagsScript()
        {
            var builder = new StringBuilder();
            builder.AppendLine("using UnityEngine;");
            builder.AppendLine();
            builder.AppendLine("namespace UnityGAS");
            builder.AppendLine("{");
            builder.AppendLine("    // This is an auto-generated file. Do not modify it manually.");
            builder.AppendLine("    public static class UGAS_Tags");
            builder.AppendLine("    {");

            var allNodes = new List<TagNode>();
            CollectNodes(rootNodes, allNodes);

            foreach (var node in allNodes.Where(n => n.TagAsset != null))
            {
                string variableName = node.FullPath.Replace('.', '_');
                string resourcePath = $"Tags/{node.FullPath}";
                builder.AppendLine($"        public static readonly GameplayTag {variableName} = Resources.Load<GameplayTag>(\"{resourcePath}\");");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");

            if (!Directory.Exists(Path.GetDirectoryName(GeneratedScriptPath)))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(GeneratedScriptPath));
            }

            File.WriteAllText(GeneratedScriptPath, builder.ToString());
            AssetDatabase.Refresh();
            EditorUtility.DisplayDialog("Success", "UGAS_Tags.cs generated successfully.", "OK");
        }

        private void CollectNodes(List<TagNode> nodes, List<TagNode> allNodes)
        {
            foreach (var node in nodes)
            {
                allNodes.Add(node);
                CollectNodes(node.Children, allNodes);
            }
        }
    }
}