using UnityEngine;

public class RelicDebugAddKey : MonoBehaviour
{
    public KeyCode key = KeyCode.R;
    public RelicDefinition testRelic;

    private RelicInventory inv;

    private void Awake()
    {
        inv = GetComponent<RelicInventory>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(key))
            inv?.TryAdd(testRelic);
    }
}
