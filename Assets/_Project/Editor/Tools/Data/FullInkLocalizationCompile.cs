using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

internal static class FullInkLocalizationCompile
{
    [Serializable] private sealed class Row { public string key, path, text; }
    [Serializable] private sealed class Document { public List<Row> rows; }

    public static void RunBatch()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated batch mode.");
        try
        {
            var inventory = JsonUtility.FromJson<Document>(File.ReadAllText("DataSheets/Localization/InkInventory.json"));
            foreach (string path in inventory.rows.Select(r => r.path).Distinct())
            {
                var errors = new List<string>();
                var compiler = new Ink.Compiler(File.ReadAllText(path), new Ink.Compiler.Options
                {
                    sourceFilename = path,
                    errorHandler = (message, kind) =>
                    {
                        if (kind == Ink.ErrorType.Error) errors.Add(message);
                    }
                });
                var story = compiler.Compile();
                if (story == null || errors.Count > 0)
                    throw new InvalidOperationException(path + ": " + string.Join("\n", errors));
                string json = story.ToJson();
                foreach (var row in inventory.rows.Where(r => r.path == path))
                    if (!json.Contains("loc:" + row.key))
                        throw new InvalidOperationException("Unlocalized active Ink text: " + row.key);
                string output = Path.ChangeExtension(path, ".json");
                // On Windows imported TextAssets may still hold a mapped read handle.
                AssetDatabase.ReleaseCachedFileHandles();
                string temporary = output + ".localization-tmp";
                File.WriteAllText(temporary, json, new UTF8Encoding(false));
                if (File.Exists(output)) File.Replace(temporary, output, null);
                else File.Move(temporary, output);
                AssetDatabase.ImportAsset(output);
                Debug.Log("[FullInkLocalization] Compiled " + path);
            }
            AssetDatabase.SaveAssets();
            FullLocalizationValidation.RunBatch();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }
}
