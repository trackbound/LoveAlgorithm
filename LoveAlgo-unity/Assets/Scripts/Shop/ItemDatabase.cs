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
                // ══════════════════════════════════════
                //  선물 — 하예은
                // ══════════════════════════════════════
                new ItemData("gift_ankle_guard", "발목 보호대", "운동할 때 필수인 발목 보호대.\n예은이가 좋아할 것 같다.", 30000,
                    ItemCategory.Gift, iconPath: "gift_ankle_guard", targetHeroine: "Yeeun", loveEffect2: 2, loveEffect3: 3),
                new ItemData("gift_meal_coupon", "쌈밥 식사권", "동네 맛집 쌈밥 식사권.\n예은이랑 같이 가면 좋겠다.", 25000,
                    ItemCategory.Gift, iconPath: "gift_meal_coupon", targetHeroine: "Yeeun", loveEffect2: 2, loveEffect3: 3),
                new ItemData("gift_game_chip", "게임칩", "인기 게임의 칩 세트.\n예은이가 관심 있어할지도.", 30000,
                    ItemCategory.Gift, iconPath: "gift_game_chip", targetHeroine: "Yeeun", loveEffect2: 2, loveEffect3: 3),

                // ══════════════════════════════════════
                //  선물 — 서다은
                // ══════════════════════════════════════
                new ItemData("gift_album", "앨범", "좋아하는 아티스트의 앨범.\n다은이가 좋아할 것 같다.", 30000,
                    ItemCategory.Gift, iconPath: "gift_album", targetHeroine: "Daeun", loveEffect2: 2, loveEffect3: 3),
                new ItemData("gift_bread", "밤식빵", "밤이 가득 든 식빵.\n다은이의 취향 저격.", 8000,
                    ItemCategory.Gift, iconPath: "gift_bread", targetHeroine: "Daeun", loveEffect2: 1, loveEffect3: 2),
                new ItemData("gift_concert_ticket", "콘서트 티켓", "인기 가수의 콘서트 티켓.\n다은이가 정말 좋아할 거다.", 100000,
                    ItemCategory.Gift, iconPath: "gift_concert_ticket", targetHeroine: "Daeun", loveEffect2: 3, loveEffect3: 5),

                // ══════════════════════════════════════
                //  선물 — 이봄
                // ══════════════════════════════════════
                new ItemData("gift_rabbit_keyring", "토끼 키링", "귀여운 토끼 열쇠고리.\n봄이가 좋아할 것 같다.", 20000,
                    ItemCategory.Gift, iconPath: "gift_rabbit_keyring", targetHeroine: "Bom", loveEffect2: 2, loveEffect3: 3),
                new ItemData("gift_strawberry_milk", "딸기우유", "딸기맛 우유.\n봄이의 최애 음료.", 3000,
                    ItemCategory.Gift, iconPath: "gift_strawberry_milk", targetHeroine: "Bom", loveEffect2: 1, loveEffect3: 2),
                new ItemData("gift_popup_ticket", "팝업스토어 티켓", "인기 팝업스토어 입장권.\n봄이랑 같이 가면 좋겠다.", 60000,
                    ItemCategory.Gift, iconPath: "gift_popup_ticket", targetHeroine: "Bom", loveEffect2: 3, loveEffect3: 4),

                // ══════════════════════════════════════
                //  선물 — 도희원
                // ══════════════════════════════════════
                new ItemData("gift_soju", "한정판 소주", "한정판 프리미엄 소주.\n희원이가 좋아할 것 같다.", 40000,
                    ItemCategory.Gift, iconPath: "gift_soju", targetHeroine: "Heewon", loveEffect2: 3, loveEffect3: 4),
                new ItemData("gift_diamond", "다이아몬드", "작지만 빛나는 다이아몬드.\n희원이가 정말 좋아할 거다.", 120000,
                    ItemCategory.Gift, iconPath: "gift_diamond", targetHeroine: "Heewon", loveEffect2: 3, loveEffect3: 5),
                new ItemData("gift_ribbon", "리본 머리끈", "예쁜 리본 모양 머리끈.\n희원이한테 어울릴 것 같다.", 35000,
                    ItemCategory.Gift, iconPath: "gift_ribbon", targetHeroine: "Heewon", loveEffect2: 2, loveEffect3: 3),

                // ══════════════════════════════════════
                //  선물 — 로아
                // ══════════════════════════════════════
                new ItemData("gift_blue_shirt", "하늘색 티셔츠", "하늘색 포인트 티셔츠.\n로아가 좋아할 것 같다.", 40000,
                    ItemCategory.Gift, iconPath: "gift_blue_shirt", targetHeroine: "Roa", loveEffect2: 3, loveEffect3: 4),
                new ItemData("gift_monitor", "QHD 모니터", "고해상도 QHD 모니터.\n로아가 정말 좋아할 거다.", 120000,
                    ItemCategory.Gift, iconPath: "gift_monitor", targetHeroine: "Roa", loveEffect2: 3, loveEffect3: 5),
                new ItemData("gift_headset", "헤드셋", "고급 게이밍 헤드셋.\n로아가 정말 좋아할 거다.", 90000,
                    ItemCategory.Gift, iconPath: "gift_headset", targetHeroine: "Roa", loveEffect2: 3, loveEffect3: 5),

                // ══════════════════════════════════════
                //  피로 회복 소모품
                // ══════════════════════════════════════
                new ItemData("consume_energy_drink", "에너지 음료", "졸릴 때 마시면 좋은 에너지 음료.\n피로가 조금 줄어든다.", 3000,
                    ItemCategory.Consumable, effectValue: 5, iconPath: "consume_energy_drink"),
                new ItemData("consume_energy_bar", "에너지바", "간편하게 먹을 수 있는 에너지바.\n피로를 적당히 줄여준다.", 5000,
                    ItemCategory.Consumable, effectValue: 6, iconPath: "consume_energy_bar"),
                new ItemData("consume_vitamin", "종합비타민", "종합비타민 한 알로 컨디션 UP.\n피로 회복에 효과적이다.", 12000,
                    ItemCategory.Consumable, effectValue: 8, iconPath: "consume_vitamin"),
                new ItemData("consume_arginine", "아르기닌", "고함량 아르기닌 보충제.\n피로가 크게 줄어든다.", 20000,
                    ItemCategory.Consumable, effectValue: 12, iconPath: "consume_arginine"),
                new ItemData("consume_mood_lamp", "수면용 무드등", "은은한 수면용 무드등.\n피로를 확실히 풀어준다.", 25000,
                    ItemCategory.Consumable, effectValue: 15, iconPath: "consume_mood_lamp"),

                // ══════════════════════════════════════
                //  세션 버프 — 체력
                // ══════════════════════════════════════
                new ItemData("buff_protein_choco", "프로틴 초코맛", "운동 전에 먹으면 효과적인 초코 프로틴.\n자유행동 1회 체력 +2", 10000,
                    ItemCategory.SessionBuff, effectValue: 2, iconPath: "buff_protein_choco", effectStat: "Str", duplicateTag: "buff_str"),
                new ItemData("buff_protein_straw", "프로틴 딸기맛", "딸기맛 프로틴. 맛도 좋고 효과도 좋다.\n자유행동 1회 체력 +3", 12000,
                    ItemCategory.SessionBuff, effectValue: 3, iconPath: "buff_protein_straw", effectStat: "Str", duplicateTag: "buff_str"),

                // ══════════════════════════════════════
                //  세션 버프 — 지성
                // ══════════════════════════════════════
                new ItemData("buff_note", "노트", "깔끔한 줄 노트. 공부 효율이 오른다.\n자유행동 1회 지성 +1", 4000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_note", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_pen", "볼펜", "필기감 좋은 볼펜.\n자유행동 1회 지성 +1", 3000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_pen", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_postit", "포스트잇", "형형색색 포스트잇. 정리에 딱이다.\n자유행동 1회 지성 +1", 6000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_postit", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_highlighter", "형광펜 세트", "5색 형광펜 세트. 중요한 건 표시!\n자유행동 1회 지성 +2", 8000,
                    ItemCategory.SessionBuff, effectValue: 2, iconPath: "buff_highlighter", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_sharp", "샤프", "0.3mm 정밀 샤프. 필기감이 좋다.\n자유행동 1회 지성 +1", 5000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_sharp", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_keyboard", "기계식 키보드", "청축 기계식 키보드. 타건감 최고.\n자유행동 1회 지성 +2", 60000,
                    ItemCategory.SessionBuff, effectValue: 2, iconPath: "buff_keyboard", effectStat: "Int", duplicateTag: "buff_int"),
                new ItemData("buff_gaming_mouse", "게이밍 마우스", "인체공학 게이밍 마우스.\n자유행동 1회 지성 +1", 35000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_gaming_mouse", effectStat: "Int", duplicateTag: "buff_int"),

                // ══════════════════════════════════════
                //  세션 버프 — 끈기
                // ══════════════════════════════════════
                new ItemData("buff_vertical_mouse", "버티컬 마우스", "손목이 편한 버티컬 마우스.\n자유행동 1회 끈기 +1", 35000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_vertical_mouse", effectStat: "Per", duplicateTag: "buff_per"),
                new ItemData("buff_coffee", "아이스 아메리카노", "시원한 아이스 아메리카노.\n자유행동 1회 끈기 +1", 4000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_coffee", effectStat: "Per", duplicateTag: "buff_per"),
                new ItemData("buff_blanket", "무릎담요", "따뜻한 무릎담요. 집중력이 올라간다.\n자유행동 1회 끈기 +1, 피로 -2", 8000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_blanket", effectStat: "Per", duplicateTag: "buff_per"),
                new ItemData("buff_handcream", "핸드크림", "보습 핸드크림. 손이 편해진다.\n자유행동 1회 끈기 +1", 6000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_handcream", effectStat: "Per", duplicateTag: "buff_per"),

                // ══════════════════════════════════════
                //  세션 버프 — 사교성
                // ══════════════════════════════════════
                new ItemData("buff_tumbler", "텀블러", "감성 텀블러. 대화의 시작.\n자유행동 1회 사교성 +1", 7000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_tumbler", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_battery", "보조배터리", "든든한 보조배터리.\n자유행동 1회 사교성 +1", 10000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_battery", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_diary", "다이어리", "예쁜 다이어리. 일정 관리에 딱이다.\n자유행동 1회 사교성 +2", 12000,
                    ItemCategory.SessionBuff, effectValue: 2, iconPath: "buff_diary", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_laptop_stand", "노트북 거치대", "인체공학 노트북 거치대.\n자유행동 1회 사교성 +1, 지성 +1", 15000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_laptop_stand", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_bluelight_glasses", "블루라이트 차단 안경", "눈이 편해지는 블루라이트 차단 안경.\n자유행동 1회 사교성 +1", 18000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_bluelight_glasses", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_umbrella", "휴대용 우산", "갑자기 비가 와도 걱정 없다.\n자유행동 1회 사교성 +1", 9000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_umbrella", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_plant", "미니 화분", "작은 다육식물 화분.\n자유행동 1회 사교성 +1", 7000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_plant", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_calendar", "탁상 달력", "귀여운 탁상 달력.\n자유행동 1회 사교성 +1", 5000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_calendar", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_chocolate", "초콜릿", "달콤한 초콜릿.\n자유행동 1회 사교성 +1", 8000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_chocolate", effectStat: "Soc", duplicateTag: "buff_soc"),
                new ItemData("buff_jelly", "젤리", "쫀득한 젤리.\n자유행동 1회 사교성 +1", 8000,
                    ItemCategory.SessionBuff, effectValue: 1, iconPath: "buff_jelly", effectStat: "Soc", duplicateTag: "buff_soc"),
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
