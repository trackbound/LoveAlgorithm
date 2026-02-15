using System;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 카테고리
    /// </summary>
    public enum ItemCategory
    {
        Gift,           // 선물 — 히로인에게 줄 수 있음
        Consumable      // 소모품 — 피로 회복 등
    }

    /// <summary>
    /// 아이템 데이터 (정적 정의용)
    /// </summary>
    [Serializable]
    public class ItemData
    {
        /// <summary>고유 ID (예: gift_yeun_bracelet)</summary>
        public string Id;

        /// <summary>표시 이름</summary>
        public string Name;

        /// <summary>설명</summary>
        public string Description;

        /// <summary>가격</summary>
        public int Price;

        /// <summary>카테고리</summary>
        public ItemCategory Category;

        /// <summary>
        /// 대상 히로인 ID (null이면 범용 선물)
        /// Gift 카테고리에서만 사용
        /// </summary>
        public string TargetHeroine;

        /// <summary>
        /// 효과 값 (Gift: 호감 포인트, Consumable: 피로 회복량 등)
        /// </summary>
        public int EffectValue;

        /// <summary>
        /// 아이콘 경로 (Resources 기준, null이면 기본 아이콘)
        /// </summary>
        public string IconPath;

        public ItemData(string id, string name, string desc, int price,
            ItemCategory category, string targetHeroine = null,
            int effectValue = 0, string iconPath = null)
        {
            Id = id;
            Name = name;
            Description = desc;
            Price = price;
            Category = category;
            TargetHeroine = targetHeroine;
            EffectValue = effectValue;
            IconPath = iconPath;
        }

        /// <summary>히로인 전용 선물인지</summary>
        public bool IsHeroineSpecific => !string.IsNullOrEmpty(TargetHeroine);
    }
}
