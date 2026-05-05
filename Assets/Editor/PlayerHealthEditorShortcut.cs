using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

public static class PlayerHealthEditorShortcut
{
    private const string MenuPath = "Tools/Runtime/Set Player Health To 1";

    [MenuItem(MenuPath)]
    public static void SetPlayerHealthToOneFromMenu()
    {
        TrySetPlayerHealthToOne();
    }

    [MenuItem(MenuPath, true)]
    private static bool ValidateSetPlayerHealthToOneFromMenu()
    {
        return Application.isPlaying && FindPlayerDeathController() != null;
    }

    [Shortcut(MenuPath, KeyCode.Equals)]
    private static void SetPlayerHealthToOneShortcut()
    {
        TrySetPlayerHealthToOne();
    }

    private static void TrySetPlayerHealthToOne()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[PlayerHealthEditorShortcut] Enter Play Mode before setting player health.");
            return;
        }

        PlayerDeathReturnToHub2D controller = FindPlayerDeathController();
        if (controller == null)
        {
            Debug.LogWarning("[PlayerHealthEditorShortcut] No PlayerDeathReturnToHub2D was found in the loaded scenes.");
            return;
        }

        controller.EditorTrySetHealthToOne();
        EditorGUIUtility.PingObject(controller);
    }

    private static PlayerDeathReturnToHub2D FindPlayerDeathController()
    {
        if (Selection.activeGameObject != null)
        {
            PlayerDeathReturnToHub2D selectedController =
                Selection.activeGameObject.GetComponentInParent<PlayerDeathReturnToHub2D>();

            if (IsSceneController(selectedController))
                return selectedController;
        }

        PlayerDeathReturnToHub2D fallback = null;
        PlayerDeathReturnToHub2D[] controllers = Resources.FindObjectsOfTypeAll<PlayerDeathReturnToHub2D>();
        for (int i = 0; i < controllers.Length; i++)
        {
            PlayerDeathReturnToHub2D controller = controllers[i];
            if (!IsSceneController(controller))
                continue;

            if (controller.isActiveAndEnabled && controller.gameObject.activeInHierarchy)
                return controller;

            fallback ??= controller;
        }

        return fallback;
    }

    private static bool IsSceneController(PlayerDeathReturnToHub2D controller)
    {
        return controller != null &&
               controller.gameObject.scene.IsValid() &&
               controller.gameObject.scene.isLoaded;
    }
}
