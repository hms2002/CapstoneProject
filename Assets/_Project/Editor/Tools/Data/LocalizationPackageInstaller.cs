using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

internal static class LocalizationPackageInstaller
{
    private static AddRequest request;
    private static double deadline;

    public static void Install()
    {
        request = Client.Add("com.unity.localization");
        deadline = EditorApplication.timeSinceStartup + 300;
        EditorApplication.update += Poll;
    }

    private static void Poll()
    {
        if (request == null) return;
        if (!request.IsCompleted && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Poll;
        if (request.IsCompleted && request.Status == StatusCode.Success)
        {
            Debug.Log("[Localization] Installed " + request.Result.packageId);
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("[Localization] Install failed: " + (request.Error?.message ?? "timeout"));
            EditorApplication.Exit(1);
        }
    }
}
