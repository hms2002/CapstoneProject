namespace UnityGAS
{
    public sealed class AbilityCancellationToken
    {
        public bool IsCancelled { get; private set; }
        public void Cancel() => IsCancelled = true;
    }
}
