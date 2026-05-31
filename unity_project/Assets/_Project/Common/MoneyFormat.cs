using System;

namespace LoveAlgo.Core
{
    public static class MoneyFormat
    {
        public static string Currency(int value)
        {
            long abs = Math.Abs((long)value);
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
