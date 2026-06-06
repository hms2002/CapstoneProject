using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// GAS의 GameplayCueNotify 느낌.
    /// CuePrefab에 이 컴포넌트를 붙여두면, Manager가 OnExecute/OnAdd/OnRemove 호출해줌.
    /// </summary>
    public abstract class GameplayCueNotify : MonoBehaviour
    {
        public virtual void OnExecute(GameplayCueParams p) { }
        public virtual void OnAdd(GameplayCueParams p) { }
        public virtual void OnRemove(GameplayCueParams p) { }

        /// <summary>
        /// 동일 tag/target에 대해 AddCue가 다시 들어왔을 때 호출(스택/갱신 연출에 사용).
        /// </summary>
        public virtual void OnRefresh(GameplayCueParams p) { }
    }
}
