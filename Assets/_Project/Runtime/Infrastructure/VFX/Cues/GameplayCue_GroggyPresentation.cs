using UnityEngine;

namespace UnityGAS
{
    /// <summary>
    /// 책임 :
    /// - 공통 그로기 GameplayCue를 보스 쪽 BossGroggyPresentation에 연결한다.
    /// - Cue는 특정 보스 구현을 몰라도 되고, 대상에 연결된 공통 Presentation만 찾아 enter/while/exit를 전달한다.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GameplayCue_GroggyPresentation : GameplayCueNotify
    {
        public override void OnExecute(GameplayCueParams p)
        {
            ForwardAdded(p);
        }

        public override void OnAdd(GameplayCueParams p)
        {
            ForwardAdded(p);
        }

        public override void OnRemove(GameplayCueParams p)
        {
            BossGroggyPresentation presentation = ResolvePresentation(p);
            if (presentation == null)
                return;

            presentation.HandleCueRemoved();
        }

        public override void OnRefresh(GameplayCueParams p)
        {
            BossGroggyPresentation presentation = ResolvePresentation(p);
            if (presentation == null)
                return;

            presentation.HandleCueRefreshed();
        }

        /// <summary>
        /// 책임 :
        /// - Execute/Add 경로에서 공통적으로 그로기 진입/유지 시작을 전달한다.
        /// - 동일한 실행 의미를 한 곳으로 모아 중복 분기를 줄인다.
        /// </summary>
        private static void ForwardAdded(GameplayCueParams p)
        {
            BossGroggyPresentation presentation = ResolvePresentation(p);
            if (presentation == null)
                return;

            presentation.HandleCueAdded();
        }

        /// <summary>
        /// 책임 :
        /// - Cue 대상 오브젝트에서 사용할 BossGroggyPresentation을 찾아 반환한다.
        /// - 대상 루트 또는 자식 어느 쪽에 붙어 있어도 재사용 가능하도록 탐색 범위를 넓힌다.
        /// </summary>
        private static BossGroggyPresentation ResolvePresentation(GameplayCueParams p)
        {
            if (p.Target == null)
                return null;

            BossGroggyPresentation onTarget = p.Target.GetComponent<BossGroggyPresentation>();
            if (onTarget != null)
                return onTarget;

            return p.Target.GetComponentInChildren<BossGroggyPresentation>(includeInactive: true);
        }
    }
}
