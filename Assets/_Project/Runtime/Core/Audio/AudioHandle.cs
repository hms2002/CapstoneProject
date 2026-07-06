using System;

namespace CapstoneAudio
{
    /// <summary>
    /// 책임: Core 호출자가 구체 SoundManager 참조 없이 재생 중인 추적 사운드를 식별하게 하는 불투명 값 핸들이다.
    /// </summary>
    [Serializable]
    public readonly struct AudioHandle : IEquatable<AudioHandle>
    {
        private readonly int id;

        public AudioHandle(int id)
        {
            this.id = id;
        }

        public bool IsValid => id > 0;

        public int Id => id;

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
