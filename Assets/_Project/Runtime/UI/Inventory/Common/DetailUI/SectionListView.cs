using System.Collections.Generic;
using UnityEngine;

public class SectionListView : MonoBehaviour
{
    [SerializeField] private Transform root;
    [SerializeField] private ItemDetailSectionView sectionPrefab;

    private readonly List<ItemDetailSectionView> spawned = new();

    public void Clear()
    {
        for (int i = 0; i < spawned.Count; i++)
        {
            if (spawned[i] != null)
                Destroy(spawned[i].gameObject);
        }
        spawned.Clear();
    }

    public void Add(string header, string body, System.Action<string> onGlossaryClick)
    {
        if (root == null || sectionPrefab == null) return;
        var v = Instantiate(sectionPrefab, root);
        v.Set(header, body, onGlossaryClick);
        spawned.Add(v);
    }
}
