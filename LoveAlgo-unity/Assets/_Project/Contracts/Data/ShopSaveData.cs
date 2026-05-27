using System;
using System.Collections.Generic;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 상점 세이브 데이터.
    /// C4-Phase A Group H에서 LoveAlgo.Shop → LoveAlgo.Contracts 로 이동.
    /// </summary>
    [Serializable]
    public class ShopSaveData
    {
        /// <summary>인벤토리: itemId → 수량</summary>
        public Dictionary<string, int> Inventory = new();

        /// <summary>아이템 효과 시스템 데이터 (세션 버프, 중복 추적)</summary>
        public ItemEffectSaveData EffectData;
    }
}
