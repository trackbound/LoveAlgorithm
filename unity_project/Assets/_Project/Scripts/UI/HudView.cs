using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // DayChanged/AffinityChanged/StatChanged/SaveCompleted/BgmChanged
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 인게임 HUD 뷰. 그간 구독자 없이 떠 있던 통지 이벤트(DayChanged·AffinityChanged·StatChanged·
    /// SaveCompleted·BgmChanged)를 구독해 TMP 텍스트로 표시한다(ADR-007: UI는 표시만, 상태 변경 없음).
    /// 표시 문자열 조립은 순수 <see cref="HudFormat"/>에 위임 — 이 클래스는 구독·바인딩뿐.
    ///
    /// 슬라이스1 범위: 통지 소비 + TMP 갱신. UIManager(패널 루트/show-hide)·프리팹·소지금(MoneyChangedEvent
    /// 부재)은 범위 밖. TMP_Text는 인스펙터 바인딩(미바인딩 필드는 조용히 무시).
    /// </summary>
    public class HudView : MonoBehaviour
    {
        [SerializeField] TMP_Text dayText;
        [SerializeField] TMP_Text moneyText;
        [SerializeField] TMP_Text affinityText;
        [SerializeField] TMP_Text statText;
        [SerializeField] TMP_Text statusText; // 저장/BGM 등 일시 상태 라인

        public TMP_Text DayText { get => dayText; set => dayText = value; }
        public TMP_Text MoneyText { get => moneyText; set => moneyText = value; }
        public TMP_Text AffinityText { get => affinityText; set => affinityText = value; }
        public TMP_Text StatText { get => statText; set => statText = value; }
        public TMP_Text StatusText { get => statusText; set => statusText = value; }

        readonly List<IDisposable> _subs = new();

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<DayChangedEvent>(e => Set(dayText, HudFormat.Day(e.NewDay))));
            _subs.Add(EventBus.Subscribe<MoneyChangedEvent>(e => Set(moneyText, HudFormat.Money(e.NewMoney))));
            _subs.Add(EventBus.Subscribe<AffinityChangedEvent>(e => Set(affinityText, HudFormat.Affinity(e.HeroineId, e.NewScore))));
            _subs.Add(EventBus.Subscribe<StatChangedEvent>(e => Set(statText, HudFormat.Stat(e.StatId, e.NewValue))));
            _subs.Add(EventBus.Subscribe<SaveCompletedEvent>(e => Set(statusText, HudFormat.SaveStatus(e.Success))));
            _subs.Add(EventBus.Subscribe<BgmChangedEvent>(e => Set(statusText, HudFormat.Bgm(e.Name))));
        }

        void OnDisable()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
        }

        static void Set(TMP_Text target, string text)
        {
            if (target != null) target.text = text;
        }
    }
}
