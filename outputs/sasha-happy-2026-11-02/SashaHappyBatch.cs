using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.U2D.Animation;
using UnityEngine;
using UnityEngine.U2D.Animation;

public static class SashaHappyBatch
{
    const string Folder = "Assets/_Project/Art/Sprites/NPCs/merchant/";
    const string Library = "Assets/_Project/Data/Dialogue/NPC/SpriteLibrary/SpriteLibraryAsset_Npc_Merchant.spriteLib";
    const string Ink = "Assets/_Project/Data/Dialogue/Ink/AnimatedVariants/MerchantDialogue_Animated.ink";
    static readonly List<string> Checks = new List<string>();
    static void Check(bool ok, string label) { if (!ok) throw new Exception(label); Checks.Add("PASS " + label); }
    public static void Run()
    {
        try
        {
            string happyPath = Folder + "merchant_happy.png";
            AssetDatabase.ImportAsset(happyPath, ImportAssetOptions.ForceUpdate);
            var reference = (TextureImporter)AssetImporter.GetAtPath(Folder + "merchant.png");
            var importer = (TextureImporter)AssetImporter.GetAtPath(happyPath);
            var settings = new TextureImporterSettings(); reference.ReadTextureSettings(settings);
            importer.SetTextureSettings(settings); importer.spriteImportMode = SpriteImportMode.Single;
            importer.filterMode = FilterMode.Point; importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048; importer.alphaIsTransparency = true; importer.SaveAndReimport();
            var happy = AssetDatabase.LoadAssetAtPath<Sprite>(happyPath);
            var original = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(Library);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string guid, out long localId);
            var normal = original.GetSprite("Face", "Normal");
            var surprised = original.GetSprite("Face", "Surprised");
            var edited = UnityEngine.Object.Instantiate(original);
            try
            {
                edited.AddCategoryLabel(happy, "Face", "Happy");
                SpriteLibrarySourceAssetFactory.SaveAsSourceAsset(edited, Library);
                AssetDatabase.ImportAsset(Library, ImportAssetOptions.ForceUpdate);
            }
            finally { UnityEngine.Object.DestroyImmediate(edited); }
            var library = AssetDatabase.LoadAssetAtPath<SpriteLibraryAsset>(Library);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(library, out string newGuid, out long newId);
            Check(guid == newGuid && localId == newId, "Existing library GUID/local reference preserved");
            Check(library.GetSprite("Face", "Normal") == normal && library.GetSprite("Face", "Surprised") == surprised, "Existing Normal/Surprised mappings preserved");
            Check(library.GetSprite("Face", "Happy") == happy && happy != null, "Happy label resolves to supplied sprite");
            Check(happy.rect.size == new Vector2(1254,1254) && happy.pivot == normal.pivot && happy.pixelsPerUnit == normal.pixelsPerUnit, "Happy full 1254 canvas, pivot and PPU match existing portrait");
            Check(happy.texture.filterMode == FilterMode.Point && !importer.mipmapEnabled && importer.alphaIsTransparency, "Point/alpha/no-mipmap settings");
            var decoded = new Texture2D(2,2); decoded.LoadImage(File.ReadAllBytes(happyPath));
            Check(decoded.GetPixel(0,0).a == 0, "Supplied background retains transparency"); UnityEngine.Object.DestroyImmediate(decoded);
            var compiled = new Ink.Compiler(File.ReadAllText(Ink)).Compile();
            Check(compiled != null, "Merchant Ink compiles");
            string json = compiled.ToJson(); File.WriteAllText(Path.ChangeExtension(Ink,".json"), json);
            foreach (string path in new[]{"affection_0_a","affection_0_b","affection_0_c","affection_1_a","affection_1_b","affection_1_c","affection_2_a","affection_2_b"})
                for (int choice=0; choice<2; choice++)
                {
                    var story = new Ink.Runtime.Story(json); story.ChoosePathString(path); string face="Surprised";
                    Action apply = () => { foreach(string tag in story.currentTags) if(tag.StartsWith("face:")) face=tag.Split(':')[2].Trim(); };
                    while(story.canContinue) { story.Continue(); apply(); }
                    Check(face == "Normal", "Opening resets Normal: "+path+" choice "+choice);
                    story.ChooseChoiceIndex(choice);
                    while(story.canContinue) { story.Continue(); apply(); }
                    string expected=choice==1 ? "Surprised" : (path=="affection_0_a" || path=="affection_1_c" ? "Happy" : "Normal");
                    Check(face==expected && library.GetSprite("Face",face)!=null, "Branch resolves "+expected+": "+path+" choice "+choice);
                }
            AssetDatabase.SaveAssets();
            File.WriteAllLines("validation.txt", Checks.Concat(new[]{"PASS: "+Checks.Count+" focused native checks."}));
            Debug.Log("SASHA_HAPPY_PASS: "+Checks.Count+" checks."); EditorApplication.Exit(0);
        }
        catch(Exception error) { Debug.LogException(error); EditorApplication.Exit(1); }
    }
}
