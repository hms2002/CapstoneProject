using System;

namespace CapstoneAudio
{
    [Serializable]
    public readonly struct AudioHandle : IEquatable<AudioHandle>
    {
        // 이 구조체의 책임:
        // SoundManager가 재생 중인 루프 사운드를 안전하게 식별할 수 있는 불투명 핸들을 제공한다.

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
