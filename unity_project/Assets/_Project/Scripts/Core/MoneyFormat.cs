using System;

namespace LoveAlgo.Core
{
    public static class MoneyFormat
    {
        public static string Currency(int value) => Currency((long)value);

        /// <summary>long 소지금/합산액용(GameStateSO.Money·장바구니 총액이 long).</summary>
        public static string Currency(long value)
        {
            long abs = Math.Abs(value);
            return value < 0 ? $"-₩{abs:#,##0}" : $"₩{abs:#,##0}";
        }

        public static string SignedCurrency(int value)
        {
            if (value > 0)
                return $"+₩{value:#,##0}";

            if (value < 0)
                return $"-₩{Math.Abs((long)value):#,##0}";

            return "₩0";
        }
    }
}
