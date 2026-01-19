using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "SwordCombo2DData", menuName = "GAS/Samples/Data/Sword Combo 2D")]
    public class SwordCombo2DData : ScriptableObject
    {
        [Header("Combo")]
        public float comboResetTime = 0.45f;
        public float[] damages = new float[3] { 10f, 10f, 30f };
        public string[] animTriggers = new string[3] { "SwordCombo1", "SwordCombo2", "SwordCombo3" };

        [Header("Hit Timing (Animation Event)")]
        public GameplayTag hitEventTag;        // Event.Anim.SwordCombo.Hit
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
        public GameplayEffect damageEffect;     // GE_Damage_Spec (ISpecGameplayEffect)
    }
}
