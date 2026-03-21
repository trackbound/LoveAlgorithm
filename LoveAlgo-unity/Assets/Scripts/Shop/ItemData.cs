using System;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 카테고리
    /// </summary>
    public enum ItemCategory
    {
        Gift,           // 선물 — 히로인에게 줄 수 있음
        Consumable,     // 소모품 — 피로 회복 등
        SessionBuff     // 세션 버프 — 자유행동 1회 동안 스탯 일시 보정
    }

    /// <summary>
    /// 선물 계층 (기획서: 가격대별 포인트 결정)
    /// </summary>
    public enum GiftTier
    {
        None,       // 선물이 아닌 아이템
        Low,        // 저가 (≤1만): 2차(+1), 3차(+2)
        Mid,        // 중급 (1만~3만대): 2차(+2), 3차(+3)
        High,       // 고급 (4만~7만대): 2차(+3), 3차(+4)
        Premium     // 최고급 (≥8만): 2차(+3), 3차(+5)
    }

    /// <summary>
    /// 아이템 해금 시점 (기획서: 스토리 진행에 따라 상점에 등장)
    /// </summary>
    public enum ItemAvailability
    {
        Always,             // 항상 구매 가능
        AfterEvent2Start,   // 2차 이벤트 시작 전 오픈
        AfterEvent3Start,   // 3차 이벤트 시작 전 오픈
        AfterConfession     // 고백 이벤트 시작 전 오픈
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
        /// 대상 히로인 ID (null이면 범용)
        /// Gift 카테고리에서만 사용
        /// </summary>
        public string TargetHeroine;

        /// <summary>
        /// 효과 값 (Consumable: 피로 회복량, SessionBuff: 스탯 증가량)
        /// Gift는 GiftTier로 포인트 결정하므로 이 값 무시
        /// </summary>
        public int EffectValue;

        /// <summary>
        /// Sale 슬롯용 큰 이미지 경로 (Resources 기준)
        /// </summary>
        public string IconPath;

        /// <summary>
        /// Cart 슬롯용 작은 아이콘 경로 (자동 파생: Items/xxx → Items/Icon/xxx)
        /// </summary>
        public string IconSmallPath
        {
            get => !string.IsNullOrEmpty(IconPath) && IconPath.StartsWith("Items/")
                ? IconPath.Replace("Items/", "Items/Icon/")
                : IconPath;
        }

        /// <summary>
        /// 선물 계층 (기획서: 계층에 따라 2차/3차 이벤트 포인트 결정)
        /// </summary>
        public GiftTier Tier;

        /// <summary>
        /// 해금 시점 (기획서: 이벤트 진행에 따라 상점에 등장)
        /// </summary>
        public ItemAvailability Availability;

        /// <summary>
        /// 효과 대상 스탯 ID (SessionBuff용: "Str", "Int", "Per" 등)
        /// </summary>
        public string EffectStat;

        /// <summary>
        /// 중복 효율 추적용 태그 (기획서: 동일 태그 2회차부터 50%)
        /// null이면 아이템 ID를 태그로 사용
        /// </summary>
        public string DuplicateTag;

        /// <summary>SO 직렬화용 기본 생성자</summary>
        public ItemData() { }

        public ItemData(string id, string name, string desc, int price,
            ItemCategory category, string targetHeroine = null,
            int effectValue = 0, string iconPath = null,
            GiftTier tier = GiftTier.None,
            ItemAvailability availability = ItemAvailability.Always,
            string effectStat = null, string duplicateTag = null)
        {
            Id = id;
            Name = name;
            Description = desc;
            Price = price;
            Category = category;
            TargetHeroine = targetHeroine;
            EffectValue = effectValue;
            IconPath = iconPath;
            Tier = tier;
            Availability = availability;
            EffectStat = effectStat;
            DuplicateTag = duplicateTag;
        }

        /// <summary>히로인 전용 선물인지</summary>
        public bool IsHeroineSpecific => !string.IsNullOrEmpty(TargetHeroine);

        /// <summary>중복 추적에 사용할 태그</summary>
        public string GetDuplicateTag() => DuplicateTag ?? Id;
    }
}
