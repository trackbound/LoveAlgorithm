namespace LoveAlgo.Events
{
    /// <summary>
    /// 상점 화면 열기(ADR-013 Overlay). 스케줄 화면의 "아이템 구매" 버튼(<c>ShopOpenButton</c>)이 발행 →
    /// <c>ShopView</c>가 구독·표시. Schedule↔Shop은 피처 간 직접 참조 금지라 Core 이벤트로 경유(ADR-007).
    /// 닫기는 공용 뒤로가기(OverlayGate.CloseTop) 또는 뷰 자체 닫기 — 별도 명령 불요.
    /// </summary>
    public readonly struct ShowShopCommand
    {
    }
}
