using System;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // MoneyChangedEvent, StatChangedEvent
using UnityEngine;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 통합층의 얇은 어댑터(MonoBehaviour). 구 <c>ShopModule</c>+<c>ShopSystem</c>의 Service Locator 경로를
    /// 대체(금지선4): <see cref="PurchaseRequestedCommand"/>를 구독해 순수 <see cref="ShopService.Purchase"/>로
    /// 처리하고 결과를 EventBus 통지로 발행한다(ADR-007). 결정 로직은 전부 ShopService에 있다.
    ///
    /// 발행: 즉시 스탯 변화(StatChangedEvent×n) + 소지금 변경(MoneyChangedEvent) + 완료/거부 통지.
    /// 씬 하이어라키: _Managers/ShopController(또는 Simulation 그룹), 인스펙터에서 <see cref="state"/> 바인딩.
    /// </summary>
    public class ShopController : MonoBehaviour
    {
        [Tooltip("단일 런타임 상태 SO. 소지금/스탯 변경 대상.")]
        [SerializeField] GameStateSO state;

        /// <summary>상태 SO 바인딩. 인스펙터 또는 부팅 시퀀스가 주입.</summary>
        public GameStateSO State { get => state; set => state = value; }

        IDisposable _sub;

        void OnEnable() => _sub = EventBus.Subscribe<PurchaseRequestedCommand>(HandlePurchase);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
        }

        /// <summary>구매 요청 처리: 순수 Purchase 후 결과를 통지로 변환·발행. 직접 호출 가능(테스트/와이어링).</summary>
        public void HandlePurchase(PurchaseRequestedCommand cmd)
        {
            if (state == null)
            {
                Debug.LogError("[ShopController] state(GameStateSO) 미바인딩 — 구매 처리 불가.");
                EventBus.Publish(new ShopRejectedEvent(ShopRejection.ItemNotFound));
                return;
            }

            var result = ShopService.Purchase(state, cmd.Cart);
            if (!result.Ok)
            {
                EventBus.Publish(new ShopRejectedEvent(result.Reason));
                return;
            }

            for (int i = 0; i < result.StatChanges.Length; i++)
                EventBus.Publish(result.StatChanges[i]);

            EventBus.Publish(new MoneyChangedEvent(state.Money));
            EventBus.Publish(new ShopPurchasedEvent(result.TotalCost));
        }
    }
}
