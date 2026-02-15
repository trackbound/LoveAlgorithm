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
    /// 상점 팝업 (ModalPopupBase)
    /// 
    /// ShopUI 프리팹에 연결:
    ///   - 왼쪽: 판매 목록 (ShopSaleSlot)
    ///   - 오른쪽: 장바구니 (ShopCartSlot)
    ///   - 하단: 합계/구매 버튼
    /// </summary>
    public class ShopPopup : ModalPopupBase
    {
        [Header("상점 UI")]
        [SerializeField] TMP_Text moneyText;
        [SerializeField] Transform saleContainer;
        [SerializeField] Transform cartContainer;
        [SerializeField] TMP_Text totalPriceText;
        [SerializeField] Button purchaseButton;
        [SerializeField] Button backButton;

        [Header("프리팹")]
        [SerializeField] ShopSaleSlot saleSlotPrefab;
        [SerializeField] ShopCartSlot cartSlotPrefab;

        /// <summary>장바구니: itemId → 수량</summary>
        readonly Dictionary<string, int> cart = new();

        /// <summary>활성화된 판매 슬롯 목록</summary>
        readonly List<ShopSaleSlot> activeSaleSlots = new();

        /// <summary>활성화된 장바구니 슬롯 목록</summary>
        readonly List<ShopCartSlot> activeCartSlots = new();

        protected override void Awake()
        {
            base.Awake();

            if (purchaseButton != null)
                purchaseButton.onClick.AddListener(OnPurchaseClick);

            if (backButton != null)
                backButton.onClick.AddListener(Close);
        }

        public override void Show()
        {
            cart.Clear();
            PopulateSaleList();
            RefreshCart();
            RefreshMoneyDisplay();
            base.Show();
        }

        #region 판매 목록

        /// <summary>판매 목록 생성</summary>
        void PopulateSaleList()
        {
            // 기존 슬롯 제거
            foreach (var slot in activeSaleSlots)
            {
                if (slot != null) Destroy(slot.gameObject);
            }
            activeSaleSlots.Clear();

            if (saleSlotPrefab == null || saleContainer == null) return;

            var items = ItemDatabase.GetAll();
            foreach (var item in items)
            {
                var slot = Instantiate(saleSlotPrefab, saleContainer);
                slot.Setup(item, OnAddToCart);
                activeSaleSlots.Add(slot);
            }
        }

        #endregion

        #region 장바구니

        /// <summary>장바구니에 추가</summary>
        void OnAddToCart(ItemData item)
        {
            if (!cart.ContainsKey(item.Id))
                cart[item.Id] = 0;

            cart[item.Id]++;

            // 합산이 소지금 초과하면 롤백
            if (GetCartTotal() > (GameState.Instance?.Money ?? 0))
            {
                cart[item.Id]--;
                if (cart[item.Id] <= 0) cart.Remove(item.Id);
                PopupManager.Instance?.Toast("소지금 부족", "소지금이 부족합니다.");
                return;
            }

            UISoundManager.Instance?.PlayClick();
            RefreshCart();
        }

        /// <summary>장바구니에서 수량 변경</summary>
        void OnCartQuantityChanged(string itemId, int newQty)
        {
            if (newQty <= 0)
                cart.Remove(itemId);
            else
                cart[itemId] = newQty;

            RefreshCart();
        }

        /// <summary>장바구니 UI 갱신</summary>
        void RefreshCart()
        {
            // 기존 슬롯 제거
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
                PopupManager.Instance?.Toast("소지금 부족", "소지금이 부족합니다.");
                return;
            }

            // 확인 팝업
            bool confirmed = await PopupManager.Instance.ConfirmAsync(
                $"총 {total:N0}원을 결제하시겠습니까?"
            );

            if (!confirmed) return;

            // 구매 실행
            gs.AddMoney(-total);
            foreach (var kv in cart)
            {
                ShopManager.AddItem(kv.Key, kv.Value);
                var item = ItemDatabase.Get(kv.Key);
                Debug.Log($"[ShopPopup] 구매: {item?.Name} x{kv.Value}");
            }

            cart.Clear();
            RefreshCart();
            RefreshMoneyDisplay();

            UISoundManager.Instance?.PlayClick();
            PopupManager.Instance?.Toast("구매 완료", "아이템을 구매했습니다!");
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
