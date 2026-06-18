using System.Collections.Generic;

namespace LoveAlgo.Shop
{
    /// <summary>
    /// 구매 요청 명령(EventBus). UI(M5)가 장바구니(itemId→수량)를 담아 발행 → <see cref="ShopController"/>가 구독·처리.
    /// Service Locator 없이 명령을 흘린다(ADR-007).
    /// </summary>
    public readonly struct PurchaseRequestedCommand
    {
        public readonly IReadOnlyDictionary<string, int> Cart;
        public PurchaseRequestedCommand(IReadOnlyDictionary<string, int> cart) { Cart = cart; }
    }

    /// <summary>구매 거부 사유.</summary>
    public enum ShopRejection
    {
        None,
        EmptyCart,
        ItemNotFound,
        InsufficientFunds
    }

    /// <summary>구매 완료 통지(EventBus). UI 토스트 등(M5)이 구독.</summary>
    public readonly struct ShopPurchasedEvent
    {
        public readonly long TotalCost;
        public ShopPurchasedEvent(long totalCost) { TotalCost = totalCost; }
    }

    /// <summary>구매 거부 통지(EventBus). "소지금 부족" 등 피드백(M5).</summary>
    public readonly struct ShopRejectedEvent
    {
        public readonly ShopRejection Reason;
        public ShopRejectedEvent(ShopRejection reason) { Reason = reason; }
    }
}
