using System.Collections.Generic;
using UnityEngine;
using UnityGAS;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordCombo2DData", menuName = "GAS/Samples/Data/Sword Combo 2D")]
    public class SwordCombo2DData : ScriptableObject
    {
        [System.Serializable]
        public class ElementDamageGroup
        {
            public List<ElementDamageInput> elements = new();
        }

        [Header("Damage Channels")]
        [SerializeField] private DamagePayloadConfig damageConfig = new();
        public DamagePayloadConfig DamageConfig => damageConfig;

        [Tooltip("Per-combo element damages (can contain multiple elements per hit).")]
        public ElementDamageGroup[] elementDamagesByCombo = new ElementDamageGroup[3];
        public float[] staggerDamages = new float[3] { 0f, 0f, 0f };

        [Header("Combo")]
        public float comboResetTime = 0.45f;

        [Header("Damage / Knockback Formula (Per Hit)")]
        [Tooltip("If set, base HP damage for each combo hit is computed from attacker stats via this formula.\nIf null, legacy 'damages[]' is used.")]
        public ScaledStatFormula[] damageFormulas = new ScaledStatFormula[3];

        [Tooltip("If set, knockback impulse for each combo hit is computed from attacker stats via this formula.")]
        public ScaledStatFormula[] knockbackFormulas = new ScaledStatFormula[3];

        [Header("Legacy Base Damage (Deprecated)")]
        public float[] damages = new float[3] { 10f, 10f, 30f };
        public string[] animTriggers = new string[3] { "SwordCombo1", "SwordCombo2", "SwordCombo3" };
        public float[] recoveryOverrides = new float[3];

        [Header("Hit Timing (Animation Event)")]
        public GameplayTag hitEventTag;
        public GameplayTag hitConfirmedTag;
        public float hitEventTimeout = 0.35f;

        [Header("Hitbox")]
        public Vector2 hitboxSize = new Vector2(1.2f, 0.8f);
        public float forwardOffset = 0.9f;
        public float sideOffset = 0.25f;
        public int[] sideSigns = new int[3] { -1, +1, -1 };
        public LayerMask hitLayers;

        [Header("Lunge")]
        public float[] lungeDistances = new float[3] { 0.7f, 0.7f, 1.0f };
        public float[] lungeDurations = new float[3] { 0.08f, 0.08f, 0.10f };

        [Header("Damage Effect")]
        public GameplayEffect damageEffect; // GE_Damage_Spec (HP)

    }
}
