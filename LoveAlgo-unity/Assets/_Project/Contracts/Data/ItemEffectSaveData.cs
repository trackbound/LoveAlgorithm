using System;
using System.Collections.Generic;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// ItemEffectSystem 세이브 데이터.
    /// C4-Phase A Group H에서 LoveAlgo.Shop → LoveAlgo.Contracts 로 이동 (ShopSaveData sub-dep).
    /// </summary>
    [Serializable]
    public class ItemEffectSaveData
    {
        public string ActiveBuffStat;
        public int ActiveBuffValue;
        public string ActiveSubBuffStat;
        public int ActiveSubBuffValue;
        public bool HasActiveBuff;
        public int LastTrackedDay;
        public Dictionary<string, int> DayUsageCount = new();
    }
}
