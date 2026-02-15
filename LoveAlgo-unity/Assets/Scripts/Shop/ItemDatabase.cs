using System.Collections.Generic;
using System.Linq;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 데이터베이스 (정적 카탈로그)
    /// 
    /// 데모 기준:
    ///   - 범용 선물 3종 (+1~+3 포인트)
    ///   - 히로인 전용 선물 5종 (+4 포인트, 특정 히로인만)
    ///   - 소모품 2종 (피로 회복)
    /// </summary>
    public static class ItemDatabase
    {
        static readonly Dictionary<string, ItemData> items = new();

        static ItemDatabase()
        {
            Register(
                // ── 범용 선물 (누구에게나 줄 수 있음) ──
                new ItemData("gift_snack", "간식 세트", "편의점에서 고른 과자와 음료 세트.\n누구나 좋아한다.", 
                    5000, ItemCategory.Gift, effectValue: 1),
                new ItemData("gift_handcream", "핸드크림", "은은한 향이 나는 핸드크림.\n실용적인 선물.", 
                    15000, ItemCategory.Gift, effectValue: 2),
                new ItemData("gift_plush", "인형 키링", "귀여운 동물 키링.\n가방에 달기 좋다.", 
                    25000, ItemCategory.Gift, effectValue: 3),

                // ── 히로인 전용 선물 (특정 히로인에게 줄 때 +4) ──
                new ItemData("gift_yeun", "스포츠 타월", "하예은이 좋아할 스포츠용 타월.\n\"운동할 때 쓸게!\"",
                    30000, ItemCategory.Gift, "Yeun", 4),
                new ItemData("gift_daeun", "고급 볼펜", "서다은이 좋아할 만년필.\n\"...괜찮은 선물이네.\"",
                    30000, ItemCategory.Gift, "Daeun", 4),
                new ItemData("gift_bom", "스티커 팩", "이봄이 좋아할 캐릭터 스티커.\n\"이거 진짜 귀여워~!\"",
                    30000, ItemCategory.Gift, "Bom", 4),
                new ItemData("gift_heewon", "문고본 소설", "도희원이 좋아할 소설책.\n\"...읽어볼게.\"",
                    30000, ItemCategory.Gift, "Heewon", 4),
                new ItemData("gift_roa", "LED 조명", "로아가 좋아할 방송용 조명.\n\"방송 퀄리티 업! 고마워!\"",
                    30000, ItemCategory.Gift, "Roa", 4),

                // ── 소모품 ──
                new ItemData("consume_coffee", "캔 커피", "졸릴 때 마시면 좋은 캔 커피.\n피로가 조금 줄어든다.",
                    3000, ItemCategory.Consumable, effectValue: 10),
                new ItemData("consume_energydrink", "에너지 드링크", "기력 회복에 좋은 에너지 드링크.\n피로가 크게 줄어든다.",
                    8000, ItemCategory.Consumable, effectValue: 25)
            );
        }

        static void Register(params ItemData[] itemList)
        {
            foreach (var item in itemList)
                items[item.Id] = item;
        }

        /// <summary>ID로 아이템 조회</summary>
        public static ItemData Get(string itemId)
        {
            return items.GetValueOrDefault(itemId);
        }

        /// <summary>전체 아이템 목록</summary>
        public static IReadOnlyList<ItemData> GetAll()
        {
            return items.Values.ToList();
        }

        /// <summary>카테고리별 아이템 목록</summary>
        public static IReadOnlyList<ItemData> GetByCategory(ItemCategory category)
        {
            return items.Values.Where(i => i.Category == category).ToList();
        }

        /// <summary>구매 가능 여부 (소지금 확인)</summary>
        public static bool CanAfford(string itemId, int currentMoney)
        {
            var item = Get(itemId);
            return item != null && currentMoney >= item.Price;
        }
    }
}
