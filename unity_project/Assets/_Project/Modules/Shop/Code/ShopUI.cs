using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using LoveAlgo.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.Story;
using LoveAlgo.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 패널 (UIManager 관리 — ScheduleUI와 크로스페이드 스왑)
    ///
    /// 레이아웃:
    ///   - 좌측: 잔액 + 장바구니(CART) + 합계 + 구매하기 버튼
    ///   - 우측: 아이템 그리드 (카드형, 스크롤)
    ///   - 호버 시: 아이템 설명 팝업 (ShopItemTooltip)
    ///   - 클릭: 토글 선택/해제 (체크마크)
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class ShopUI : MonoBehaviour
    {
        CanvasGroup _canvasGroup;
        public CanvasGroup CanvasGroup => _canvasGroup != null ? _canvasGroup : (_canvasGroup = GetComponent<CanvasGroup>());

        [Header("상점 UI")]
        [SerializeField] TMP_Text moneyText;
        [SerializeField] Transform saleContainer;
        [SerializeField] Transform cartContainer;
        [SerializeField] TMP_Text totalPriceText;
        [SerializeField] Button purchaseButton;

        [Header("카테고리 필터")]
        [SerializeField] TabGroup categoryTabs;

        [Header("호버 설명 팝업")]
        [SerializeField] ShopItemTooltip itemDetailPopup;
        [Tooltip("Detail 팝업 위치 계산 기준 (그리드 ScrollRect의 Viewport)")]
        [SerializeField] RectTransform saleViewport;

        [Header("프리팹")]
        [SerializeField] ShopSaleSlot saleSlotPrefab;
        [SerializeField] ShopCartSlot cartSlotPrefab;

        [Header("스크롤 끝 푸터")]
        [Tooltip("판매 그리드 스크롤을 끝까지 내렸을 때 보이는 'COMING SOON' 등 푸터 GameObject.\n" +
                 "saleContainer 또는 그 부모(Content)의 자식으로 배치해 두면 됨. " +
                 "ShopUI은 항상 마지막 자식으로 정렬하고 활성 상태로 유지함.")]
        [SerializeField] GameObject saleListFooter;

        [Header("아이템 필터 (테스트 빌드용)")]
        [Tooltip("설정 시 체크된 아이템만 상점에 노출. 미설정 시 전체 표시.\n" +
                 "비워두면 ShopUI 프리합 루트/자식에서 자동 탐색됨.")]
        [SerializeField] ShopItemFilter itemFilter;

        /// <summary>장바구니: itemId → 수량</summary>
        readonly Dictionary<string, int> cart = new();

        /// <summary>활성화된 판매 슬롯 목록</summary>
        readonly List<ShopSaleSlot> activeSaleSlots = new();

        /// <summary>활성화된 장바구니 슬롯 목록</summary>
        readonly List<ShopCartSlot> activeCartSlots = new();

        /// <summary>itemId → 슬롯 역참조 (토글 해제 시 사용)</summary>
        readonly Dictionary<string, ShopSaleSlot> slotMap = new();

        /// <summary>현재 카테고리 필터 (null = 전체)</summary>
        ItemCategory? activeFilter;

        /// <summary>탭 인덱스 → 카테고리 매핑 (0=전체, 1=소모품, 2=세션버프)</summary>
        static readonly ItemCategory?[] TabCategories = { null, ItemCategory.Gift, ItemCategory.Consumable, ItemCategory.SessionBuff };

        readonly ListenerBag _listeners = new();

        void Awake()
        {
            _listeners.Bind(purchaseButton, OnPurchaseClick);

            if (categoryTabs != null)
                categoryTabs.OnTabChanged += OnCategoryTabChanged;

            // 인스펙터에서 미바인딩 시 자기 자신/자식에서 필터 자동 탐색
            // (ShopUI 프리합 루트에 ShopItemFilter를 컴포넌트로 붙이면 완료)
            if (itemFilter == null)
                itemFilter = GetComponentInChildren<ShopItemFilter>(true);

            EnsureSaleSlotPool();
        }

        void OnDestroy()
        {
            _listeners.Dispose();
            if (categoryTabs != null) categoryTabs.OnTabChanged -= OnCategoryTabChanged;
        }

        void OnEnable()
        {
            if (GameState.Instance != null)
                GameState.Instance.OnChanged += OnGameStateChanged;
        }

        void OnDisable()
        {
            if (GameState.Instance != null)
                GameState.Instance.OnChanged -= OnGameStateChanged;
        }

        /// <summary>GameState 변경 시 머니 실시간 갱신</summary>
        void OnGameStateChanged()
        {
            if (!gameObject.activeInHierarchy) return;
            RefreshMoneyDisplay();
        }

        /// <summary>패널 열릴 때 초기화 (ScheduleUI가 호출)</summary>
        public void Open()
        {
            cart.Clear();
            activeFilter = null;
            categoryTabs?.Select(0, notify: false);
            PopulateSaleList();
            RefreshCart();
            RefreshMoneyDisplay();
            HideItemDetail();
        }

        /// <summary>카테고리 탭 변경 콜백</summary>
        void OnCategoryTabChanged(int index)
        {
            activeFilter = (index >= 0 && index < TabCategories.Length) ? TabCategories[index] : null;
            PopulateSaleList();
        }

        #region 판매 목록 (그리드)

        /// <summary>판매 목록 생성 (카테고리 필터 적용)</summary>
        void PopulateSaleList()
        {
            slotMap.Clear();

            if (saleSlotPrefab == null || saleContainer == null) return;

            var allItems = ItemDatabase.GetAll();

            // 카테고리 필터 + 아이템 노출 필터 적용
            var items = new List<ItemData>();
            foreach (var item in allItems)
            {
                if (activeFilter != null && item.Category != activeFilter.Value)
                    continue;
                if (itemFilter != null && !itemFilter.IsItemEnabled(item.Id))
                    continue;
                items.Add(item);
            }

            EnsureSaleSlotPool(items.Count);

            for (int i = 0; i < activeSaleSlots.Count; i++)
            {
                var slot = activeSaleSlots[i];
                if (slot == null) continue;

                bool shouldShow = i < items.Count;
                slot.gameObject.SetActive(shouldShow);
                if (!shouldShow) continue;

                var item = items[i];
                slot.Setup(item, OnSlotClicked, OnSlotHovered);
                slotMap[item.Id] = slot;

                // 장바구니에 이미 담긴 아이템이면 선택 상태 복원
                if (cart.ContainsKey(item.Id))
                    slot.SetSelected(true);
            }

            // 푸터(COMING SOON 등)를 항상 마지막 자식으로 유지하고 활성화
            UpdateSaleListFooter();
        }

        /// <summary>판매 그리드 끝에 표시되는 푸터를 항상 마지막 자식으로 정렬 + 활성화</summary>
        void UpdateSaleListFooter()
        {
            if (saleListFooter == null) return;
            saleListFooter.transform.SetAsLastSibling();
            if (!saleListFooter.activeSelf)
                saleListFooter.SetActive(true);
        }

        void EnsureSaleSlotPool(int requiredCount = -1)
        {
            if (saleSlotPrefab == null || saleContainer == null) return;

            if (requiredCount < 0)
                requiredCount = ItemDatabase.GetAll().Count;

            while (activeSaleSlots.Count < requiredCount)
            {
                var slot = Instantiate(saleSlotPrefab, saleContainer);
                slot.gameObject.SetActive(false);
                activeSaleSlots.Add(slot);
            }
        }

        #endregion

        #region 클릭 → 장바구니 토글

        /// <summary>슬롯 클릭 콜백 — 토글: 장바구니에 없으면 추가, 있으면 제거</summary>
        void OnSlotClicked(ShopSaleSlot slot)
        {
            if (slot.Item == null) return;

            var id = slot.Item.Id;

            if (cart.ContainsKey(id))
            {
                // 이미 담겨 있으면 제거
                cart.Remove(id);
                slot.SetSelected(false);
            }
            else
            {
                // 새로 담기 (수량 1)
                cart[id] = 1;
                slot.SetSelected(true);
            }

            UISoundManager.Instance?.PlayClick();
            RefreshCart();
        }

        #endregion

        #region 호버 설명 팝업

        /// <summary>슬롯 호버 콜백</summary>
        void OnSlotHovered(ShopSaleSlot slot, bool entered)
        {
            if (entered && slot.Item != null)
            {
                ShowItemDetail(slot);
            }
            else
            {
                HideItemDetail();
            }
        }

        void ShowItemDetail(ShopSaleSlot slot)
        {
            if (itemDetailPopup == null) return;
            var slotRect = slot.GetComponent<RectTransform>();
            var vpRect = ResolveSaleViewport();
            itemDetailPopup.Show(slot.Item, slotRect, vpRect);
        }

        void HideItemDetail()
        {
            if (itemDetailPopup != null)
                itemDetailPopup.Hide();
        }

        RectTransform ResolveSaleViewport()
        {
            if (saleViewport != null)
                return saleViewport;

            if (saleContainer == null)
                return null;

            var scrollRect = saleContainer.GetComponentInParent<ScrollRect>();
            if (scrollRect != null && scrollRect.viewport != null)
            {
                saleViewport = scrollRect.viewport;
                return saleViewport;
            }

            saleViewport = saleContainer.parent as RectTransform;
            return saleViewport;
        }

        #endregion

        #region 장바구니

        /// <summary>장바구니에서 수량 변경</summary>
        void OnCartQuantityChanged(string itemId, int newQty)
        {
            if (newQty <= 0)
            {
                cart.Remove(itemId);
                // 그리드 슬롯 체크마크도 해제
                if (slotMap.TryGetValue(itemId, out var slot))
                    slot.SetSelected(false);
            }
            else
            {
                cart[itemId] = newQty;
            }

            RefreshCart();
        }

        /// <summary>장바구니 UI 갱신</summary>
        void RefreshCart()
        {
            if (cartSlotPrefab == null || cartContainer == null) return;

            EnsureCartSlotPool(cart.Count);

            int index = 0;
            foreach (var kv in cart)
            {
                var item = ItemDatabase.Get(kv.Key);
                if (item == null) continue;

                var slot = activeCartSlots[index++];
                slot.gameObject.SetActive(true);
                slot.Setup(item, kv.Value, OnCartQuantityChanged);
            }

            for (int i = index; i < activeCartSlots.Count; i++)
            {
                if (activeCartSlots[i] != null)
                    activeCartSlots[i].gameObject.SetActive(false);
            }

            // 합계 갱신
            int total = GetCartTotal();
            if (totalPriceText != null)
                totalPriceText.text = MoneyFormat.Currency(total);

            // 구매 버튼 활성/비활성 — 장바구니에 아이템이 있으면 항상 활성
            if (purchaseButton != null)
                purchaseButton.interactable = cart.Count > 0;
        }

        /// <summary>장바구니 합계</summary>
        int GetCartTotal()
        {
            int total = 0;
            foreach (var kv in cart)
            {
                var item = ItemDatabase.Get(kv.Key);
                if (item != null)
                    total += item.Price * kv.Value;
            }
            return total;
        }

        void EnsureCartSlotPool(int requiredCount)
        {
            if (cartSlotPrefab == null || cartContainer == null) return;

            while (activeCartSlots.Count < requiredCount)
            {
                var slot = Instantiate(cartSlotPrefab, cartContainer);
                slot.gameObject.SetActive(false);
                activeCartSlots.Add(slot);
            }
        }

        #endregion

        #region 구매

        void OnPurchaseClick()
        {
            OnPurchaseAsync().Forget();
        }

        async UniTaskVoid OnPurchaseAsync()
        {
            if (cart.Count == 0) return;

            // 확인 팝업: "구매하시겠습니까?"
            bool confirmed = await PopupManager.Instance.ConfirmAsync(
                "구매하시겠습니까?",
                "네", "아니오"
            );

            if (!confirmed) return;

            // 일괄 구매 + 즉시 효과 적용 (원자적 트랜잭션)
            //   기획: "구매 = 즉시 적용" — 선물(Gift) 제외하고 Consumable/SessionBuff는 구매 시점에 바로 소진·적용
            //     · Consumable(피로회복): 피로 즉시 감소 (중복 패널티 그대로 적용, 인벤토리에 남지 않음)
            //     · SessionBuff(스탯 증가): 주/보조 효과 즉시 AddStat (중복 패널티 적용, 인벤토리 X)
            //     · Gift(선물): 인벤토리에 적재, 2차/3차 이벤트에서 사용
            int currentDay = GameManager.Instance != null ? GameManager.Instance.CurrentDay : 0;
            if (!ShopManager.BuyBatchAndApply(cart, currentDay, out var feedbackParts))
            {
                PopupManager.Instance?.Toast("구매 실패", "소지금이 부족합니다!");
                return;
            }

            // 상태 초기화
            cart.Clear();
            foreach (var slot in activeSaleSlots)
                slot.SetSelected(false);
            RefreshCart();
            RefreshMoneyDisplay();

            UISoundManager.Instance?.PlayClick();

            // 효과 피드백 토스트 — 아이템별로 "이름\n효과들" 블록을 차례로 표시
            //   예) "오렌지사탕\n체력 +1 지성 +2" → "딸기사탕\n체력 +2 지성 +1"
            PopupManager.Instance?.ToastSequence("구매 완료", feedbackParts, 1.2f);
        }

        #endregion

        /// <summary>소지금 표시 갱신</summary>
        void RefreshMoneyDisplay()
        {
            if (moneyText != null)
            {
                var gs = GameState.Instance;
                moneyText.text = gs != null ? MoneyFormat.Currency(gs.Money) : MoneyFormat.Currency(0);
            }
        }
    }
}
