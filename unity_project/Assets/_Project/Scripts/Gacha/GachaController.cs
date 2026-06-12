using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Common; // EventBus, Log
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events;

namespace LoveAlgo.Gacha
{
    /// <summary>
    /// 가챠 얇은 어댑터(*Controller) — 구매 진입(OpenGachaCommand fromPurchase) 시 즉시 추첨·기록·업적을
    /// 확정하고 결과를 통지한다(ADR-007: 뷰는 확정 결과의 연출만 — 연출 중 크래시에도 조각 손실 없음).
    /// RNG는 주입 가능(<see cref="RollSource"/>, ScheduleController 투자 배수 선례) — 기본 UnityEngine.Random.
    /// </summary>
    public class GachaController : MonoBehaviour
    {
        [SerializeField] GameStateSO state;
        [SerializeField] GachaTuningSO tuning;

        public GameStateSO State { get => state; set => state = value; }
        public GachaTuningSO Tuning { get => tuning; set => tuning = value; }

        /// <summary>[0,1) 난수 공급원(테스트 주입용). null이면 UnityEngine.Random.value.</summary>
        public Func<float> RollSource { get; set; }

        readonly List<IDisposable> _subs = new();

        void OnEnable() => _subs.Add(EventBus.Subscribe<OpenGachaCommand>(OnOpen));

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
        }

        void OnOpen(OpenGachaCommand cmd)
        {
            if (!cmd.FromPurchase) return; // 현황 보기 — 추첨 없음(표시는 뷰 소관)
            if (state == null || tuning == null)
            {
                Log.Warn("[GachaController] state/tuning 미바인딩 — 추첨 무시.");
                return;
            }

            float roll = RollSource != null ? RollSource() : UnityEngine.Random.value;
            int piece = GachaPuzzleService.Draw(state, tuning, roll);

            bool isBonus = piece < 0;
            if (isBonus)
                GachaPuzzleService.RecordBonusPurchase(state); // 완성 후 구매 = 연출만 + 업적 카운트
            else
                GachaPuzzleService.Own(state, tuning, piece);

            EventBus.Publish(new GachaDrawResultEvent(
                piece,
                isBonus,
                GachaPuzzleService.OwnedCount(state),
                GachaPuzzleService.IsComplete(state, tuning)));
        }
    }
}
