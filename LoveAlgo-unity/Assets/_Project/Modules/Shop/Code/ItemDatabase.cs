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
        /// <summary>SO가 없을 때 사용하는 하드코딩 기본 데이터 (리스트CSV No.1~43 순서)</summary>
        static void RegisterDefaults()
        {
            var defaultItems = new[]
            {
                // ══════════════════════════════════════
                //  선물 — 하예은 (No.1, 6, 11)
                // ══════════════════════════════════════
                new ItemData("gift_ankle_guard", "발목 보호대", "발목을 보호해 주는 보호대", 30000,
                    ItemCategory.Gift, iconPath: "1_gift_ankle_guard", targetHeroine: "HaYeEun", loveEffect2: 2, loveEffect3: 3),
                new ItemData("gift_meal_coupon", "[한식명인 최명자의 쌈밥명가] 식사권", "유명한 쌈밥 맛집의 식사권", 25000,
                    ItemCategory.Gift, iconPath: "6_gift_meal_coupon", targetHeroine: "HaYeEun", loveEffect2: 2, loveEffect3: 3),
                new ItemData("gift_game_chip", "<점프점프 거북맨> 게임 칩", "예은과 어릴 때 함께 플레이했던 게임 칩", 30000,
                    ItemCategory.Gift, iconPath: "11_gift_game_chip", targetHeroine: "HaYeEun", loveEffect2: 2, loveEffect3: 3),

                // ══════════════════════════════════════
                //  선물 — 서다은 (No.2, 7, 12)
                // ══════════════════════════════════════
                new ItemData("gift_album", "<트로피컬 글로우> 앨범", "세계적 인기 밴드 <트로피컬 글로우> 앨범", 30000,
                    ItemCategory.Gift, iconPath: "2_gift_album", targetHeroine: "SeoDaEun", loveEffect2: 2, loveEffect3: 3),
                new ItemData("gift_bread", "알밤달밤 밤식빵", "학교 근처 유명 베이커리 [밤밤밤]의 밤식빵", 8000,
                    ItemCategory.Gift, iconPath: "7_gift_bread", targetHeroine: "SeoDaEun", loveEffect2: 1, loveEffect3: 2),
                new ItemData("gift_concert_ticket", "<트로피컬 글로우> 내한 콘서트 티켓", "<트로피컬 글로우> 내한 콘서트 티켓", 100000,
                    ItemCategory.Gift, iconPath: "12_gift_concert_ticket", targetHeroine: "SeoDaEun", loveEffect2: 3, loveEffect3: 5),

                // ══════════════════════════════════════
                //  선물 — 이봄 (No.3, 8, 13)
                // ══════════════════════════════════════
                new ItemData("gift_rabbit_keyring", "토끼 인형 키링", "미니 토끼 인형 키링", 20000,
                    ItemCategory.Gift, iconPath: "3_gift_rabbit_keyring", targetHeroine: "LeeBom", loveEffect2: 2, loveEffect3: 3),
                new ItemData("gift_strawberry_milk", "[내 맘에 딸기가득] 딸기우유", "귀여운 패키지의 딸기우유", 3000,
                    ItemCategory.Gift, iconPath: "8_gift_strawberry_milk", targetHeroine: "LeeBom", loveEffect2: 1, loveEffect3: 2),
                new ItemData("gift_popup_ticket", "<러블리톡> 팝업스토어 입장 확정 티켓", "<러블리톡> 팝업스토어 입장 확정 티켓", 60000,
                    ItemCategory.Gift, iconPath: "13_gift_popup_ticket", targetHeroine: "LeeBom", loveEffect2: 3, loveEffect3: 4),

                // ══════════════════════════════════════
                //  선물 — 도희원 (No.4, 9, 14)
                // ══════════════════════════════════════
                new ItemData("gift_soju", "한정판 소주 <여름, 레몬, 밤>", "소주 브랜드에서 새로 나온 과일 소주", 40000,
                    ItemCategory.Gift, iconPath: "4_gift_soju", targetHeroine: "DoHeewon", loveEffect2: 3, loveEffect3: 4),
                new ItemData("gift_diamond", "엠파이어 다이아몬드", "고급 위스키", 120000,
                    ItemCategory.Gift, iconPath: "9_gift_diamond", targetHeroine: "DoHeewon", loveEffect2: 3, loveEffect3: 5),
                new ItemData("gift_ribbon", "리본 머리끈", "흰색 실크 리본 머리끈", 35000,
                    ItemCategory.Gift, iconPath: "14_gift_ribbon", targetHeroine: "DoHeewon", loveEffect2: 2, loveEffect3: 3),

                // ══════════════════════════════════════
                //  선물 — 로아 (No.5, 10, 15)
                // ══════════════════════════════════════
                new ItemData("gift_blue_shirt", "하늘색 반팔 티셔츠", "검은색이 아닌 옷", 40000,
                    ItemCategory.Gift, iconPath: "5_gift_blue_shirt", targetHeroine: "Roa", loveEffect2: 3, loveEffect3: 4),
                new ItemData("gift_monitor", "QHD 모니터", "화질이 매우 좋은 모니터", 120000,
                    ItemCategory.Gift, iconPath: "10_gift_monitor", targetHeroine: "Roa", loveEffect2: 3, loveEffect3: 5),
                new ItemData("gift_headset", "생동감이 느껴지는 헤드셋", "로아의 목소리를 더 잘 들을 수 있는 노이즈 캔슬링 헤드셋", 90000,
                    ItemCategory.Gift, iconPath: "15_gift_headset", targetHeroine: "Roa", loveEffect2: 3, loveEffect3: 5),

                // ══════════════════════════════════════
                //  피로 회복 소모품 (No.16, 17, 18, 34, 41)
                // ══════════════════════════════════════
                new ItemData("consume_energy_drink", "에너지 음료", "고카페인 각성 음료", 3000,
                    ItemCategory.Consumable, effectValue: 5, iconPath: "16_consume_energy_drink"),
                new ItemData("consume_vitamin", "종합비타민", "힘이 나는 피로 회복 비타민", 12000,
                    ItemCategory.Consumable, effectValue: 8, iconPath: "17_consume_vitamin"),
                new ItemData("consume_arginine", "아르기닌", "남성건강 영양제", 20000,
                    ItemCategory.Consumable, effectValue: 12, iconPath: "18_consume_arginine"),
                new ItemData("consume_mood_lamp", "수면용 무드등", "부드러운 빛이 나는 무드등", 25000,
                    ItemCategory.Consumable, effectValue: 15, iconPath: "34_consume_mood_lamp"),
                new ItemData("consume_energy_bar", "에너지바", "견과류가 든 에너지바", 5000,
                    ItemCategory.Consumable, effectValue: 6, iconPath: "41_consume_energy_bar"),

                // ══════════════════════════════════════
                //  세션 버프 — 체력 (No.19, 20)
                // ══════════════════════════════════════
                new ItemData("buff_protein_choco", "프로틴_초코맛", "단백질 충전에 필요한 초코맛 프로틴", 10000,
                    ItemCategory.SessionBuff, effectValue: 2, iconPath: "19_buff_protein_choco", effectStat: "Str", duplicateTag: "buff_str"),
                new ItemData("buff_protein_straw", "프로틴_딸기맛", "단백질 충전에 필요한 딸기맛 프로틴", 12000,
                    ItemCategory.SessionBuff, effectValue: 3, iconPath: "20_buff_protein_straw", effectStat: "Str", duplicateTag: "buff_str"),

                // ══════════════════════════════════════
                //  세션 버프 — 지성 (No.21, 22, 24, 25, 28, 30, 40)
                // ══════════════════════════════════════
                new ItemData("buff_keyboard", "기계식 키보드", "타건음이 좋은 기계식 키보드", 60000,
                    ItemCategory.SessionBuff, effectValue: 2, iconPath: "21_buff_keyboard", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_gaming_mouse", "게이밍 마우스", "게임 플레이에 도움이 되는 마우스", 35000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "22_buff_gaming_mouse", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_note", "노트", "필기에 사용하는 노트", 4000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "24_buff_note", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_pen", "볼펜", "필기에 사용하는 볼펜", 3000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "25_buff_pen", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_postit", "포스트잇", "공부할 때 사용하는 메모지", 6000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "28_buff_postit", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_highlighter", "형광펜 세트", "필기에 사용하는 형광펜 3색 세트", 8000,
                    ItemCategory.SessionBuff, effectValue: 2, iconPath: "30_buff_highlighter", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_sharp", "샤프", "필기에 사용하는 샤프", 5000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "40_buff_sharp", effectStat: "Int", duplicateTag: "buff_int"),

                // ══════════════════════════════════════
                //  세션 버프 — 끈기 (No.23, 39, 42, 43)
                // ══════════════════════════════════════
                new ItemData("buff_vertical_mouse", "버티컬 마우스", "손목 건강에 도움이 되는 마우스", 35000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "23_buff_vertical_mouse", effectStat: "Per", duplicateTag: "buff_per"),
                new ItemData("buff_blanket", "무릎담요", "적당한 사이즈의 폭신한 담요", 8000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "39_buff_blanket", effectStat: "Per", duplicateTag: "buff_per",
                    subEffectStat: "Fatigue", subEffectValue: -2),
                new ItemData("buff_coffee", "아이스 아메리카노", "시원한 아메리카노 한 잔", 4000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "42_buff_coffee", effectStat: "Per", duplicateTag: "buff_per"),
                new ItemData("buff_handcream", "핸드크림", "좋은 향이 나는 핸드크림", 6000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "43_buff_handcream", effectStat: "Per", duplicateTag: "buff_per"),

                // ══════════════════════════════════════
                //  세션 버프 — 사교성 (No.26, 27, 29, 31, 32, 33, 35, 36, 37, 38)
                // ══════════════════════════════════════
                new ItemData("buff_tumbler", "텀블러", "음료를 담아 다닐 수 있는 텀블러", 7000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "26_buff_tumbler", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_battery", "보조배터리", "어디서나 충전할 수 있는 보조배터리", 10000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "27_buff_battery", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_diary", "다이어리", "하루를 기록하는 일기장", 12000,
                    ItemCategory.SessionBuff, effectValue: 2, iconPath: "29_buff_diary", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_laptop_stand", "노트북 거치대", "노트북을 올려둘 수 있는 거치대", 15000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "31_buff_laptop_stand", effectStat: "Soc", duplicateTag: "buff_soc",
                    subEffectStat: "Int", subEffectValue: 1),
                new ItemData("buff_bluelight_glasses", "블루라이트 차단 안경", "눈에 좋지 않은 청광을 차단하는 안경", 18000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "32_buff_bluelight_glasses", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_umbrella", "휴대용 우산", "가방에 쏙 들어가는 작은 우산", 9000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "33_buff_umbrella", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_chocolate", "<마일드 스위트> 초콜릿", "달콤한 밀크초콜릿", 8000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "35_buff_chocolate", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_jelly", "<몰랑말랑 후르츠> 젤리", "과일 맛의 하트 모양 젤리", 8000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "36_buff_jelly", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_plant", "미니 화분 다육이", "작은 다육식물 화분", 7000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "37_buff_plant", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_calendar", "탁상형 달력", "책상 위에 올려놓는 달력", 5000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "38_buff_calendar", effectStat: "Soc", duplicateTag: "buff_soc"),
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
