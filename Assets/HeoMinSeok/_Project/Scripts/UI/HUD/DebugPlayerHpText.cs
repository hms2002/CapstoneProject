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
        TryResolvePlayerAttributes();

        if (text == null)
            text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        PlayerRuntimeRegistry.PlayerRegistered += HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered += HandlePlayerUnregistered;
        TryResolvePlayerAttributes();
    }

    private void OnDisable()
    {
        PlayerRuntimeRegistry.PlayerRegistered -= HandlePlayerRegistered;
        PlayerRuntimeRegistry.PlayerUnregistered -= HandlePlayerUnregistered;
    }


    private void HandlePlayerRegistered(SampleTopDownPlayer registeredPlayer)
    {
        player = registeredPlayer != null ? registeredPlayer.gameObject : null;
        _attrs = registeredPlayer != null ? registeredPlayer.GetComponent<AttributeSet>() : null;
    }

    private void HandlePlayerUnregistered(SampleTopDownPlayer unregisteredPlayer)
    {
        if (unregisteredPlayer != null && player == unregisteredPlayer.gameObject)
        {
            player = null;
            _attrs = null;
        }
    }

    private void TryResolvePlayerAttributes()
    {
        if (player == null)
        {
            var currentPlayer = PlayerRuntimeRegistry.CurrentPlayer != null
                ? PlayerRuntimeRegistry.CurrentPlayer.gameObject
                : SampleTopDownPlayer.Instance != null ? SampleTopDownPlayer.Instance.gameObject : null;

            player = currentPlayer;
        }

        if (player != null)
            _attrs = player.GetComponent<AttributeSet>();
    }

    private void Update()
    {
        if (_attrs == null)
            TryResolvePlayerAttributes();
        if (_attrs == null || hpDef == null || maxHpDef == null || text == null)
            return;

        float hp = _attrs.GetAttributeValue(hpDef);
        float maxHp = _attrs.GetAttributeValue(maxHpDef);

        text.text = string.Format(format, hp, maxHp);
    }
}
