using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/UI/Glossary Database", fileName = "GlossaryDatabase")]
public class GlossaryDatabase : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public string key;
        [TextArea(2, 10)] public string description;
    }

    public List<Entry> entries = new();

    public bool TryGet(string key, out string description)
    {
        for (int i = 0; i < entries.Count; i++)
        {
            if (entries[i].key == key)
            {
                description = entries[i].description;
                return true;
            }
        }
        description = null;
        return false;
    }
}
