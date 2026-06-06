namespace UnityGAS
{
    /// <summary>
    /// Provides stat values by <see cref="StatId"/>.
    ///
    /// This decouples formulas (e.g., ScaledStatFormula) from concrete AttributeDefinition references.
    /// Implementations may return raw attributes (base/add) or derived values (final).
    /// </summary>
    public interface IStatProvider
    {
        float Get(StatId id);
    }
}
