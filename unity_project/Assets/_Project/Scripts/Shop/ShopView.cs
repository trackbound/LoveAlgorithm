using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO, OverlayGate, MoneyFormat
using LoveAlgo.Events; // ShowShopCommand, MoneyChangedEvent, ShowModalCommand, ModalButton, ModalRequest
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 화면 뷰(*View, ADR-013 Overlay — 기획서 내부 콘텐츠 §상점). <see cref="ShowShopCommand"/>로 표시:
    /// 판매 그리드(해금 아이템, 클릭=장바구니 토글, 호버=설명창) + 장바구니(수량 ±·1개당 가격·합산) +
    /// 잔액 + 구매하기(확인 모달 → <see cref="PurchaseRequestedCommand"/> 발행). 결과는
    /// <see cref="ShopPurchasedEvent"/>(완료 모달+비우기)/<see cref="ShopRejectedEvent"/>(불가 모달) 구독으로 표시
    /// — 검증·차감은 전부 ShopService(ADR-007: 표시·발행만). 닫기 = 공용 뒤로가기(OverlayGate 토큰,
    /// SetVisible/SetAsLastSibling = SettingsView 1:1 미러).
    /// 해금: 현재 <see cref="ItemAvailability.Always"/>만 노출 — 선물 해금 게이트(2차/3차 전날)는
    /// ShopService 해금 슬라이스와 함께 후속(범위 동결 일치).
    /// </summary>
    public class ShopView : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("잔액 표시용 상태 SO(GameState_Main).")]
        [SerializeField] GameStateSO state;

        [Header("판매 그리드")]
        [Tooltip("판매 슬롯을 스폰할 컨테이너(Sale Grid/Viewport/Content).")]
        [SerializeField] Transform saleContainer;
        [SerializeField] SaleSlotView saleSlotPrefab;
        [Tooltip("그리드 맨 끝 고정 푸터(ComingSoonFooter) — 스폰 슬롯을 이 앞에 배치.")]
        [SerializeField] RectTransform saleFooter;

        [Header("장바구니")]
        [Tooltip("장바구니 칸을 스폰할 컨테이너(Cart/Viewport/Content).")]
        [SerializeField] Transform cartContainer;
        [SerializeField] CartSlotView cartSlotPrefab;
        [SerializeField] TMP_Text balanceText;
        [SerializeField] TMP_Text totalText;
        [SerializeField] Button purchaseButton;

        [Header("호버 설명창")]
        [SerializeField] ItemTooltipView tooltip;

        [Header("구매 확인/결과 모달 문구")]
        [SerializeField] string confirmMessage = "구매하시겠습니까?";
        [SerializeField] string confirmYes = "네";
        [SerializeField] string confirmNo = "아니오";
        [SerializeField] string purchasedMessage = "구매가 완료되었습니다!";
        [SerializeField] string insufficientMessage = "구매가 불가능합니다.";
        [SerializeField] string closeLabel = "확인";

        // 테스트/배선 주입용(SaveLoadView 패턴 — Awake 전 세팅).
        public CanvasGroup Group { get => canvasGroup; set => canvasGroup = value; }
        public GameStateSO State { get => state; set => state = value; }
        public Transform SaleContainer { get => saleContainer; set => saleContainer = value; }
        public SaleSlotView SaleSlotPrefab { get => saleSlotPrefab; set => saleSlotPrefab = value; }
        public Transform CartContainer { get => cartContainer; set => cartContainer = value; }
        public CartSlotView CartSlotPrefab { get => cartSlotPrefab; set => cartSlotPrefab = value; }
        public TMP_Text BalanceText { get => balanceText; set => balanceText = value; }
        public TMP_Text TotalText { get => totalText; set => totalText = value; }
        public Button PurchaseButton { get => purchaseButton; set => purchaseButton = value; }
        public ItemTooltipView Tooltip { get => tooltip; set => tooltip = value; }

        /// <summary>현재 장바구니(itemId→수량) — 읽기 전용 뷰(테스트 검증용).</summary>
        public IReadOnlyDictionary<string, int> Cart => _cart;
        public bool IsVisible => _visible;

        readonly Dictionary<string, int> _cart = new();
        readonly List<string> _cartOrder = new(); // 담은 순서 표시(기획서 목업 — 리스트 위→아래)
        readonly List<SaleSlotView> _saleSlots = new();
        readonly List<CartSlotView> _cartSlots = new();
        readonly List<IDisposable> _subs = new();
        IDisposable _gate;
        bool _visible;

        void Awake()
        {
            if (purchaseButton != null) purchaseButton.onClick.AddListener(OnPurchaseClicked);
            SetVisible(false); // 부팅 숨김(프리팹 CanvasGroup alpha0과 정합)
        }

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<ShowShopCommand>(_ => Show()));
            _subs.Add(EventBus.Subscribe<ShopPurchasedEvent>(OnPurchased));
            _subs.Add(EventBus.Subscribe<ShopRejectedEvent>(OnRejected));
            _subs.Add(EventBus.Subscribe<MoneyChangedEvent>(_ => RefreshBalance()));
        }

        void OnDisable()
        {
            foreach (var s in _subs) s.Dispose();
            _subs.Clear();
            _gate?.Dispose(); // 표시 중 비활성 시 게이트 누수 방지(중복 무해)
            _gate = null;
            _visible = false;
        }

        /// <summary>상점 열기 — 판매 그리드 재구성(해금 갱신 반영) + 장바구니 초기화.</summary>
        public void Show()
        {
            BuildSaleSlots();
            ClearCart();
            RefreshBalance();
            SetVisible(true);
        }

        void BuildSaleSlots()
        {
            foreach (var s in _saleSlots)
                if (s != null) Destroy(s.gameObject);
            _saleSlots.Clear();
            if (saleSlotPrefab == null || saleContainer == null) return;

            foreach (var item in ItemDatabase.GetAvailable(ItemAvailability.Always))
            {
                var slot = Instantiate(saleSlotPrefab, saleContainer);
                slot.Bind(item, OnSaleSlotClicked, OnSaleSlotHover);
                _saleSlots.Add(slot);
            }
            if (saleFooter != null) saleFooter.SetAsLastSibling(); // 푸터는 항상 그리드 끝
        }

        // 클릭 = 장바구니 토글(기획서: 선택 시 체크, 한 번 더 누르면 취소).
        void OnSaleSlotClicked(ItemData item)
        {
            if (item == null) return;
            if (_cart.ContainsKey(item.Id)) RemoveFromCart(item.Id);
            else
            {
                _cart[item.Id] = 1;
                _cartOrder.Add(item.Id);
            }
            RefreshCart();
        }

        void OnSaleSlotHover(SaleSlotView slot, bool enter)
        {
            if (tooltip == null) return;
            if (enter) tooltip.Show(slot.Item, slot.transform);
            else tooltip.Hide();
        }

        void OnQtyUp(string itemId)
        {
            if (!_cart.TryGetValue(itemId, out int qty)) return;
            _cart[itemId] = CartMath.Increment(qty);
            RefreshCart();
        }

        void OnQtyDown(string itemId)
        {
            if (!_cart.TryGetValue(itemId, out int qty)) return;
            int next = CartMath.Decrement(qty);
            if (next <= 0) RemoveFromCart(itemId); // 0 = 항목 제거(판매 슬롯 체크도 해제)
            else _cart[itemId] = next;
            RefreshCart();
        }

        void RemoveFromCart(string itemId)
        {
            _cart.Remove(itemId);
            _cartOrder.Remove(itemId);
        }

        void ClearCart()
        {
            _cart.Clear();
            _cartOrder.Clear();
            RefreshCart();
        }

        /// <summary>장바구니 칸 재구성 + 합산/버튼/판매 슬롯 체크 동기화(항목 수 적어 전체 재구성으로 충분).</summary>
        void RefreshCart()
        {
            foreach (var s in _cartSlots)
                if (s != null) Destroy(s.gameObject);
            _cartSlots.Clear();

            if (cartSlotPrefab != null && cartContainer != null)
            {
                foreach (string id in _cartOrder)
                {
                    var item = ItemDatabase.Get(id);
                    if (item == null) continue;
                    var slot = Instantiate(cartSlotPrefab, cartContainer);
                    slot.Bind(item, _cart[id], OnQtyUp, OnQtyDown);
                    _cartSlots.Add(slot);
                }
            }

            long total = CartMath.Total(_cart, id => ItemDatabase.Get(id)?.Price ?? -1);
            if (totalText != null) totalText.text = MoneyFormat.Currency(total);
            if (purchaseButton != null) purchaseButton.interactable = _cart.Count > 0;

            foreach (var s in _saleSlots)
                if (s != null && s.Item != null) s.SetSelected(_cart.ContainsKey(s.Item.Id));
        }

        void RefreshBalance()
        {
            if (balanceText != null && state != null) balanceText.text = MoneyFormat.Currency(state.Money);
        }

        // 구매하기 → 확인 모달(기획서 p38) → "네"일 때만 구매 명령 발행(검증·차감은 ShopService).
        void OnPurchaseClicked()
        {
            if (_cart.Count == 0) return;
            EventBus.Publish(new ShowModalCommand(null, confirmMessage,
                new[]
                {
                    new ModalButton(confirmNo, ModalButtonKind.No),   // index 0 (좌)
                    new ModalButton(confirmYes, ModalButtonKind.Yes), // index 1 (우)
                },
                new ModalRequest(i =>
                {
                    if (i == 1) EventBus.Publish(new PurchaseRequestedCommand(new Dictionary<string, int>(_cart)));
                })));
        }

        void OnPurchased(ShopPurchasedEvent _)
        {
            if (!_visible) return; // 상점이 닫혀 있으면 다른 발행원 — 무시
            ClearCart();
            ShowNotice(purchasedMessage); // 잔액은 MoneyChangedEvent 구독이 갱신
        }

        void OnRejected(ShopRejectedEvent e)
        {
            if (!_visible) return;
            // 기획서 문구는 자산 부족 케이스 — 그 외(빈 장바구니/미존재)는 UI가 선차단하므로 동일 문구로 충분.
            ShowNotice(insufficientMessage);
        }

        void ShowNotice(string message) => EventBus.Publish(new ShowModalCommand(
            null, message,
            new[] { new ModalButton(closeLabel, ModalButtonKind.Close) },
            new ModalRequest()));

        void SetVisible(bool v)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
            if (v)
            {
                // 재표시 포함 항상 시각·논리 최상단 동기화(SettingsView 미러) — 뒤로가기 CloseTop 대상 보장.
                transform.SetAsLastSibling();
                _gate?.Dispose();
                _gate = OverlayGate.Push(() => SetVisible(false));
                _visible = true;
            }
            else if (_visible)
            {
                _gate?.Dispose();
                _gate = null;
                _visible = false;
                if (tooltip != null) tooltip.Hide();
            }
        }
    }
}
