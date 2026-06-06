using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct AddressableAssetKeyEntry
{
    [SerializeField] private Object sourceAsset;
    [SerializeField] private string addressKey;

    public AddressableAssetKeyEntry(Object sourceAsset, string addressKey)
    {
        this.sourceAsset = sourceAsset;
        this.addressKey = addressKey;
    }

    public Object SourceAsset => sourceAsset;
    public string AddressKey => addressKey;
    public bool IsValid => sourceAsset != null && !string.IsNullOrWhiteSpace(addressKey);
}

[CreateAssetMenu(
    fileName = "LoadingAddressableRegistry",
    menuName = "Capstone/Loading/Addressable Registry")]
public sealed class LoadingAddressableRegistrySO : ScriptableObject
{
    public const string DefaultAssetPath = "Assets/_Project/Data/SceneFlow/LoadingManifests/LoadingAddressableRegistry.asset";

    [SerializeField] private List<AddressableAssetKeyEntry> entries = new();

    private Dictionary<int, string> addressKeyBySourceId;

    public IReadOnlyList<AddressableAssetKeyEntry> Entries => entries;

    public bool TryGetAddressKey(Object sourceAsset, out string addressKey)
    {
        addressKey = null;
        if (sourceAsset == null)
            return false;

        EnsureCache();
        return addressKeyBySourceId.TryGetValue(sourceAsset.GetInstanceID(), out addressKey) &&
               !string.IsNullOrWhiteSpace(addressKey);
    }

#if UNITY_EDITOR
    public void ReplaceEntries(IReadOnlyList<AddressableAssetKeyEntry> newEntries)
    {
        entries.Clear();
        if (newEntries != null)
        {
            for (int i = 0; i < newEntries.Count; i++)
            {
                AddressableAssetKeyEntry entry = newEntries[i];
                if (!entry.IsValid)
                    continue;

                entries.Add(entry);
            }
        }

        addressKeyBySourceId = null;
    }
#endif

    private void OnEnable()
    {
        addressKeyBySourceId = null;
    }

    private void EnsureCache()
    {
        if (addressKeyBySourceId != null)
            return;

        addressKeyBySourceId = new Dictionary<int, string>();
        if (entries == null)
            return;

        for (int i = 0; i < entries.Count; i++)
        {
            AddressableAssetKeyEntry entry = entries[i];
            if (!entry.IsValid)
                continue;

            int sourceId = entry.SourceAsset.GetInstanceID();
            if (!addressKeyBySourceId.ContainsKey(sourceId))
                addressKeyBySourceId.Add(sourceId, entry.AddressKey);
        }
    }
}
