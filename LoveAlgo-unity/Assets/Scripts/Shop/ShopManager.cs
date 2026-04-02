using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Story;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점/인벤토리 관리자 (정적 클래스)
    /// 
    /// 기능:
    ///   - 아이템 구매 (소지금 차감)
    ///   - 인벤토리 관리 (소지 아이템 목록)
    ///   - 소모품 사용 (피로 회복 등)
    /// </summary>
    public static class ShopManager
    {
        /// <summary>
        /// 인벤토리: itemId → 수량
        /// </summary>
        static readonly Dictionary<string, int> inventory = new();

        static ShopManager()
        {
            Reset();
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload() => Reset();

        /// <summary>초기화 (새 게임 시)</summary>
        public static void Reset()
        {
            inventory.Clear();
        }

        #region 구매

        /// <summary>
        /// 아이템 구매
        /// </summary>
        /// <returns>성공 여부</returns>
        public static bool Buy(string itemId, int quantity = 1)
        {
            var item = ItemDatabase.Get(itemId);
            if (item == null)
            {
                Debug.LogWarning($"[ShopManager] 아이템 없음: {itemId}");
                return false;
            }

            var gs = GameState.Instance;
            if (gs == null) return false;

            int totalCost = item.Price * quantity;
            if (gs.Money < totalCost)
            {
                Debug.Log($"[ShopManager] 소지금 부족: {MoneyFormat.Currency(gs.Money)} < {MoneyFormat.Currency(totalCost)}");
                return false;
            }

            gs.AddMoney(-totalCost);
            AddItem(itemId, quantity);
            Debug.Log($"[ShopManager] 구매 완료: {item.Name} x{quantity} ({MoneyFormat.SignedCurrency(-totalCost)})");
            return true;
        }

        /// <summary>
        /// 장바구니 일괄 구매 (원자적 — 전체 성공 또는 전체 실패)
        /// </summary>
        /// <returns>성공 여부</returns>
        public static bool BuyBatch(IReadOnlyDictionary<string, int> items)
        {
            var gs = GameState.Instance;
            if (gs == null) return false;

            // 사전 검증: 모든 아이템 존재 + 총액 계산
            int total = 0;
            foreach (var kv in items)
            {
                var item = ItemDatabase.Get(kv.Key);
                if (item == null)
                {
                    Debug.LogWarning($"[ShopManager] BuyBatch 아이템 없음: {kv.Key}");
                    return false;
                }
                total += item.Price * kv.Value;
            }

            if (gs.Money < total)
            {
                Debug.Log($"[ShopManager] BuyBatch 소지금 부족: {MoneyFormat.Currency(gs.Money)} < {MoneyFormat.Currency(total)}");
                return false;
            }

            // 일괄 차감 + 아이템 추가
            gs.AddMoney(-total);
            foreach (var kv in items)
                AddItem(kv.Key, kv.Value);

            Debug.Log($"[ShopManager] BuyBatch 완료: {items.Count}종 ({MoneyFormat.SignedCurrency(-total)})");
            return true;
        }

        #endregion

        #region 인벤토리

        /// <summary>아이템 추가</summary>
        public static void AddItem(string itemId, int quantity = 1)
        {
            if (!inventory.ContainsKey(itemId))
                inventory[itemId] = 0;
            inventory[itemId] += quantity;
        }

        /// <summary>아이템 제거</summary>
        public static bool RemoveItem(string itemId, int quantity = 1)
        {
            if (!inventory.ContainsKey(itemId) || inventory[itemId] < quantity)
                return false;

            inventory[itemId] -= quantity;
            if (inventory[itemId] <= 0)
                inventory.Remove(itemId);
            return true;
        }

        /// <summary>아이템 수량 조회</summary>
        public static int GetItemCount(string itemId)
        {
            return inventory.GetValueOrDefault(itemId);
        }

        /// <summary>인벤토리 전체 (수량 > 0)</summary>
        public static IReadOnlyDictionary<string, int> GetInventory()
        {
            return inventory;
        }

        /// <summary>인벤토리가 비어있는지</summary>
        public static bool IsInventoryEmpty()
        {
            return inventory.Count == 0 || inventory.Values.All(v => v <= 0);
        }

        #endregion

        #region 소모품

        /// <summary>
        /// 소모품 사용 (기획서: 동일날 중복 시 50% 패널티)
        /// </summary>
        /// <param name="itemId">소모품 아이템 ID</param>
        /// <param name="currentDay">현재 날짜 (중복 패널티 판정용)</param>
        /// <returns>성공 여부</returns>
        public static bool UseConsumable(string itemId, int currentDay = -1)
        {
            var item = ItemDatabase.Get(itemId);
            if (item == null || item.Category != ItemCategory.Consumable)
            {
                Debug.LogWarning($"[ShopManager] 소모품이 아님: {itemId}");
                return false;
            }

            if (!RemoveItem(itemId))
            {
                Debug.LogWarning($"[ShopManager] 인벤토리에 없음: {itemId}");
                return false;
            }

            var gs = GameState.Instance;
            if (gs == null) return false;

            // 동일날 중복 사용 시 50% 패널티 적용
            int effect = item.EffectValue;
            if (currentDay >= 0)
                effect = ItemEffectSystem.ApplyDuplicatePenalty(item.GetDuplicateTag(), effect, currentDay);

            gs.AddStat("Fatigue", -effect);
            Debug.Log($"[ShopManager] 소모품 사용: {item.Name} (피로 -{effect}, 원본 -{item.EffectValue})");
            return true;
        }

        #endregion

        #region 세션 버프

        /// <summary>
        /// 세션 버프 아이템 사용 (기획서: 자유행동 1회 스탯 보정)
        /// </summary>
        /// <param name="itemId">세션 버프 아이템 ID</param>
        /// <param name="currentDay">현재 날짜 (중복 패널티 판정용)</param>
        /// <returns>적용될 실제 버프값 (0이면 실패)</returns>
        public static int UseSessionBuff(string itemId, int currentDay)
        {
            var item = ItemDatabase.Get(itemId);
            if (item == null || item.Category != ItemCategory.SessionBuff)
            {
                Debug.LogWarning($"[ShopManager] 세션 버프 아이템이 아님: {itemId}");
                return 0;
            }

            if (GetItemCount(itemId) <= 0)
            {
                Debug.LogWarning($"[ShopManager] 인벤토리에 없음: {itemId}");
                return 0;
            }

            if (!RemoveItem(itemId))
                return 0;

            int buffValue = ItemEffectSystem.ActivateSessionBuff(item, currentDay);
            Debug.Log($"[ShopManager] 세션 버프 사용: {item.Name} ({item.EffectStat} +{buffValue})");
            return buffValue;
        }

        #endregion

        #region Save / Load

        /// <summary>세이브용 데이터 추출</summary>
        public static ShopSaveData GetSaveData()
        {
            return new ShopSaveData
            {
                Inventory = new Dictionary<string, int>(inventory),
                EffectData = ItemEffectSystem.GetSaveData()
            };
        }

        /// <summary>로드 시 복원</summary>
        public static void RestoreFromSave(ShopSaveData data)
        {
            Reset();
            if (data == null) return;

            if (data.Inventory != null)
            {
                foreach (var kv in data.Inventory)
                    inventory[kv.Key] = kv.Value;
            }

            ItemEffectSystem.RestoreFromSave(data.EffectData);
        }

        #endregion
    }

    /// <summary>
    /// 상점 세이브 데이터
    /// </summary>
    [Serializable]
    public class ShopSaveData
    {
        /// <summary>인벤토리: itemId → 수량</summary>
        public Dictionary<string, int> Inventory = new();

        /// <summary>아이템 효과 시스템 데이터 (세션 버프, 중복 추적)</summary>
        public ItemEffectSaveData EffectData;
    }
}
