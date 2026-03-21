using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Story;
using LoveAlgo.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점 패널 (ScheduleUI 내부 크로스페이드 패널)
    ///
    /// 레이아웃:
    ///   - 좌측: 잔액 + 장바구니(CART) + 합계 + 구매하기 버튼
    ///   - 우측: 아이템 그리드 (카드형, 스크롤)
    ///   - 호버 시: 아이템 설명 팝업 (ShopItemDetailPopup)
    ///   - 클릭: 토글 선택/해제 (체크마크)
    /// </summary>
    public class ShopPopup : MonoBehaviour
    {
        [Header("상점 UI")]
        [SerializeField] TMP_Text moneyText;
        [SerializeField] Transform saleContainer;
        [SerializeField] Transform cartContainer;
        [SerializeField] TMP_Text totalPriceText;
        [SerializeField] Button purchaseButton;
        [SerializeField] Button backButton;

        [Header("호버 설명 팝업")]
        [SerializeField] ShopItemDetailPopup itemDetailPopup;

        [Header("프리팹")]
        [SerializeField] ShopSaleSlot saleSlotPrefab;
        [SerializeField] ShopCartSlot cartSlotPrefab;

        /// <summary>뒤로가기 콜백 (ScheduleUI가 등록)</summary>
        public event Action OnBackRequested;

        /// <summary>장바구니: itemId → 수량</summary>
        readonly Dictionary<string, int> cart = new();

        /// <summary>활성화된 판매 슬롯 목록</summary>
        readonly List<ShopSaleSlot> activeSaleSlots = new();

        /// <summary>활성화된 장바구니 슬롯 목록</summary>
        readonly List<ShopCartSlot> activeCartSlots = new();

        /// <summary>itemId → 슬롯 역참조 (토글 해제 시 사용)</summary>
        readonly Dictionary<string, ShopSaleSlot> slotMap = new();

        void Awake()
        {
            if (purchaseButton != null)
                purchaseButton.onClick.AddListener(OnPurchaseClick);

            if (backButton != null)
                backButton.onClick.AddListener(OnBackClick);
        }

        /// <summary>패널 열릴 때 초기화 (ScheduleUI가 호출)</summary>
        public void Open()
        {
            cart.Clear();
            PopulateSaleList();
            RefreshCart();
            RefreshMoneyDisplay();
            HideItemDetail();
        }

        void OnBackClick()
        {
            OnBackRequested?.Invoke();
        }

        #region 판매 목록 (그리드)

        /// <summary>판매 목록 생성 (GridLayoutGroup으로 배치)</summary>
        void PopulateSaleList()
        {
            foreach (var slot in activeSaleSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            activeSaleSlots.Clear();
            slotMap.Clear();

            if (saleSlotPrefab == null || saleContainer == null) return;

            var items = ItemDatabase.GetAll();
            foreach (var item in items)
            {
                var slot = Instantiate(saleSlotPrefab, saleContainer);
                slot.Setup(item, OnSlotToggled, OnSlotHovered);
                activeSaleSlots.Add(slot);
                slotMap[item.Id] = slot;
            }
        }

        #endregion

        #region 토글 선택

        /// <summary>슬롯 토글 콜백 — 선택/해제 시 장바구니 반영</summary>
        void OnSlotToggled(ShopSaleSlot slot, bool selected)
        {
            if (slot.Item == null) return;

            if (selected)
            {
                // 장바구니에 1개 추가
                cart[slot.Item.Id] = 1;
            }
            else
            {
                // 장바구니에서 제거
                cart.Remove(slot.Item.Id);
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
            var gridRect = saleContainer as RectTransform;
            itemDetailPopup.Show(slot.Item, slotRect, gridRect);
        }

        void HideItemDetail()
        {
            if (itemDetailPopup != null)
                itemDetailPopup.Hide();
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
            foreach (var slot in activeCartSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            activeCartSlots.Clear();

            if (cartSlotPrefab == null || cartContainer == null) return;

            foreach (var kv in cart)
            {
                var item = ItemDatabase.Get(kv.Key);
                if (item == null) continue;

                var slot = Instantiate(cartSlotPrefab, cartContainer);
                slot.Setup(item, kv.Value, OnCartQuantityChanged);
                activeCartSlots.Add(slot);
            }

            // 합계 갱신
            int total = GetCartTotal();
            if (totalPriceText != null)
                totalPriceText.text = $"{total:N0}원";

            // 구매 버튼 활성/비활성
            if (purchaseButton != null)
                purchaseButton.interactable = cart.Count > 0 && total <= (GameState.Instance?.Money ?? 0);
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

        #endregion

        #region 구매

        void OnPurchaseClick()
        {
            OnPurchaseAsync().Forget();
        }

        async UniTaskVoid OnPurchaseAsync()
        {
            if (cart.Count == 0) return;

            int total = GetCartTotal();
            var gs = GameState.Instance;
            if (gs == null || gs.Money < total)
            {
                await (PopupManager.Instance?.AlertAsync("소지 금액을 초과하여 구매할 수 없어요!") ?? UniTask.CompletedTask);
                return;
            }

            // 확인 팝업: "구매하시겠습니까?"
            bool confirmed = await PopupManager.Instance.ConfirmAsync(
                "구매하시겠습니까?",
                "네", "아니오"
            );

            if (!confirmed) return;

            // 확인 후 소지금 재검증
            if (gs.Money < total)
            {
                await (PopupManager.Instance?.AlertAsync("소지 금액을 초과하여 구매할 수 없어요!") ?? UniTask.CompletedTask);
                return;
            }

            // 구매 실행 — ShopManager.Buy()에 소지금 차감 위임
            foreach (var kv in cart)
            {
                ShopManager.Buy(kv.Key, kv.Value);
            }

            // 상태 초기화
            cart.Clear();
            foreach (var slot in activeSaleSlots)
                slot.SetSelected(false);
            RefreshCart();
            RefreshMoneyDisplay();

            UISoundManager.Instance?.PlayClick();
            await (PopupManager.Instance?.AlertAsync("구매가 완료되었습니다!") ?? UniTask.CompletedTask);
        }

        #endregion

        /// <summary>소지금 표시 갱신</summary>
        void RefreshMoneyDisplay()
        {
            if (moneyText != null)
            {
                var gs = GameState.Instance;
                moneyText.text = gs != null ? $"{gs.Money:N0}원" : "0원";
            }
        }
    }
}
