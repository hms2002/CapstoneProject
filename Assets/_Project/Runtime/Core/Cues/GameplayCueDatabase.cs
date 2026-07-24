using System.Collections.Generic;
using UnityEngine;

namespace UnityGAS
{
    [CreateAssetMenu(fileName = "GameplayCueDatabase", menuName = "GAS/Gameplay Cue Database")]
    public class GameplayCueDatabase : ScriptableObject
    {
        public const string DefaultResourcesPath = "Cues/GameplayCueDatabase";

        [SerializeField] private List<GameplayCueDefinition> definitions = new();

        public IReadOnlyList<GameplayCueDefinition> Definitions => definitions;

        public static GameplayCueDatabase LoadDefault()
        {
            return Resources.Load<GameplayCueDatabase>(DefaultResourcesPath);
        }
    }
}
