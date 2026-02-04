using UnityEngine;

public class RelicDebugAddKey : MonoBehaviour
{
    public KeyCode key = KeyCode.R;
    public RelicDefinition testRelic;

    [Tooltip("디버그로 추가할 강화량. 0 이하면 testRelic.dropLevel을 사용합니다.")]
    public int gainedLevel = 0;

    private RelicInventory inv;

    private void Awake()
    {
        inv = GetComponent<RelicInventory>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(key))
            inv?.TryAcquireOrUpgrade(testRelic, gainedLevel);
    }
}
