using System;
using System.Collections;
using LoveAlgo;        // EventScriptCatalogSO
using LoveAlgo.Story;  // StoryAssetLoader
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO, DayLoop, DayAdvanceResult, GameTimeline, DayInfo, JsonSaveStore
using LoveAlgo.Events; // DayEndRequestedEvent, DayChangedEvent, RequestPhaseCommand, PlayScriptCommand, NarrativeFinishedEvent
using UnityEngine;

namespace LoveAlgo.Game
{
    /// <summary>
    /// 게임 흐름 오케스트레이션의 거처(승인된 4매니저 중 GameManager). EventBus + 단일 State SO 패턴(ADR-007):
    /// Service Locator/인터페이스 없이 통지 이벤트를 구독해 하루 전환을 진행한다(금지선4).
    ///
    /// <para>하루전환 코어 = <see cref="DayEndRequestedEvent"/> 구독 → (저녁 이벤트가 있으면 먼저 재생·대기) →
    /// <see cref="DayLoop.AdvanceDay"/> → 결과를 <see cref="DayChangedEvent"/> 통지, 엔딩 진입은
    /// <c>RequestPhaseCommand(Ending)</c> 발행.</para>
    ///
    /// <para>저녁 이벤트 씨임(M3 내러티브): 오늘(<c>state.Day</c>)의 타임라인 <c>DayInfo.EventTag</c>가
    /// <see cref="eventScripts"/> 카탈로그에 매핑돼 있으면, 하루 전환 전에 <c>PlayScriptCommand</c>로 스크립트를
    /// 재생하고 <see cref="NarrativeFinishedEvent"/>까지 코루틴으로 대기한 뒤 전환한다. 카탈로그 미바인딩이거나
    /// 이벤트가 없으면 종전대로 즉시(동기) 전환한다 — 가산적·옵셔널 게이트라 기존 동기 계약을 보존한다.
    /// 나머지 단계(페이드/로딩 M5 UI)는 아직 미존재 시스템이라 seam 주석으로 남긴다(과설계 게이트).</para>
    ///
    /// 씬 하이어라키: _Managers/GameManager, 인스펙터에서 <see cref="state"/>·<see cref="eventScripts"/> 바인딩.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        [Tooltip("단일 런타임 상태 SO. 부팅 리셋/세이브 복원 대상.")]
        [SerializeField] GameStateSO state;

        [Tooltip("이벤트 태그→스토리 스크립트 카탈로그(옵셔널). 비우면 저녁 이벤트 없이 시뮬만 진행.")]
        [SerializeField] EventScriptCatalogSO eventScripts;

        /// <summary>상태 SO 바인딩. 인스펙터 또는 부팅 시퀀스가 주입.</summary>
        public GameStateSO State { get => state; set => state = value; }

        /// <summary>저녁 이벤트 카탈로그 바인딩(옵셔널). 인스펙터 또는 테스트가 주입.</summary>
        public EventScriptCatalogSO EventScripts { get => eventScripts; set => eventScripts = value; }

        IDisposable _sub;
        bool _seamLocked;

