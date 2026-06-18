using System;
using System.Collections.Generic;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 장바구니 수량/합산 순수 계산(EventBus/Unity 무관 — EditMode 테스트 대상, ADR-007 순수층).
    /// 수량 상한 99는 기획서(내부 콘텐츠 §상점 "구매 수량 99개 제한") 동결값.
    /// 합산은 "현재 가진 돈과 별개로 담김"(기획서) — 자금 게이트는 구매 시점에 ShopService가 판정.
    /// </summary>
    public static class CartMath
    {
        /// <summary>장바구니 한 항목의 최대 수량(기획서 동결).</summary>
        public const int MaxQuantity = 99;

        /// <summary>수량 +1(상한 99 클램프).</summary>
        public static int Increment(int qty) => Math.Min(qty + 1, MaxQuantity);

        /// <summary>수량 -1(하한 0 — 0은 "항목 제거" 신호로 호출부가 해석).</summary>
        public static int Decrement(int qty) => Math.Max(qty - 1, 0);

        /// <summary>장바구니 총액(가격×수량 합, long — 수량 99×고가 아이템 오버플로 방지). 수량≤0/미지정 가격(음수)은 무시.</summary>
        public static long Total(IReadOnlyDictionary<string, int> cart, Func<string, int> priceOf)
        {
            if (cart == null || priceOf == null) return 0;
            long total = 0;
            foreach (var kv in cart)
            {
                if (kv.Value <= 0) continue;
                int price = priceOf(kv.Key);
                if (price < 0) continue;
                total += (long)price * kv.Value;
            }
            return total;
        }
    }
}
