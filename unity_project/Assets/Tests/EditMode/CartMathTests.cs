using System.Collections.Generic;
using NUnit.Framework;
using LoveAlgo.Shop;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>장바구니 순수 계산(수량 99 클램프·0=제거 신호·long 합산) 단위테스트 — 기획서 동결값 가드.</summary>
    public class CartMathTests
    {
        [Test]
        public void Increment_ClampsAt99()
        {
            Assert.AreEqual(2, CartMath.Increment(1));
            Assert.AreEqual(99, CartMath.Increment(98));
            Assert.AreEqual(99, CartMath.Increment(99), "기획서: 구매 수량 99개 제한");
        }

        [Test]
        public void Decrement_FloorsAtZero_AsRemoveSignal()
        {
            Assert.AreEqual(1, CartMath.Decrement(2));
            Assert.AreEqual(0, CartMath.Decrement(1), "0 = 항목 제거 신호");
            Assert.AreEqual(0, CartMath.Decrement(0));
        }

        [Test]
        public void Total_SumsPriceTimesQty_IgnoresInvalid()
        {
            var cart = new Dictionary<string, int> { ["a"] = 2, ["b"] = 99, ["zero"] = 0, ["unknown"] = 3 };
            int PriceOf(string id) => id switch { "a" => 38900, "b" => 643500, "zero" => 100, _ => -1 };

            long expected = 38900L * 2 + 643500L * 99; // 미지정 가격(-1)·수량 0은 무시
            Assert.AreEqual(expected, CartMath.Total(cart, PriceOf));
        }

        [Test]
        public void Total_LongRange_NoOverflow()
        {
            // 고가(int 상한 근처) × 99 — int 곱셈이었다면 오버플로(기획서: 담는 건 소지금과 별개).
            var cart = new Dictionary<string, int> { ["x"] = 99 };
            Assert.AreEqual(99L * 2_000_000_000, CartMath.Total(cart, _ => 2_000_000_000));
        }

        [Test]
        public void Total_NullSafe()
        {
            Assert.AreEqual(0, CartMath.Total(null, _ => 1));
            Assert.AreEqual(0, CartMath.Total(new Dictionary<string, int> { ["a"] = 1 }, null));
        }
    }
}
