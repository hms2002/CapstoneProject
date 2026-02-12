using TMPro;
using UnityEngine;
using UnityGAS;

public class DebugPlayerHpText : MonoBehaviour
{
    [Header("Refs")]
    public GameObject player;
    public AttributeDefinition hpDef;
    public AttributeDefinition maxHpDef;

    [Header("UI")]
    public TMP_Text text;

    [Header("Format")]
    public string format = "HP {0:0}/{1:0}";

    private AttributeSet _attrs;

    private void Awake()
    {
        if (player != null)
            _attrs = player.GetComponent<AttributeSet>();

        if (text == null)
            text = GetComponent<TMP_Text>();
    }

    private void Update()
    {
        if (_attrs == null || hpDef == null || maxHpDef == null || text == null)
            return;

        float hp = _attrs.GetAttributeValue(hpDef);
        float maxHp = _attrs.GetAttributeValue(maxHpDef);

        text.text = string.Format(format, hp, maxHp);
    }
}
