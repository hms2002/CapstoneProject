using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D.Animation;

[System.Serializable]
public struct AffectionReward
{
    public int targetLevel;
    public AffectionEffect effect;
}

[CreateAssetMenu(menuName = "NPC/NPC Data")]
public class NPCData : ScriptableObject
{
    [Header("Basic Info")]
    public int id;
    public string npcName;
    public bool isBoss;

    [Header("Main Dialogue")]
    [SerializeField] private TextAsset primaryInk;

    [Header("Dialogue Theme")]
    [SerializeField] private DialogueThemeSO dialogueTheme;

    [Header("Affection Rewards")]
    public List<AffectionReward> affectionRewards;

    [Header("Portrait Data")]
    public SpriteLibraryAsset spriteLibraryAsset;

    [Header("Portrait Settings")]
    public Vector2 emoteOffset = new Vector2(300f, 300f);

    public TextAsset PrimaryInk => primaryInk;
    public DialogueThemeSO DialogueTheme => dialogueTheme;

    public void AssignPrimaryInkIfEmpty(TextAsset ink)
    {
        if (primaryInk != null || ink == null)
            return;

        primaryInk = ink;
    }
}