        void OnEnable() => _sub = EventBus.Subscribe<DayEndRequestedEvent>(OnDayEndRequested);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
            // 씨임 진행 중 비정상 종료(씬 언로드 등) 시 흐름 게이트가 영구 잠기지 않도록 해제.
            if (_seamLocked) { NarrativeFlowGate.Unlock(); _seamLocked = false; }
        }

        /// <summary>
        /// 하루 종료 요청 처리: 오늘이 이벤트 날(카탈로그 매핑 있음)이면 저녁 이벤트를 재생·대기 후 전환,
        /// 아니면 즉시 전환(<see cref="AdvanceDayAndNotify"/>). 1차 경로는 EventBus 구독이지만 직접 호출도 가능.
        /// </summary>
        public void OnDayEndRequested(DayEndRequestedEvent _)
        {
            if (state == null)
            {
                Debug.LogError("[GameManager] state(GameStateSO) 미바인딩 — 하루 전환 불가.");
                return;
            }

            // seam(M3 내러티브): 저녁 이벤트가 있으면 재생 → 완료까지 await(코루틴), 없으면 즉시 동기 전환.
            // seam(M5 UI): 페이드아웃 → 로딩 진입.
            string eveningPath = ResolveEveningEvent();
            if (!string.IsNullOrEmpty(eveningPath))
                StartCoroutine(PlayEveningEventThenAdvance(eveningPath));
            else
                AdvanceDayAndNotify();
        }

        /// <summary>오늘(state.Day)의 타임라인 이벤트 태그에 매핑된 CSV 상대경로. 카탈로그/이벤트/매핑이 없으면 null.</summary>
        string ResolveEveningEvent()
        {
            if (eventScripts == null) return null;
            DayInfo info = GameTimeline.GetDayInfo(state.Day);
            return info == null ? null : eventScripts.Resolve(info.EventTag);
        }

        /// <summary>
        /// 저녁 이벤트 파일을 읽어 재생 → 종료(<see cref="NarrativeFinishedEvent"/>)까지 대기 → 하루 전환.
        /// 파일 없음/손상은 fail-open(건너뛰고 진행). 대기 구간은 <c>NarrativeFlowGate</c>로 잠가, 기획 도구가
        /// 이 사이 Apply로 스크립트를 교체해 완료 이벤트가 영영 안 와 데드락되는 일을 막는다.
        /// </summary>
        IEnumerator PlayEveningEventThenAdvance(string csvPath)
        {
            string csv = StoryAssetLoader.Read(csvPath);
            if (string.IsNullOrEmpty(csv))
            {
                // fail-open: 저녁 이벤트 건너뛰고 하루 진행(기존 "이벤트 없으면 하루 안 막음" 계약 보존).
                Log.Warn($"[GameManager] 저녁 이벤트 로드 실패 — 건너뜀: {csvPath}");
                AdvanceDayAndNotify();
                yield break;
            }

            NarrativeFlowGate.Lock();
            _seamLocked = true;

            bool finished = false;
            string scriptName = csvPath; // 이름=경로(다른 스크립트 완료와 섞이지 않게 매칭).
            using (EventBus.Subscribe<NarrativeFinishedEvent>(e => { if (e.ScriptName == scriptName) finished = true; }))
            {
                EventBus.Publish(new PlayScriptCommand(csv, scriptName));
                yield return new WaitUntil(() => finished);
            }

            NarrativeFlowGate.Unlock();
            _seamLocked = false;
            AdvanceDayAndNotify();
        }

        /// <summary>
        /// 하루 전환 코어: 일차+1·행동 풀충전·1일1회 제한 리셋(<see cref="DayLoop.AdvanceDay"/>) 후 통지.
        /// 엔딩 진입 시 <c>RequestPhaseCommand(Ending)</c>만 발행하고 오토세이브/통지 없이 반환(구 EndDayAsync 순서).
        /// 아니면 오토세이브(슬롯0) 요청 후 <see cref="DayChangedEvent"/>.
        /// </summary>
        void AdvanceDayAndNotify()
        {
            int prevDay = state.Day;
            DayAdvanceResult r = DayLoop.AdvanceDay(state);

            if (r.EnteredEnding)
            {
                // 구 EndDayAsync도 엔딩 진입 시 오토세이브/페이드 전에 return — 정상 하루 진행·세이브 없음.
                EventBus.Publish(new RequestPhaseCommand(ScreenPhase.Ending));
                return;
            }

            // 오토세이브 요청(슬롯0). 실제 직렬화는 SaveManager 구독자가 수행(SaveRequestedEvent→SaveService).
            EventBus.Publish(new SaveRequestedEvent(JsonSaveStore.AutoSaveSlot, "day-end"));
            // seam(M5 UI): 로딩 이탈 → 페이드인.
            EventBus.Publish(new DayChangedEvent(prevDay, r.Day));
        }
    }
}
