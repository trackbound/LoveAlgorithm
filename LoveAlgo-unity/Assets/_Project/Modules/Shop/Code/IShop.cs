namespace LoveAlgo.Shop
{
    /// <summary>
    /// 상점/인벤토리 모듈 외부 계약.
    /// 구현: <see cref="ShopModule"/>.
    /// </summary>
    public interface IShop
    {
        /// <summary>아이템 보유 여부.</summary>
        bool HasItem(string itemId);

        /// <summary>아이템 개수 조회.</summary>
        int GetItemCount(string itemId);

        /// <summary>소모품 즉시 사용 (피로 회복 등). 반환값: 실제 적용 효과량 (0이면 실패).</summary>
        int UseConsumable(string itemId, int currentDay = -1);

        /// <summary>세션 버프 아이템 등록 (자유행동 1회 보정용).</summary>
        bool UseSessionBuff(string itemId, int currentDay);

        /// <summary>ShopUI 인스턴스 (lazy spawn).</summary>
        ShopUI ShopUI { get; }
    }
}
