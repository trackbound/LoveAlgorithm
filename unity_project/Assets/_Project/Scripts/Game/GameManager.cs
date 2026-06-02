using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO, DayLoop, DayAdvanceResult
using LoveAlgo.Events; // DayEndRequestedEvent, DayChangedEvent, EnteredEndingEvent
using UnityEngine;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 게임 흐름 오케스트레이션의 거처(승인된 4매니저 중 GameManager). EventBus + 단일 State SO 패턴(ADR-007):
    /// Service Locator/인터페이스 없이 통지 이벤트를 구독해 하루 전환을 진행한다(금지선4).
    ///
    /// <para>slice1 책임 = 하루전환 코어뿐 — <see cref="DayEndRequestedEvent"/> 구독 → <see cref="DayLoop.AdvanceDay"/>
    /// → 결과를 <see cref="DayChangedEvent"/> 또는 <see cref="EnteredEndingEvent"/>로 통지.</para>
    ///
    /// <para>구 <c>DayLoopController.EndDayAsync</c>의 나머지 단계는 아직 재작성되지 않은 시스템에 의존하므로
    /// 아래 OnDayEndRequested 내부에 명시적 seam(주석)으로 남긴다 — 저녁 이벤트 인라인 실행(M3 내러티브),
    /// 페이드/로딩 연출(M5 UI), 슬롯0 오토세이브(Save 슬라이스), 페이즈 전환(GamePhase 상태머신).
    /// 무존재 시스템용 await 훅 인프라는 지금 만들지 않는다(과설계 게이트).</para>
    ///
    /// 씬 하이어라키: _Managers/GameManager, 인스펙터에서 <see cref="state"/> 바인딩(부팅 와이어링은 후속 슬라이스).
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Tooltip("단일 런타임 상태 SO. 부팅 리셋/세이브 복원 대상.")]
        [SerializeField] GameStateSO state;

        /// <summary>상태 SO 바인딩. 인스펙터 또는 부팅 시퀀스가 주입.</summary>
        public GameStateSO State { get => state; set => state = value; }

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<DayEndRequestedEvent>(OnDayEndRequested);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>
        /// 하루 종료 요청 처리: 일차 +1·행동 풀충전·1일1회 제한 리셋(<see cref="DayLoop.AdvanceDay"/>) 후 통지.
        /// 1차 경로는 EventBus 구독이지만, 직접 호출도 가능(라이프사이클 비의존 — 테스트/부팅 와이어링).
        /// </summary>
        public void OnDayEndRequested(DayEndRequestedEvent _)
        {
            if (state == null)
            {
                Debug.LogError("[GameManager] state(GameStateSO) 미바인딩 — 하루 전환 불가.");
                return;
            }

            // seam(M3 내러티브): 저녁 이벤트가 있으면 인라인 실행 → 완료까지 await (AdvanceDay 앞).
            // seam(M5 UI): 페이드아웃 → 로딩 진입.

            int prevDay = state.Day;
            DayAdvanceResult r = DayLoop.AdvanceDay(state);

            if (r.EnteredEnding)
            {
                // 구 EndDayAsync도 엔딩 진입 시 오토세이브/페이드 전에 return — 정상 하루 진행·세이브 없음.
                EventBus.Publish(new EnteredEndingEvent(r.Day));
                return;
            }

            // seam(Save 슬라이스): 슬롯0 오토세이브("day-end") — 썸네일 캡처 포함은 M5 UI 이후.
            // seam(M5 UI): 로딩 이탈 → 페이드인.

            EventBus.Publish(new DayChangedEvent(prevDay, r.Day));
        }
    }
}
