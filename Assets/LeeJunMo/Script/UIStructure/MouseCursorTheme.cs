using UnityEngine;

[CreateAssetMenu(fileName = "MouseCursorTheme", menuName = "UI/Mouse Cursor Theme")]
public sealed class MouseCursorTheme : ScriptableObject
{
    [Header("Combat Domain")]
    [SerializeField] private MouseCursorDomainDefinition combatDomain = new MouseCursorDomainDefinition();

    [Header("Inventory Domain")]
    [SerializeField] private MouseCursorDomainDefinition inventoryDomain = new MouseCursorDomainDefinition();

    [Header("NPC UI Domain")]
    [SerializeField] private MouseCursorDomainDefinition npcUiDomain = new MouseCursorDomainDefinition();

    [Header("System UI Domain")]
    [SerializeField] private MouseCursorDomainDefinition systemUiDomain = new MouseCursorDomainDefinition();

    [Header("Encyclopedia Domain")]
    [SerializeField] private MouseCursorEncyclopediaDomainDefinition encyclopediaDomain = new MouseCursorEncyclopediaDomainDefinition();

    public MouseCursorSpriteDefinition GetDefinition(MouseCursorDomain domain, MouseCursorVariant variant)
    {
        if (domain == MouseCursorDomain.Encyclopedia)
            return encyclopediaDomain != null ? encyclopediaDomain.GetDefinition(variant) : null;

        return GetDomainDefinition(domain)?.GetDefinition(variant);
    }

    public MouseCursorDomainDefinition GetDomainDefinition(MouseCursorDomain domain)
    {
        return domain switch
        {
            MouseCursorDomain.Inventory => inventoryDomain,
            MouseCursorDomain.NpcUi => npcUiDomain,
            MouseCursorDomain.SystemUi => systemUiDomain,
            MouseCursorDomain.Encyclopedia => null,
            _ => combatDomain
        };
    }
}
