using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // FlowCommandRequestedEvent, AffinityChangedEvent, DayChangedEvent
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine.Flow
{
    /// <summary>
    /// 순수 <see cref="FlowCommandInterpreter"/>의 EventBus 어댑터(ScheduleController 패턴 미러). 구 엔진의
    /// Service Locator 경로 대신 <see cref="FlowCommandRequestedEvent"/>를 구독해 명령을 적용하고, 결과를
    /// 통지 이벤트로 발행한다(ADR-007). 결정 로직은 전부 순수 인터프리터에 있고, 여기선 구독·발행뿐.
    ///
    /// slice2에서 "발행 연기"했던 부분의 런타임 호출자 — 이후 ScriptEngine 이식 시 엔진이 Flow 라인을
    /// <see cref="FlowCommandRequestedEvent"/>로 흘려보내면 그대로 동작한다. 제어흐름 Flow(Jump/If 등)는 범위 밖.
    /// 씬 하이어라키: _Managers/FlowCommandController, 인스펙터에서 <see cref="state"/> 바인딩.
    /// </summary>
    public class FlowCommandController : MonoBehaviour
    {
        [Tooltip("단일 런타임 상태 SO. Flow 명령 적용 대상.")]
        [SerializeField] GameStateSO state;

        /// <summary>상태 SO 바인딩. 인스펙터 또는 부팅 시퀀스가 주입.</summary>
        public GameStateSO State { get => state; set => state = value; }

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<FlowCommandRequestedEvent>(OnFlowRequested);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>
        /// Flow 명령 처리: 순수 <see cref="FlowCommandInterpreter.Apply"/> 후 결과 종류에 따라 통지.
        /// 직접 호출도 가능(라이프사이클 비의존 — 테스트/부팅 와이어링).
        /// </summary>
        public void OnFlowRequested(FlowCommandRequestedEvent e)
        {
            if (state == null)
            {
                Debug.LogError("[FlowCommandController] state(GameStateSO) 미바인딩 — Flow 명령 처리 불가.");
                return;
            }

            int prevDay = state.Day;
            FlowCommandResult r = FlowCommandInterpreter.Apply(state, e.Command);

            if (!r.Ok)
            {
                Debug.LogWarning($"[FlowCommandController] Flow 실패: \"{e.Command}\" — {r.Error}");
                return;
            }

            switch (r.Kind)
            {
                case FlowCommandKind.AffinityEventChoice:
                case FlowCommandKind.AffinityPoint:
                    EventBus.Publish(new AffinityChangedEvent(r.HeroineId, r.NewScore));
                    break;
                case FlowCommandKind.Day:
                    EventBus.Publish(new DayChangedEvent(prevDay, r.Day));
                    break;
            }
        }
    }
}
