using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO, OverlayGate
using LoveAlgo.Events; // ShowShopCommand, ShowModalCommand
using LoveAlgo.Shop;   // ShopView, SaleSlotView, CartSlotView, CartMath

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// ShopView 런타임 계약: 열기(ShowShopCommand→표시+게이트)·판매 슬롯 클릭=장바구니 토글·수량 ±(99/0 경계)·
    /// 구매하기→확인 모달→"네"만 PurchaseRequestedCommand·완료 이벤트→비우기·뒤로가기(CloseTop)로 닫힘.
    /// 슬롯은 런타임 생성 템플릿을 prefab으로 주입(QuickMenu 테스트 패턴) — 실 prefab 아트와 무관하게
    /// 거동만 결정적으로 검증. 아이템은 ItemDatabase 실데이터에서 차출(SO/폴백 모두 비어있지 않음).
    /// </summary>
    public class ShopViewPlayModeTests
    {
        GameObject _root;
        GameStateSO _state;
        readonly List<IDisposable> _subs = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            if (_root != null) UnityEngine.Object.DestroyImmediate(_root);
            if (_state != null) UnityEngine.Object.DestroyImmediate(_state);
            OverlayGate.Reset();
        }

        ShopView CreateView()
        {
            // 잔존/상주 Game 씬의 ShopView 제거 — 같은 ShowShopCommand에 함께 반응해 게이트·모달이
            // 이중 계상된다(HANDOFF PlayMode 격리 주의, DialogueView 가드 미러).
            foreach (var v in UnityEngine.Object.FindObjectsByType<ShopView>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(v.gameObject);
            // 상주 ShopController 비활성 — 이 픽스처의 PurchaseRequestedCommand를 실처리해 결과 이벤트를
            // 추가 발행하고 실 GameState SO 소지금을 변형한다(컨트롤러 응답은 테스트가 직접 모사).
            foreach (var c in UnityEngine.Object.FindObjectsByType<ShopController>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                c.enabled = false;

            _state = ScriptableObject.CreateInstance<GameStateSO>();
            _root = new GameObject("ShopRoot");
            _root.SetActive(false); // Awake/OnEnable 전 주입

            var viewGo = new GameObject("ShopView", typeof(RectTransform), typeof(CanvasGroup));
            viewGo.transform.SetParent(_root.transform, false);
            var view = viewGo.AddComponent<ShopView>();
            view.Group = viewGo.GetComponent<CanvasGroup>();
            view.State = _state;

            // 판매/장바구니 슬롯 템플릿(활성 보관 — 비활성 원본을 Instantiate하면 클론도 비활성이라
            // Awake/리스너가 안 붙는다. 컨테이너 밖에 있어 스폰 슬롯 탐색에 안 섞임).
            var saleTemplate = new GameObject("SaleSlotTemplate", typeof(RectTransform), typeof(Button));
            saleTemplate.transform.SetParent(_root.transform, false);
            var saleSlot = saleTemplate.AddComponent<SaleSlotView>();
            saleSlot.Button = saleTemplate.GetComponent<Button>();

            var cartTemplate = new GameObject("CartSlotTemplate", typeof(RectTransform));
            cartTemplate.transform.SetParent(_root.transform, false);
            var cartSlot = cartTemplate.AddComponent<CartSlotView>();
            var up = new GameObject("Up", typeof(RectTransform), typeof(Button));
            up.transform.SetParent(cartTemplate.transform, false);
            var down = new GameObject("Down", typeof(RectTransform), typeof(Button));
            down.transform.SetParent(cartTemplate.transform, false);
            cartSlot.UpButton = up.GetComponent<Button>();
            cartSlot.DownButton = down.GetComponent<Button>();

            var saleContainer = new GameObject("SaleContent", typeof(RectTransform));
            saleContainer.transform.SetParent(_root.transform, false);
            var cartContainer = new GameObject("CartContent", typeof(RectTransform));
            cartContainer.transform.SetParent(_root.transform, false);

            var purchaseGo = new GameObject("Purchase", typeof(RectTransform), typeof(Button));
            purchaseGo.transform.SetParent(_root.transform, false);

            view.SaleSlotPrefab = saleSlot;
            view.CartSlotPrefab = cartSlot;
            view.SaleContainer = saleContainer.transform;
            view.CartContainer = cartContainer.transform;
            view.PurchaseButton = purchaseGo.GetComponent<Button>();

            _root.SetActive(true); // Awake(부팅 숨김) + OnEnable(구독)
            return view;
        }

        static SaleSlotView SpawnedSlot(ShopView view, int index)
        {
            var slots = view.SaleContainer.GetComponentsInChildren<SaleSlotView>(true);
            Assert.IsTrue(slots.Length > index, $"판매 슬롯 {index + 1}개 이상 스폰");
            return slots[index];
        }

        static CartSlotView[] CartSlots(ShopView view)
            => view.CartContainer.GetComponentsInChildren<CartSlotView>(true)
                .Where(s => s.ItemId != null).ToArray();

        [UnityTest]
        public IEnumerator Show_BuildsSlots_PushesGate_BackClosesIt()
        {
            var view = CreateView();
            yield return null;

            Assert.IsFalse(view.IsVisible, "부팅 숨김");
            EventBus.Publish(new ShowShopCommand());
            Assert.IsTrue(view.IsVisible, "열기 명령 → 표시");
            Assert.IsTrue(OverlayGate.IsBlocked, "게임플레이 입력 차단(게이트)");
            Assert.IsTrue(ItemDatabase.GetAvailable(ItemAvailability.Always).Any(), "해금 아이템 존재 전제");
            SpawnedSlot(view, 0);

            Assert.IsTrue(OverlayGate.CloseTop(), "공용 뒤로가기 → 최상단=상점 닫기");
            Assert.IsFalse(view.IsVisible, "닫힘");
            Assert.IsFalse(OverlayGate.IsBlocked, "게이트 해제");
        }

        [UnityTest]
        public IEnumerator SlotClick_TogglesCart_QtyButtons_ClampAndRemove()
        {
            var view = CreateView();
            yield return null;
            EventBus.Publish(new ShowShopCommand());

            var slot = SpawnedSlot(view, 0);
            string id = slot.Item.Id;

            slot.Button.onClick.Invoke(); // 담기
            Assert.AreEqual(1, view.Cart[id], "클릭 → 수량 1로 담김");
            Assert.IsTrue(slot.IsSelected, "선택 체크 표시");
            yield return null;

            // 수량 변경마다 1프레임 양보 — RefreshCart의 Destroy는 지연 파괴라 같은 프레임 재조회 시
            // 파괴 대기 슬롯이 중복 집계된다.
            var cartSlot = CartSlots(view).Single(c => c.ItemId == id);
            cartSlot.UpButton.onClick.Invoke();
            Assert.AreEqual(2, view.Cart[id], "+ → 2");
            yield return null;

            cartSlot = CartSlots(view).Single(c => c.ItemId == id); // 재구성 후 재취득
            cartSlot.DownButton.onClick.Invoke();
            Assert.AreEqual(1, view.Cart[id], "- → 1");
            yield return null;

            cartSlot = CartSlots(view).Single(c => c.ItemId == id);
            cartSlot.DownButton.onClick.Invoke();
            Assert.IsFalse(view.Cart.ContainsKey(id), "1에서 - → 항목 제거(0=제거 신호)");
            yield return null;

            slot.Button.onClick.Invoke(); // 다시 담고
            slot.Button.onClick.Invoke(); // 재클릭=취소(기획서)
            Assert.IsFalse(view.Cart.ContainsKey(id), "슬롯 재클릭 → 장바구니에서 빠짐");
            Assert.IsFalse(slot.IsSelected, "체크 해제");
        }

        [UnityTest]
        public IEnumerator Purchase_ConfirmModal_YesPublishes_PurchasedClearsCart()
        {
            var view = CreateView();
            yield return null;
            EventBus.Publish(new ShowShopCommand());

            var slot = SpawnedSlot(view, 0);
            string id = slot.Item.Id;
            slot.Button.onClick.Invoke();

            ShowModalCommand modal = default;
            int modals = 0;
            PurchaseRequestedCommand purchase = default;
            int purchases = 0;
            _subs.Add(EventBus.Subscribe<ShowModalCommand>(e => { modal = e; modals++; }));
            _subs.Add(EventBus.Subscribe<PurchaseRequestedCommand>(e => { purchase = e; purchases++; }));

            view.PurchaseButton.onClick.Invoke();
            Assert.AreEqual(1, modals, "구매하기 → 확인 모달");

            modal.Handle.Select(0); // 아니오
            Assert.AreEqual(0, purchases, "아니오 → 구매 명령 미발행");

            view.PurchaseButton.onClick.Invoke();
            modal.Handle.Select(1); // 네
            Assert.AreEqual(1, purchases, "네 → PurchaseRequestedCommand 발행");
            Assert.AreEqual(1, purchase.Cart[id], "발행 장바구니 내용 일치");

            EventBus.Publish(new ShopPurchasedEvent(slot.Item.Price)); // 구매 완료(ShopController 몫 모사)
            Assert.AreEqual(0, view.Cart.Count, "완료 → 장바구니 비움");
            Assert.AreEqual(3, modals, "완료 알림 모달 표시");
        }
    }
}
