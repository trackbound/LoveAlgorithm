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
    ///   - 선물 주기 (히로인 포인트 적용)
    ///   - 소모품 사용 (피로 회복 등)
    /// </summary>
    public static class ShopManager
    {
        /// <summary>
        /// 인벤토리: itemId → 수량
        /// </summary>
        static readonly Dictionary<string, int> inventory = new();

        /// <summary>히로인별 선물 포인트 합계 (최대 8점 제한 확인용)</summary>
        static readonly Dictionary<string, int> giftPointsGiven = new();

        const int MaxGiftPoints = 8; // 기획서: 선물 합계 최대 +8점

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
            giftPointsGiven.Clear();
            foreach (var id in GameConstants.HeroineIds)
                giftPointsGiven[id] = 0;
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
                Debug.Log($"[ShopManager] 소지금 부족: {gs.Money} < {totalCost}");
                return false;
            }

            gs.AddMoney(-totalCost);
            AddItem(itemId, quantity);
            Debug.Log($"[ShopManager] 구매 완료: {item.Name} x{quantity} (-{totalCost:N0}원)");
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

        #region 선물

        /// <summary>
        /// 히로인에게 선물 주기
        /// </summary>
        /// <returns>실제 부여된 포인트 (0이면 실패)</returns>
        public static int GiveGift(string itemId, string heroineId)
        {
            var item = ItemDatabase.Get(itemId);
            if (item == null || item.Category != ItemCategory.Gift)
            {
                Debug.LogWarning($"[ShopManager] 선물 아이템이 아님: {itemId}");
                return 0;
            }

            if (GetItemCount(itemId) <= 0)
            {
                Debug.LogWarning($"[ShopManager] 인벤토리에 없음: {itemId}");
                return 0;
            }

            // 최대 선물 포인트 제한 — 아이템 소비 전에 확인
            int currentGiven = giftPointsGiven.GetValueOrDefault(heroineId);
            int remaining = MaxGiftPoints - currentGiven;
            if (remaining <= 0)
            {
                Debug.Log($"[ShopManager] {heroineId} 선물 포인트 최대치 도달 ({MaxGiftPoints})");
                return 0;
            }

            // 히로인 전용 선물을 다른 히로인에게 주면 효과 절반
            int points = item.EffectValue;
            if (item.IsHeroineSpecific && item.TargetHeroine != heroineId)
            {
                points = Mathf.Max(1, points / 2);
                Debug.Log($"[ShopManager] 전용 선물 불일치: {item.TargetHeroine}→{heroineId}, 효과 절반 ({points})");
            }

            // 아이템 소비 (포인트 제한 통과 후)
            if (!RemoveItem(itemId))
            {
                Debug.LogWarning($"[ShopManager] 아이템 제거 실패: {itemId}");
                return 0;
            }

            int actualPoints = Mathf.Min(points, remaining);
            giftPointsGiven[heroineId] = currentGiven + actualPoints;

            // HeroinePointTracker에 반영
            HeroinePointTracker.AddPoint(heroineId, PointCategory.Gift, actualPoints);
            Debug.Log($"[ShopManager] 선물 전달: {item.Name} → {heroineId} (+{actualPoints}점, 누적: {giftPointsGiven[heroineId]}/{MaxGiftPoints})");

            return actualPoints;
        }

        /// <summary>히로인별 남은 선물 포인트 여유</summary>
        public static int GetRemainingGiftPoints(string heroineId)
        {
            return MaxGiftPoints - giftPointsGiven.GetValueOrDefault(heroineId);
        }

        #endregion

        #region 소모품

        /// <summary>
        /// 소모품 사용
        /// </summary>
        /// <returns>성공 여부</returns>
        public static bool UseConsumable(string itemId)
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

            // 소모품 효과 적용 (현재: 피로 회복)
            gs.AddStat("Fatigue", -item.EffectValue);
            Debug.Log($"[ShopManager] 소모품 사용: {item.Name} (피로 -{item.EffectValue})");
            return true;
        }

        #endregion

        #region Save / Load

        /// <summary>세이브용 데이터 추출</summary>
        public static ShopSaveData GetSaveData()
        {
            return new ShopSaveData
            {
                Inventory = new Dictionary<string, int>(inventory),
                GiftPointsGiven = new Dictionary<string, int>(giftPointsGiven)
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

            if (data.GiftPointsGiven != null)
            {
                foreach (var kv in data.GiftPointsGiven)
                    giftPointsGiven[kv.Key] = kv.Value;
            }
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

        /// <summary>히로인별 선물 포인트 누적</summary>
        public Dictionary<string, int> GiftPointsGiven = new();
    }
}
