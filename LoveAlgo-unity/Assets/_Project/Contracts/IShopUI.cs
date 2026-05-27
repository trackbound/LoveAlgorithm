using UnityEngine;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 상점 UI 외부 계약 (Phase B-5).
    /// 구현: <see cref="LoveAlgo.Shop.ShopUI"/>.
    ///
    /// 외부 호출자(ScheduleUI)는 크로스페이드용 CanvasGroup + 패널 진입 시 초기화(Open) 만 사용.
    /// 장바구니/구매/슬롯 호버 같은 내부 동작은 ShopUI 캡슐화. 구매 완료 후 외부 통지는
    /// PopupManager Toast 로만 처리되므로 인터페이스에 이벤트 불필요.
    /// </summary>
    public interface IShopUI
    {
        /// <summary>패널 크로스페이드용 CanvasGroup (ScheduleUI 가 shop 탭 전환에 사용).</summary>
        CanvasGroup CanvasGroup { get; }

        /// <summary>패널 열릴 때 초기화 (장바구니 비우기, 머니/판매 목록 새로고침).</summary>
        void Open();
    }
}
