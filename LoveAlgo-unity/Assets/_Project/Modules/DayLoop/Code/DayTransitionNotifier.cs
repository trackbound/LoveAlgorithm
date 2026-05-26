using LoveAlgo.Common;
using LoveAlgo.Contracts;
using LoveAlgo.UI;
using UnityEngine;

namespace LoveAlgo.Modules.DayLoop
{
    /// <summary>
    /// DayChangedEvent를 받아 "Day N — 페이즈 라벨" 트랜지션 카드를 보여주는 알리미.
    /// 토스트 인프라(PopupManager.Toast) 재사용 — 일반 토스트보다 살짝 길게 유지해
    /// 일자가 넘어가는 흐름을 인지하게 함.
    ///
    /// AfterSceneLoad에서 자동 부트스트랩 — 씬 배치 불필요.
    /// 게임 첫 진입(OldDay &lt; 1)이나 일자 감소(로드/되감기)는 표시 생략.
    /// </summary>
    public class DayTransitionNotifier : MonoBehaviour
    {
        /// <summary>일자 카드 유지 시간 — 토스트 기본보다 약간 길게.</summary>
        const float CardDuration = 2.6f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (Headless.IsEnabled) return;

            var go = new GameObject("[DayTransitionNotifier]");
            DontDestroyOnLoad(go);
            go.AddComponent<DayTransitionNotifier>();
        }

        void Awake()
        {
            this.SubscribeOnDestroy<DayChangedEvent>(OnDayChanged);
        }

        void OnDayChanged(DayChangedEvent evt)
        {
            // 초기 진입(OldDay == -1) 또는 로드/되감기로 일자가 줄어든 경우는 표시 안 함.
            if (evt.OldDay < 1) return;
            if (evt.NewDay <= evt.OldDay) return;
            if (PopupManager.Instance == null) return;

            string title = $"Day {evt.NewDay}";
            string subtitle = ResolvePhaseLabel(evt.NewDay);

            PopupManager.Instance.Toast(title, subtitle, CardDuration);
        }

        string ResolvePhaseLabel(int day)
        {
            var loop = Services.TryGet<IDayLoop>();
            EventPhase phase = loop != null
                ? loop.GetPhaseForDay(day)
                : InferPhase(day);
            return PhaseLabel(phase);
        }

        /// <summary>
        /// IDayLoop 미등록 환경(테스트 등)을 대비한 정적 추론.
        /// DayLoopModule.GetPhaseForDay와 동일 분기. 두 곳을 동기화 유지할 책임은 이 클래스에 있음.
        /// </summary>
        static EventPhase InferPhase(int day)
        {
            if (day <= 0)  return EventPhase.Opening;
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

        public static string PhaseLabel(EventPhase phase) => phase switch
        {
            EventPhase.Opening        => "개강 첫 주",
            EventPhase.Event1         => "1차 이벤트 당일",
            EventPhase.AfterEvent1    => "이벤트 후 일상",
            EventPhase.Festival       => "축제 기간",
            EventPhase.AfterFestival  => "축제 후 일상",
            EventPhase.Event2         => "2차 이벤트 당일",
            EventPhase.AfterEvent2    => "이벤트 후 일상",
            EventPhase.MT             => "MT 기간",
            EventPhase.AfterMT        => "MT 후 일상",
            EventPhase.Event3         => "3차 이벤트 당일",
            EventPhase.AfterEvent3    => "고백을 향해",
            EventPhase.Confession     => "고백의 날",
            EventPhase.Ending         => "마지막 날",
            _                         => "새로운 하루",
        };
    }
}
