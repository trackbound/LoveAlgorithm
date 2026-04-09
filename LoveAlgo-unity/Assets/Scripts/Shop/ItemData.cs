using System;
using UnityEngine;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 아이템 카테고리
    /// </summary>
    public enum ItemCategory
    {
        Gift,           // 선물 — 히로인 호감도 상승
        Consumable,     // 소모품 — 피로 회복 등
        SessionBuff     // 세션 버프 — 자유행동 1회 동안 스탯 일시 보정
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
        /// 효과 값 (Consumable: 피로 회복량, SessionBuff: 스탯 증가량)
        /// </summary>
        public int EffectValue;

        /// <summary>
        /// 아이템 이름 (Art/Item/{name}, Art/Item/Icon/{name} 경로용)
        /// </summary>
        public string IconPath;

        /// <summary>
        /// 아이콘 스프라이트 (SaleSlot, CartSlot 공통 사용)
        /// 경로: Art/Item/Icon/{name}
        /// </summary>
        public Sprite IconSprite;

        /// <summary>
        /// 상세 팔업용 큰 이미지 스프라이트
        /// 경로: Art/Item/{name}
        /// </summary>
        public Sprite DetailSprite;

        /// <summary>SaleSlot/CartSlot용 아이콘 (없으면 fallback)</summary>
        public Sprite GetSaleIcon() => IconSprite != null ? IconSprite : FallbackIcon;

        /// <summary>장바구니용 아이콘 (SaleSlot과 동일)</summary>
        public Sprite GetSmallIcon() => IconSprite != null ? IconSprite : FallbackIcon;

        /// <summary>상세 팔업용 큰 이미지 (Art/Item/{name})</summary>
        public Sprite GetDetailImage() => DetailSprite != null ? DetailSprite : (IconSprite != null ? IconSprite : FallbackIcon);

        /// <summary>아이콘 미설정 시 사용할 폴백 스프라이트</summary>
        public static Sprite FallbackIcon { get; set; }

        /// <summary>
        /// 해금 시점 (기획서: 이벤트 진행에 따라 상점에 등장)
        /// </summary>
        public ItemAvailability Availability;

        /// <summary>
        /// 효과 대상 스탯 ID (SessionBuff용: "Str", "Int", "Per" 등)
        /// </summary>
        public string EffectStat;

        /// <summary>
        /// 보조 효과 대상 스탯 ID (복합 효과용: 무릎담요→"Fatigue", 노트북 거치대→"Int")
        /// null이면 보조 효과 없음
        /// </summary>
        public string SubEffectStat;

        /// <summary>보조 효과 값 (복합 효과용)</summary>
        public int SubEffectValue;

        /// <summary>
        /// 중복 효율 추적용 태그 (기획서: 동일 태그 2회차부터 50%)
        /// null이면 아이템 ID를 태그로 사용
        /// </summary>
        public string DuplicateTag;
        /// <summary>선물 대상 히로인 ID (Gift 전용: "Roa", "Daeun", "Yeeun", "Heewon", "Bom")</summary>
        public string TargetHeroine;

        /// <summary>2차 이벤트 호감도 보너스 (Gift 전용)</summary>
        public int LoveEffect2;

        /// <summary>3차 이벤트 호감도 보너스 (Gift 전용)</summary>
        public int LoveEffect3;
        /// <summary>SO 직렬화용 기본 생성자</summary>
        public ItemData() { }

        public ItemData(string id, string name, string desc, int price,
            ItemCategory category,
            int effectValue = 0, string iconPath = null,
            ItemAvailability availability = ItemAvailability.Always,
            string effectStat = null, string duplicateTag = null,
            string targetHeroine = null, int loveEffect2 = 0, int loveEffect3 = 0,
            string subEffectStat = null, int subEffectValue = 0)
        {
            Id = id;
            Name = name;
            Description = desc;
            Price = price;
            Category = category;
            EffectValue = effectValue;
            IconPath = iconPath;
            Availability = availability;
            EffectStat = effectStat;
            DuplicateTag = duplicateTag;
            TargetHeroine = targetHeroine;
            LoveEffect2 = loveEffect2;
            LoveEffect3 = loveEffect3;
            SubEffectStat = subEffectStat;
            SubEffectValue = subEffectValue;
        }

        /// <summary>중복 추적에 사용할 태그</summary>
        public string GetDuplicateTag() => DuplicateTag ?? Id;
    }
}
