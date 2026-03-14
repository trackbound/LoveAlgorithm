using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 선물 주기 팝업 (ModalPopupBase)
    /// 
    /// 인벤토리에서 선물 아이템을 선택 → 히로인 선택 → 전달
    /// ScheduleUI의 자유행동에서 접근 (행동 소비 없음)
    /// </summary>
    public class GiftPopup : ModalPopupBase
    {
        [Header("선물 UI")]
        [SerializeField] Transform itemListContainer;
        [SerializeField] Transform heroineListContainer;
        [SerializeField] TMP_Text messageText;
        [SerializeField] Button backButton;

        [Header("프리팹")]
        [SerializeField] GiftItemSlot itemSlotPrefab;
        [SerializeField] GiftHeroineSlot heroineSlotPrefab;

        readonly List<GiftItemSlot> activeItemSlots = new();
        readonly List<GiftHeroineSlot> activeHeroineSlots = new();

        ItemData selectedItem;

        protected override void Awake()
        {
            base.Awake();

            if (backButton != null)
                backButton.onClick.AddListener(Close);
        }

        public override void Show()
        {
            selectedItem = null;
            PopulateGiftItems();
            ClearHeroineList();
            SetMessage("선물할 아이템을 선택하세요.");
            base.Show();
        }

        /// <summary>선물 아이템 목록 표시</summary>
        void PopulateGiftItems()
        {
            ClearItemList();

            if (itemSlotPrefab == null || itemListContainer == null) return;

            var inventory = ShopManager.GetInventory();
            foreach (var kv in inventory)
            {
                var item = ItemDatabase.Get(kv.Key);
                if (item == null || item.Category != ItemCategory.Gift) continue;
                if (kv.Value <= 0) continue;

                var slot = Instantiate(itemSlotPrefab, itemListContainer);
                slot.Setup(item, kv.Value, OnItemSelected);
                activeItemSlots.Add(slot);
            }

            if (activeItemSlots.Count == 0)
                SetMessage("선물할 아이템이 없습니다.\n상점에서 구매해주세요.");
        }

        /// <summary>아이템 선택 → 히로인 목록 표시</summary>
        void OnItemSelected(ItemData item)
        {
            selectedItem = item;
            UISoundManager.Instance?.PlayClick();
            PopulateHeroineList();
            SetMessage($"<b>{item.Name}</b>을(를) 누구에게 줄까요?");
        }

        /// <summary>히로인 목록 표시</summary>
        void PopulateHeroineList()
        {
            ClearHeroineList();

            if (heroineSlotPrefab == null || heroineListContainer == null) return;

            for (int i = 0; i < GameConstants.HeroineIds.Length; i++)
            {
                var slot = Instantiate(heroineSlotPrefab, heroineListContainer);
                string heroineId = GameConstants.HeroineIds[i];
                int remaining = ShopManager.GetRemainingGiftPoints(heroineId);
                slot.Setup(heroineId, GameConstants.HeroineNames[i], remaining, OnHeroineSelected);
                activeHeroineSlots.Add(slot);
            }
        }

        /// <summary>히로인 선택 → 선물 전달</summary>
        void OnHeroineSelected(string heroineId)
        {
            if (selectedItem == null) return;
            OnGiveGiftAsync(heroineId).Forget();
        }

        async UniTaskVoid OnGiveGiftAsync(string heroineId)
        {
            string heroineName = GetHeroineName(heroineId);

            bool confirmed = await PopupManager.Instance.ConfirmAsync(
                $"{heroineName}에게 {selectedItem.Name}을(를) 주시겠습니까?"
            );

            if (!confirmed) return;

            int points = ShopManager.GiveGift(selectedItem.Id, heroineId);

            if (points > 0)
            {
                UISoundManager.Instance?.PlayClick();
                PopupManager.Instance?.Toast("선물 완료", $"{heroineName}에게 선물을 전달했습니다! (+{points})");
            }
            else
            {
                int remaining = ShopManager.GetRemainingGiftPoints(heroineId);
                if (remaining <= 0)
                    PopupManager.Instance?.Toast("선물 한도", $"{heroineName}에게 줄 수 있는 선물 한도에 도달했습니다.");
                else
                    PopupManager.Instance?.Toast("실패", "선물 전달에 실패했습니다.");
            }

            // UI 갱신
            selectedItem = null;
            PopulateGiftItems();
            ClearHeroineList();
            SetMessage("선물할 아이템을 선택하세요.");
        }

        void SetMessage(string msg)
        {
            if (messageText != null) messageText.text = msg;
        }

        string GetHeroineName(string id)
        {
            return GameConstants.HeroineById.TryGetValue(id, out var config)
                ? config.DisplayName
                : id;
        }

        void ClearItemList()
        {
            foreach (var s in activeItemSlots)
                if (s != null) Destroy(s.gameObject);
            activeItemSlots.Clear();
        }

        void ClearHeroineList()
        {
            foreach (var s in activeHeroineSlots)
                if (s != null) Destroy(s.gameObject);
            activeHeroineSlots.Clear();
        }
    }

    /// <summary>
    /// 선물 아이템 슬롯 (간단 버전)
    /// </summary>
    public class GiftItemSlot : MonoBehaviour
    {
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text countText;
        [SerializeField] Button selectButton;

        ItemData itemData;
        Action<ItemData> onSelected;

        public void Setup(ItemData item, int count, Action<ItemData> onSelect)
        {
            itemData = item;
            onSelected = onSelect;

            if (nameText != null) nameText.text = item.Name;
            if (countText != null) countText.text = $"x{count}";

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onSelected?.Invoke(itemData));
            }
        }
    }

    /// <summary>
    /// 히로인 선택 슬롯 (간단 버전)
    /// </summary>
    public class GiftHeroineSlot : MonoBehaviour
    {
        [SerializeField] TMP_Text nameText;
        [SerializeField] TMP_Text remainingText;
        [SerializeField] Button selectButton;

        string heroineId;
        Action<string> onSelected;

        public void Setup(string id, string name, int remainingPoints, Action<string> onSelect)
        {
            heroineId = id;
            onSelected = onSelect;

            if (nameText != null) nameText.text = name;
            if (remainingText != null) remainingText.text = $"남은 선물 포인트: {remainingPoints}";

            if (selectButton != null)
            {
                selectButton.onClick.RemoveAllListeners();
                selectButton.onClick.AddListener(() => onSelected?.Invoke(heroineId));
                selectButton.interactable = remainingPoints > 0;
            }
        }
    }
}
