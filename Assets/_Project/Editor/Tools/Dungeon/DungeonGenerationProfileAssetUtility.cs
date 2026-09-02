using System;
using System.IO;
using UnityEditor;

/// <summary>
/// 책임:
/// - 테마 룸 라이브러리에 대응하는 DungeonGenerationProfileSO를 검색하거나 안전한 기본 경로에 생성한다.
/// - 프로필이 실제 Build Settings 씬에서 참조되는지 제작 툴이 확인할 수 있게 한다.
/// </summary>
public static class DungeonGenerationProfileAssetUtility
{
    public const string ProfileFolder = "Assets/_Project/Data/Dungeon/GenerationProfiles";

    /// <summary>
    /// 책임 : 지정한 룸 라이브러리를 소유한 기존 생성 프로필을 찾는다.
    /// </summary>
    public static DungeonGenerationProfileSO FindForLibrary(RoomThemeLibrarySO library)
    {
        if (library == null)
            return null;

        string[] guids = AssetDatabase.FindAssets("t:DungeonGenerationProfileSO");
        Array.Sort(guids, StringComparer.Ordinal);
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            DungeonGenerationProfileSO profile =
                AssetDatabase.LoadAssetAtPath<DungeonGenerationProfileSO>(path);
            if (profile != null && profile.RoomLibrary == library)
                return profile;
        }

        return null;
    }

    /// <summary>
    /// 책임 : 기존 프로필을 재사용하고, 없을 때만 현재 미리보기 값을 초기값으로 가진 프로필을 만든다.
    /// </summary>
    public static DungeonGenerationProfileSO FindOrCreateForLibrary(
        RoomThemeLibrarySO library,
        DungeonLayoutPolicySO layoutPolicy,
        int seed,
        int roomCount,
        bool includeBossRoom,
        int maxPlacementAttemptsPerRoom,
        int minimumCorridorLength,
        float corridorLengthPerRoomCell,
        int corridorLengthVariation)
    {
        if (library == null)
            throw new ArgumentNullException(nameof(library));

        DungeonGenerationProfileSO profile = FindForLibrary(library);
        if (profile != null)
            return profile;

        EnsureFolder(ProfileFolder);
        string fileName = SanitizeFileName(
            string.IsNullOrWhiteSpace(library.ThemeId) ? library.name : library.ThemeId);
        string path = AssetDatabase.GenerateUniqueAssetPath(
            $"{ProfileFolder}/{fileName}GenerationProfile.asset");
        profile = UnityEngine.ScriptableObject.CreateInstance<DungeonGenerationProfileSO>();
        profile.EditorConfigure(
            library,
            layoutPolicy,
            seed,
            roomCount,
            includeBossRoom,
            maxPlacementAttemptsPerRoom,
            minimumCorridorLength,
            corridorLengthPerRoomCell,
            corridorLengthVariation);
        profile.name = Path.GetFileNameWithoutExtension(path);
        AssetDatabase.CreateAsset(profile, path);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }

    /// <summary>
    /// 책임 : 저장된 프로필을 직접 의존하는 활성 Build Settings 씬 수를 계산한다.
    /// </summary>
    public static int CountEnabledBuildSceneReferences(DungeonGenerationProfileSO profile)
    {
        string profilePath = AssetDatabase.GetAssetPath(profile);
        if (string.IsNullOrWhiteSpace(profilePath))
            return 0;

        int count = 0;
        EditorBuildSettingsScene[] scenes = EditorBuildSettings.scenes;
        for (int sceneIndex = 0; sceneIndex < scenes.Length; sceneIndex++)
        {
            if (!scenes[sceneIndex].enabled)
                continue;

            string[] dependencies = AssetDatabase.GetDependencies(scenes[sceneIndex].path, true);
            for (int dependencyIndex = 0; dependencyIndex < dependencies.Length; dependencyIndex++)
            {
                if (!string.Equals(dependencies[dependencyIndex], profilePath, StringComparison.Ordinal))
                    continue;

                count++;
                break;
            }
        }

        return count;
    }

    private static string SanitizeFileName(string value)
    {
        char[] invalidCharacters = Path.GetInvalidFileNameChars();
        string result = value ?? string.Empty;
        for (int i = 0; i < invalidCharacters.Length; i++)
            result = result.Replace(invalidCharacters[i], '_');

        return string.IsNullOrWhiteSpace(result) ? "Dungeon" : result.Trim();
    }

    private static void EnsureFolder(string folder)
    {
        string normalized = folder.Replace('\\', '/').TrimEnd('/');
        if (AssetDatabase.IsValidFolder(normalized))
            return;

        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);

            current = next;
        }
    }
}
