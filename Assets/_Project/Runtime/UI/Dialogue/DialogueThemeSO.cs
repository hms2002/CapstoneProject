using UnityEngine;

[CreateAssetMenu(menuName = "Dialogue/Dialogue Theme")]
public class DialogueThemeSO : ScriptableObject
{
    [Header("Shared Outline")]
    public Color outlineColor = Color.white;

    [Header("Speaker Frame")]
    public Color speakerFrameFillColor = Color.white;

    [Header("Effect")]
    public AnimatorOverrideController effectOverride;
}
