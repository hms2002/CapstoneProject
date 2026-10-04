using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

internal static class SteamIntegrationValidation
{
    public static void RunBatch()
    {
        try
        {
            int count = 0;
            var resolve = typeof(GameSettingsService).GetMethod("ResolveInitialLanguage", BindingFlags.NonPublic | BindingFlags.Static);
            string[] codes = { "koreana", "english", "japanese", "schinese", "tchinese" };
            for (int i = 0; i < codes.Length; i++)
            {
                if ((int)(GameLanguageOption)resolve.Invoke(null, new object[] { codes[i], SystemLanguage.French }) != i)
                    throw new Exception("Steam language mapping failed: " + codes[i]);
                count++;
            }
            foreach (string code in new[] { null, "", "german" })
            {
                if ((GameLanguageOption)resolve.Invoke(null, new object[] { code, SystemLanguage.Japanese }) != GameLanguageOption.Japanese)
                    throw new Exception("OS fallback failed.");
                if ((GameLanguageOption)resolve.Invoke(null, new object[] { code, SystemLanguage.French }) != GameLanguageOption.English)
                    throw new Exception("English fallback failed.");
                count += 2;
            }
            if (SteamPlatformService.TryGetGameLanguage(out _) || SteamPlatformService.IsInitialized)
                throw new Exception("Edit Mode must not initialize Steam.");
            count++;
            if (UnityEngine.Object.FindAnyObjectByType<SteamPlatformService>() != null)
                throw new Exception("Edit Mode query created a service.");
            count++;
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(
                "Packages/com.rlabrecque.steamworks.net/Runtime/Steam.cs");
            if (package == null || package.version != "2025.163.0") throw new Exception("Embedded package version mismatch.");
            count++;
#if (UNITY_STANDALONE_WIN || UNITY_STANDALONE_LINUX || UNITY_STANDALONE_OSX) && !DISABLESTEAMWORKS
            if (!Steamworks.Packsize.Test() || !Steamworks.DllCheck.Test())
                throw new Exception("Native Steam library validation failed.");
            count++;
#endif
            Debug.Log("[SteamIntegrationValidation] PASS " + count + " checks; no Steam client initialization or Play Mode coverage.");
            LocalizationValidation.RunBatch();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.delayCall += () => EditorApplication.Exit(1);
        }
    }
}
