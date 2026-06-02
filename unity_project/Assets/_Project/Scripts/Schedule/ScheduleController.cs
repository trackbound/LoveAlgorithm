using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // DayEndRequestedEvent
using UnityEngine;

namespace LoveAlgo.Schedule
{
    /// <summary>
    /// 스케줄 통합층의 얇은 부작용 어댑터(MonoBehaviour). 구 <c>ScheduleModule</c>의 Service Locator 등록을
    /// 대체한다(금지선4): 직접 참조/인터페이스 없이 <see cref="ScheduleSelectedCommand"/>를 EventBus로 구독해
    /// 처리하고, 결과를 EventBus 통지로 발행한다(ADR-007).
    ///
    /// 책임은 RNG 주입과 발행뿐 — 결정 로직은 전부 순수한 <see cref="ScheduleService.Execute"/>에 있다.
    /// UI 스폰·<c>ISimulationSubMode</c>·세션버프·페이드/오토세이브 같은 오케스트레이션은 범위 밖(M5/Shop/GameManager).
    /// 씬 하이어라키: _Modules/ScheduleController, 인스펙터에서 <see cref="state"/> 바인딩.
    /// </summary>
    public class ScheduleController : MonoBehaviour
    {
        [Tooltip("단일 런타임 상태 SO. 부팅 리셋/세이브 복원 대상.")]
        [SerializeField] GameStateSO state;

        /// <summary>상태 SO 바인딩. 인스펙터 또는 부팅 시퀀스(GameManager, M4/M5)가 주입.</summary>
        public GameStateSO State { get => state; set => state = value; }

        /// <summary>
        /// 투자 배수 공급원(∈[-0.5,+1.0], §5 ±50~100%). 기본은 UnityEngine.Random.
        /// EditMode 테스트가 결정적 배수를 주입할 수 있도록 교체 가능(순수성은 ScheduleService가 보장).
        /// </summary>
        internal Func<float> InvestMultiplierSource = () => UnityEngine.Random.Range(-0.5f, 1.0f);

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<ScheduleSelectedCommand>(HandleSelection);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>
        /// 스케줄 선택 커맨드 처리: 투자면 RNG 배수 1회 생성 → 순수 Execute → 결과 발행.
        /// 1차 경로는 EventBus 구독이지만, 직접 호출도 가능(라이프사이클 비의존 — 테스트/부팅 와이어링).
        /// </summary>
        public void HandleSelection(ScheduleSelectedCommand cmd)
        {
            if (state == null)
            {
                Debug.LogError("[ScheduleController] state(GameStateSO) 미바인딩 — 스케줄 처리 불가.");
                return;
            }

            float multiplier = cmd.Type == ScheduleType.Invest ? InvestMultiplierSource() : 0f;
            var result = ScheduleService.Execute(state, cmd.Type, multiplier);
            Publish(result);
        }

        /// <summary>Execute 결과를 EventBus 통지로 변환·발행.</summary>
        void Publish(ScheduleResult result)
        {
            if (result.Rejected)
            {
                EventBus.Publish(new ScheduleRejectedEvent(result.Type, result.Reason));
                return;
            }

            for (int i = 0; i < result.StatChanges.Length; i++)
                EventBus.Publish(result.StatChanges[i]);

            EventBus.Publish(new ScheduleAppliedEvent(result.Type, result.Effect, result.MoneyDelta));

            if (result.DayEnded)
                EventBus.Publish(new DayEndRequestedEvent(state.Day));
        }
    }
}
