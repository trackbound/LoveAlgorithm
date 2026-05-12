using LoveAlgo.Common;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Modules.DayLoop
{
    /// <summary>
    /// DayLoop 모듈 진입점.
    /// 기존 GameState/DayLoopController는 손대지 않고, 일자→페이즈 계산과 변경 이벤트만 제공.
    /// 매 프레임 GameManager.CurrentDay를 폴링해 변화 감지 후 DayChangedEvent / PhaseChangedEvent 발행.
    /// (GameManager가 day 변경 이벤트를 노출하지 않아 폴링 사용. 비용은 int 비교 1회)
    /// 씬 하이어라키: _Modules/DayLoopModule
    /// </summary>
    [DefaultExecutionOrder(-500)]
    public class DayLoopModule : MonoBehaviour, IDayLoop
    {
        int _cachedDay = -1;
        EventPhase _cachedPhase = EventPhase.Opening;

        void Awake()
        {
            Services.Register<IDayLoop>(this);
        }

        void OnEnable()
        {
            SyncFromState();
        }

        void OnDestroy()
        {
            if (Services.TryGet<IDayLoop>() == (IDayLoop)this)
                Services.Unregister<IDayLoop>();
        }

        void Update() => SyncFromState();

        void SyncFromState()
        {
            int day = CurrentDay;
            if (day == _cachedDay) return;

            int oldDay = _cachedDay;
            _cachedDay = day;
            EventBus.Publish(new DayChangedEvent(oldDay, day));

            var newPhase = GetPhaseForDay(day);
            if (newPhase != _cachedPhase)
            {
                var oldPhase = _cachedPhase;
                _cachedPhase = newPhase;
                EventBus.Publish(new PhaseChangedEvent(oldPhase, newPhase, day));
            }
        }

        // === IDayLoop ===

        public int CurrentDay
        {
            get
            {
                // GameState에 CurrentDay 직접 노출되어 있지 않다면 GameManager 경유.
                // 현재 코드는 GameManager.CurrentDay를 사용 — 임시로 그쪽을 본다.
                var gm = LoveAlgo.Core.GameManager.Instance;
                return gm != null ? gm.CurrentDay : 1;
            }
        }

        public EventPhase CurrentPhase => GetPhaseForDay(CurrentDay);

        public EventPhase GetPhaseForDay(int day)
        {
            if (day <= 0) return EventPhase.Opening;
            if (day <= 5)  return EventPhase.Opening;
            if (day == 6)  return EventPhase.Event1;
            if (day <= 9)  return EventPhase.AfterEvent1;
            if (day <= 12) return EventPhase.Festival;
            if (day <= 15) return EventPhase.AfterFestival;
            if (day == 16) return EventPhase.Event2;
            if (day <= 19) return EventPhase.AfterEvent2;
            if (day <= 22) return EventPhase.MT;
            if (day <= 25) return EventPhase.AfterMT;
            if (day == 26) return EventPhase.Event3;
            if (day <= 29) return EventPhase.AfterEvent3;
            if (day == 30) return EventPhase.Confession;
            return EventPhase.Ending;
        }

        public bool IsEventDay(int day)
        {
            var p = GetPhaseForDay(day);
            return p == EventPhase.Event1
                || p == EventPhase.Festival
                || p == EventPhase.Event2
                || p == EventPhase.MT
                || p == EventPhase.Event3
                || p == EventPhase.Confession;
        }

        public bool IsFreeActionDay(int day) => !IsEventDay(day) && GetPhaseForDay(day) != EventPhase.Ending;
    }
}
