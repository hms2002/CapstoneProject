namespace UnityGAS
{
    /// <summary>
    /// Stable identifiers for querying stats without directly referencing AttributeDefinitions.
    ///
    /// Notes:
    /// - Keep this enum stable once data assets start referencing it.
    /// - You can extend it freely by adding new values at the end.
    /// </summary>
    public enum StatId
    {
        None = 0,

        // --- Core combat (example set) ---
        AttackBase = 10,
        AttackAdd = 11,
        AttackMul = 12,
        AttackFinal = 13,

        NormalAdd = 20,
        NormalMul = 21,

        SkillAdd = 30,
        SkillMul = 31,

        CritChanceBase = 40,
        CritMultiplier = 41,
        CritChanceAdd = 43,
        CritChanceMul = 44,
        CritChanceFinal = 45,

        // --- Stagger (Groggy) (Base/Add/Mul/Final) ---
        StaggerBase,
        StaggerAdd = 50,
        StaggerMul = 51,
        StaggerFinal,

        FinalMul = 60,

        // Generic/common stats you may want to scale from.
        Health = 100,
        MaxHealth = 101,
        KnockbackPowerBase,
        KnockbackPowerAdd,
        KnockbackPowerMul,
        KnockbackPowerFinal = 120,

        KnockbackResistBase = 130,
        KnockbackResistAdd,
        KnockbackResistMul,
        KnockbackResistFinal,

        MoveSpeedBase = 140,
        MoveSpeedAdd = 141,
        MoveSpeedMul = 142,
        MoveSpeedFinal = 143,

        // --- Element build-up / damage (3 types) (Base/Add/Mul/Final) ---
        FireBase = 200,
        FireAdd,
        FireMul,
        FireFinal,

        BleedBase,
        BleedAdd,
        BleedMul,
        BleedFinal,

        PoisonBase,
        PoisonAdd,
        PoisonMul,
        PoisonFinal,

        ElectricBase = 320,
        ElectricAdd = 321,
        ElectricMul = 322,
        ElectricFinal = 323,

        // --- Evasion (Base/Add/Mul/Final) ---
        EvasionBase = 300,
        EvasionAdd = 301,
        EvasionMul = 302,
        EvasionFinal = 303,

        AttackSpeedBase = 310,
        AttackSpeedAdd = 311,
        AttackSpeedFinal = 312,

    }
}
