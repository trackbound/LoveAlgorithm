using System;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO, ScreenPhase
using LoveAlgo.Events; // RequestPhaseCommand, ScreenPhaseChangedEvent
using UnityEngine;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 화면 페이즈 단일 권위자(ADR-013, FlowCommandController 패턴 미러). <see cref="RequestPhaseCommand"/>(의도)를
    /// 구독해 순수 <see cref="PhaseService"/>로 검증한 뒤, **유일하게** state.phase를 바꾸고
    /// <see cref="ScreenPhaseChangedEvent"/>(사실)를 발행한다. 피처는 직접 화면을 토글하지 않는다 — UI/뷰는
    /// ScreenPhaseChangedEvent에 반응(ADR-007, vn_conventions §6 "컨트롤러에 화면 제어 누적" 안티패턴 해소).
    ///
    /// 부팅 초기 화면은 state.phase 기본값(Schedule)과 씬 초기 active가 일치하므로 별도 동기화 발행이 없다.
    /// 씬 하이어라키: _Managers/PhaseController, 인스펙터에서 <see cref="state"/> 바인딩.
    /// </summary>
    public class PhaseController : MonoBehaviour
    {
        [Tooltip("단일 런타임 상태 SO. 현재 화면 페이즈 보유.")]
        [SerializeField] GameStateSO state;

        /// <summary>상태 SO 바인딩. 인스펙터 또는 부팅 시퀀스가 주입.</summary>
        public GameStateSO State { get => state; set => state = value; }

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<RequestPhaseCommand>(OnPhaseRequested);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>
        /// 페이즈 전환 요청 처리: 순수 <see cref="PhaseService.Resolve"/>로 검증 → 유효하면 state.phase 갱신 +
        /// 통지, 무효면 경고만. 직접 호출도 가능(라이프사이클 비의존 — 테스트/부팅 와이어링).
        /// </summary>
        public void OnPhaseRequested(RequestPhaseCommand cmd)
        {
            if (state == null)
            {
                Debug.LogError("[PhaseController] state(GameStateSO) 미바인딩 — 페이즈 전환 불가.");
                return;
            }

            var r = PhaseService.Resolve(state.Phase, cmd.Target);
            if (!r.Ok)
            {
                Log.Warn($"[PhaseController] 페이즈 전환 거부: {r.Reason}");
                return;
            }

            state.Phase = r.To;
            EventBus.Publish(new ScreenPhaseChangedEvent(r.From, r.To));
        }
    }
}
