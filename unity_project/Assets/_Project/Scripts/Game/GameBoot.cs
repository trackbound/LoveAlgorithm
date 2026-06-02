using LoveAlgo.Core;     // GameStateSO, DayLoop
using LoveAlgo.Affinity; // AffinityFormula

namespace LoveAlgo.Game
{
    /// <summary>
    /// 새 게임 초기화 시퀀스(순수). 인스펙터로 할 수 없는 런타임 부팅 단계만 모은다 —
    /// 상태 리셋 + 호감도 공식 정의표 주입(GameBalanceSO) + 데이루프 시작(1일차·행동 풀충전).
    /// MonoBehaviour/EventBus를 모른다 = EditMode 테스트 가능. 매니저/컨트롤러의 State 바인딩은
    /// 씬 인스펙터(또는 별도 인스톨러) 몫 — 여기선 손대지 않아 Game이 전 모듈을 참조하는 결합을 피한다.
    /// </summary>
    public static class GameBoot
    {
        /// <summary>새 게임 시작. balance가 null이면 AffinityFormula는 검증된 폴백 정의표를 쓴다.</summary>
        public static void NewGame(GameStateSO gs, GameBalanceSO balance)
        {
            if (gs == null) return;
            gs.ResetRuntime();
            AffinityFormula.Configure(balance);
            DayLoop.BeginRun(gs);
        }
    }
}
