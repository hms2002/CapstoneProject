using System;

namespace CapstoneAudio
{
    [Serializable]
    public readonly struct AudioHandle : IEquatable<AudioHandle>
    {
        private readonly int id;

        internal AudioHandle(int id)
        {
            this.id = id;
        }

        public bool IsValid => id > 0;

        internal int Id => id;

        public bool Equals(AudioHandle other)
        {
            return id == other.id;
        }

        public override bool Equals(object obj)
        {
            return obj is AudioHandle other && Equals(other);
        }

        public override int GetHashCode()
        {
            return id;
        }

        public override string ToString()
        {
            return IsValid ? $"AudioHandle({id})" : "AudioHandle(Invalid)";
        }

        public static AudioHandle Invalid => default;
    }
}
