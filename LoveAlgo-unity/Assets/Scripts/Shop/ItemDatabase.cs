using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 데이터베이스
    /// SO(ItemCatalogSO)에서 데이터를 로드하며, SO가 없으면 하드코딩 폴백 사용
    /// </summary>
    public static class ItemDatabase
    {
        static readonly Dictionary<string, ItemData> items = new();
        static List<ItemData> cachedAll;
        static bool initialized;
        static readonly Dictionary<string, Sprite> spriteCache = new();

        /// <summary>필요 시 자동 초기화</summary>
        static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;

            // 아이콘 폴백 스프라이트 로드
            if (ItemData.FallbackIcon == null)
                ItemData.FallbackIcon = Resources.Load<Sprite>("Items/fallback");

            // SO 로드 시도
            var catalog = Resources.Load<ItemCatalogSO>("Data/ItemCatalog");
            if (catalog != null && catalog.Items.Count > 0)
            {
                foreach (var item in catalog.Items)
                {
                    ResolveIconReferences(item);
                    items[item.Id] = item;
                }
            }
            else
            {
                // SO가 없으면 하드코딩 폴백
                RegisterDefaults();
            }
        }

        /// <summary>강제 리로드 (에디터/런타임 전환 시)</summary>
        public static void Reload()
        {
            items.Clear();
            cachedAll = null;
            spriteCache.Clear();
            initialized = false;
            EnsureInit();
        }

        /// <summary>ID로 아이템 조회</summary>
        public static ItemData Get(string itemId)
        {
            EnsureInit();
            return items.GetValueOrDefault(itemId);
        }

        /// <summary>전체 아이템 목록</summary>
        public static IReadOnlyList<ItemData> GetAll()
        {
            EnsureInit();
            cachedAll ??= new List<ItemData>(items.Values);
            return cachedAll;
        }

        /// <summary>카테고리별 아이템 목록</summary>
        public static IEnumerable<ItemData> GetByCategory(ItemCategory category)
        {
            EnsureInit();
            return items.Values.Where(i => i.Category == category);
        }

        /// <summary>현재 해금된 아이템만 반환</summary>
        public static IEnumerable<ItemData> GetAvailable(ItemAvailability maxPhase)
        {
            EnsureInit();
            return items.Values.Where(i => i.Availability <= maxPhase);
        }

        /// <summary>구매 가능 여부 (소지금 확인)</summary>
        public static bool CanAfford(string itemId, int currentMoney)
        {
            var item = Get(itemId);
            return item != null && currentMoney >= item.Price;
        }

        #region Hardcoded Fallback
        /// <summary>SO가 없을 때 사용하는 하드코딩 기본 데이터</summary>
        static void RegisterDefaults()
        {
            var defaultItems = new[]
            {
                // ── 피로 회복 소모품 ──
                new ItemData("consume_energy_drink", "에너지 음료", "졸릴 때 마시면 좋은 에너지 음료.\n피로가 조금 줄어든다.", 3000, ItemCategory.Consumable, effectValue: 5, iconPath: "Items/energybar"),
                new ItemData("consume_energy_bar", "에너지바", "간편하게 먹을 수 있는 에너지바.\n피로를 적당히 줄여준다.", 5000, ItemCategory.Consumable, effectValue: 6, iconPath: "Items/energybar"),
                new ItemData("consume_vitamin", "비타민 음료", "비타민이 풍부한 건강 음료.\n피로 회복에 효과적이다.", 8000, ItemCategory.Consumable, effectValue: 8, iconPath: "Items/vitamin"),
                new ItemData("consume_arginine", "아르기닌 젤리", "고함량 아르기닌 젤리.\n피로가 크게 줄어든다.", 15000, ItemCategory.Consumable, effectValue: 12, iconPath: "Items/jelly"),
                new ItemData("consume_mood_lamp", "무드등 (아로마)", "아로마 무드등. 은은한 향으로\n피로를 확실히 풀어준다.", 25000, ItemCategory.Consumable, effectValue: 15, iconPath: "Items/lamp"),
                // ── 세션 버프 — 체력 ──
                new ItemData("buff_protein_choco", "프로틴 초코바", "운동 전에 먹으면 효과적인 초코바.\n자유행동 1회 체력 효과 +2", 8000, ItemCategory.SessionBuff, effectValue: 2, iconPath: "Items/marron bread", effectStat: "Str", duplicateTag: "buff_str"),
                new ItemData("buff_protein_straw", "프로틴 딸기바", "딸기맛 프로틴 바. 맛도 좋고 효과도 좋다.\n자유행동 1회 체력 효과 +3", 12000, ItemCategory.SessionBuff, effectValue: 3, iconPath: "Items/marron bread", effectStat: "Str", duplicateTag: "buff_str"),
                // ── 세션 버프 — 지성 ──
                new ItemData("buff_note", "정리 노트", "깔끔한 줄 노트. 공부 효율이 오른다.\n자유행동 1회 지성 효과 +1", 3000, ItemCategory.SessionBuff, effectValue: 1, iconPath: "Items/popup ticket", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_pen", "3색 볼펜", "색깔별 필기가 가능한 볼펜.\n자유행동 1회 지성 효과 +1", 3000, ItemCategory.SessionBuff, effectValue: 1, iconPath: "Items/3clolor pen", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_postit", "포스트잇 세트", "형형색색 포스트잇. 정리에 딱이다.\n자유행동 1회 지성 효과 +1", 4000, ItemCategory.SessionBuff, effectValue: 1, effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_highlighter", "형광펜 세트", "5색 형광펜 세트. 중요한 건 표시!\n자유행동 1회 지성 효과 +2", 6000, ItemCategory.SessionBuff, effectValue: 2, iconPath: "Items/3clolor pen", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_mech_pencil", "고급 샤프", "0.3mm 정밀 샤프. 필기감이 좋다.\n자유행동 1회 지성 효과 +1", 5000, ItemCategory.SessionBuff, effectValue: 1, iconPath: "Items/3clolor pen", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_keyboard", "기계식 키보드", "청축 기계식 키보드. 타건감 최고.\n자유행동 1회 지성 효과 +2", 35000, ItemCategory.SessionBuff, effectValue: 2, effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_mouse", "게이밍 마우스", "인체공학 게이밍 마우스.\n자유행동 1회 지성 효과 +1", 25000, ItemCategory.SessionBuff, effectValue: 1, effectStat: "Int", duplicateTag: "buff_int"),
                // ── 세션 버프 — 끈기 ──
                new ItemData("buff_vertical_mouse", "버티컬 마우스", "손목이 편한 버티컬 마우스.\n자유행동 1회 끈기 효과 +1", 20000, ItemCategory.SessionBuff, effectValue: 1, effectStat: "Per", duplicateTag: "buff_per"),
            };

            foreach (var item in defaultItems)
            {
                ResolveIconReferences(item);
                items[item.Id] = item;
            }
        }

        static void ResolveIconReferences(ItemData item)
        {
            if (item == null) return;

            // IconSprite와 DetailSprite는 SO에서 직접 바인딩됨 (Art/Item/Icon, Art/Item)
            // Resources 폴백용 로드 (개발 중 임시)
            if (item.IconSprite == null && !string.IsNullOrEmpty(item.IconPath))
                item.IconSprite = LoadSpriteCached(item.IconPath);
        }

        static Sprite LoadSpriteCached(string resourcePath)
        {
            if (string.IsNullOrEmpty(resourcePath))
                return null;

            if (spriteCache.TryGetValue(resourcePath, out var cached))
                return cached;

            var sprite = Resources.Load<Sprite>(resourcePath);
            if (sprite != null)
                spriteCache[resourcePath] = sprite;
            return sprite;
        }
        #endregion
    }
}
