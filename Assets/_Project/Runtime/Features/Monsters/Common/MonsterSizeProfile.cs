using UnityEngine;

public enum MonsterSizeCategory { Small = 0, Medium = 1, Large = 2 }

/// <summary>Authored body class for the world HUD; does not change combat stats or colliders.</summary>
[DisallowMultipleComponent]
public sealed class MonsterSizeProfile : MonoBehaviour
{
    [SerializeField] private MonsterSizeCategory size = MonsterSizeCategory.Medium;
    [SerializeField] private bool showHealthBar = true;
    [SerializeField] private Transform hudAnchor;
    public MonsterSizeCategory Size => size;
    public bool ShowHealthBar => showHealthBar;
    public Transform HudAnchor => hudAnchor;
    public float HealthBarWidth => size switch
    {
        MonsterSizeCategory.Small => 75f,
        MonsterSizeCategory.Large => 140f,
        _ => 100f
    };
}
