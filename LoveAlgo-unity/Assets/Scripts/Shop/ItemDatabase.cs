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

        /// <summary>필요 시 자동 초기화</summary>
        static void EnsureInit()
        {
            if (initialized) return;
            initialized = true;

            // SO 로드 시도
            var catalog = Resources.Load<ItemCatalogSO>("Data/ItemCatalog");
            if (catalog != null && catalog.Items.Count > 0)
            {
                foreach (var item in catalog.Items)
                    items[item.Id] = item;
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

        /// <summary>특정 히로인의 선물 목록</summary>
        public static IEnumerable<ItemData> GetGiftsForHeroine(string heroineId)
        {
            EnsureInit();
            return items.Values.Where(i =>
                i.Category == ItemCategory.Gift && i.TargetHeroine == heroineId);
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
                // ── 히로인 선물 — 하예은 ──
                new ItemData("gift_yeun_towel", "스포츠 타월", "하예은이 좋아할 스포츠용 타월.\n\"운동할 때 쓸게!\"", 8000, ItemCategory.Gift, "Yeun", iconPath: "Items/turtleman", tier: GiftTier.Low),
                new ItemData("gift_yeun_shoes", "러닝화", "가볍고 튼튼한 러닝화.\n\"와 이거 진짜 좋다!\"", 45000, ItemCategory.Gift, "Yeun", iconPath: "Items/ankle support", tier: GiftTier.High, availability: ItemAvailability.AfterEvent2Start),
                new ItemData("gift_yeun_watch", "스포츠 시계", "방수 기능이 있는 스포츠 시계.\n\"매일 차고 다닐게!\"", 85000, ItemCategory.Gift, "Yeun", iconPath: "Items/dining ticket", tier: GiftTier.Premium, availability: ItemAvailability.AfterEvent3Start),
                // ── 히로인 선물 — 서다은 ──
                new ItemData("gift_daeun_pen", "고급 볼펜", "서다은이 좋아할 만년필.\n\"...괜찮은 선물이네.\"", 10000, ItemCategory.Gift, "Daeun", iconPath: "Items/3clolor pen", tier: GiftTier.Low),
                new ItemData("gift_daeun_book", "전공 서적", "희귀한 전공 원서.\n\"이거... 구하기 어려운 건데.\"", 35000, ItemCategory.Gift, "Daeun", iconPath: "Items/plant", tier: GiftTier.Mid, availability: ItemAvailability.AfterEvent2Start),
                new ItemData("gift_daeun_tablet", "태블릿 펜", "고급 디지털 필기 펜.\n\"필기감이 정말 좋아.\"", 80000, ItemCategory.Gift, "Daeun", iconPath: "Items/monitor", tier: GiftTier.Premium, availability: ItemAvailability.AfterEvent3Start),
                // ── 히로인 선물 — 이봄 ──
                new ItemData("gift_bom_sticker", "스티커 팩", "이봄이 좋아할 캐릭터 스티커.\n\"이거 진짜 귀여워~!\"", 5000, ItemCategory.Gift, "Bom", iconPath: "Items/rabbit_keyring", tier: GiftTier.Low),
                new ItemData("gift_bom_acc", "헤어 액세서리", "트렌디한 머리핀 세트.\n\"오늘부터 바로 해볼래!\"", 25000, ItemCategory.Gift, "Bom", iconPath: "Items/hairband", tier: GiftTier.Mid, availability: ItemAvailability.AfterEvent2Start),
                new ItemData("gift_bom_bag", "미니 크로스백", "봄이 스타일에 딱 맞는 가방.\n\"대박... 이거 완전 내 스타일!\"", 65000, ItemCategory.Gift, "Bom", iconPath: "Items/handcream", tier: GiftTier.High, availability: ItemAvailability.AfterEvent3Start),
                // ── 히로인 선물 — 도희원 ──
                new ItemData("gift_heewon_novel", "문고본 소설", "도희원이 좋아할 소설책.\n\"...읽어볼게.\"", 8000, ItemCategory.Gift, "Heewon", iconPath: "Items/popup ticket", tier: GiftTier.Low),
                new ItemData("gift_heewon_tea", "프리미엄 티 세트", "희귀한 블렌딩 차 세트.\n\"...향이 좋아.\"", 40000, ItemCategory.Gift, "Heewon", iconPath: "Items/empire diamond", tier: GiftTier.High, availability: ItemAvailability.AfterEvent2Start),
                new ItemData("gift_heewon_music", "한정판 LP", "클래식 한정판 레코드.\n\"이걸... 어떻게 구한 거야?\"", 90000, ItemCategory.Gift, "Heewon", iconPath: "Items/CD_tropical glow", tier: GiftTier.Premium, availability: ItemAvailability.AfterEvent3Start),
                // ── 히로인 선물 — 로아 ──
                new ItemData("gift_roa_light", "LED 조명", "로아가 좋아할 방송용 조명.\n\"방송 퀄리티 업! 고마워!\"", 10000, ItemCategory.Gift, "Roa", iconPath: "Items/lamp", tier: GiftTier.Low),
                new ItemData("gift_roa_mic", "콘덴서 마이크", "방송용 고급 마이크.\n\"소리 진짜 깨끗하다!\"", 50000, ItemCategory.Gift, "Roa", iconPath: "Items/headphone", tier: GiftTier.High, availability: ItemAvailability.AfterEvent2Start),
                new ItemData("gift_roa_cam", "웹캠 프로", "4K 방송용 웹캠.\n\"이거면 화질 미쳤다!\"", 95000, ItemCategory.Gift, "Roa", iconPath: "Items/monitor", tier: GiftTier.Premium, availability: ItemAvailability.AfterEvent3Start),
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
                items[item.Id] = item;
        }
        #endregion
    }
}
