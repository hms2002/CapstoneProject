using System.Collections;
using UnityEngine;

namespace UnityGAS.Sample
{
    [CreateAssetMenu(fileName = "AL_Witch_NormalAttack1", menuName = "GAS/Ability Logic/Witch Boss/AL_Witch_NormalAttack1")]
    public class AbilityLogic_WitchNormalAttack1 : AbilityLogic
    {
        // 이 클래스의 책임:
        // 마녀 보스의 평타1 패턴 ability logic 진입점과 패턴 튜닝 데이터를 함께 제공한다.

        [Header("Normal Attack 1 Data")]
        [SerializeField, Min(1)] private int tileCount = 3;
        [SerializeField, Min(0f)] private float intervalSeconds = 0.3f;
        [SerializeField, Min(0.01f)] private float tileUnitSize = 1.7f;
        [SerializeField, Min(0.1f)] private float tileWidthInTiles = 3f;
        [SerializeField, Min(0.1f)] private float tileHeightInTiles = 6f;
        [SerializeField, Min(0f)] private float hitDurationSeconds = 0.12f;
        [Header("Telegraph Styles")]
        [SerializeField] private AttackTelegraphStyle warningTelegraphStyle;
        [SerializeField] private AttackTelegraphStyle hitTelegraphStyle;
        [SerializeField] private WitchNormalAttack1Tile tilePrefab;
        [SerializeField] private GE_Damage_Spec damageEffect;
        [SerializeField] private float damageAmount = 1f;

        public int TileCount => tileCount;
        public float IntervalSeconds => intervalSeconds;
        public float TileUnitSize => tileUnitSize;
        public float TileWidthInTiles => tileWidthInTiles;
        public float TileHeightInTiles => tileHeightInTiles;
        public float HitDurationSeconds => hitDurationSeconds;
        public AttackTelegraphStyle WarningTelegraphStyle => warningTelegraphStyle;
        public AttackTelegraphStyle HitTelegraphStyle => hitTelegraphStyle;
        public WitchNormalAttack1Tile TilePrefab => tilePrefab;
        public GE_Damage_Spec DamageEffect => damageEffect;
        public float DamageAmount => damageAmount;

        public override IEnumerator Activate(AbilitySystem system, AbilitySpec spec, GameObject initialTarget)
        {
            yield return null;
        }
    }
}
