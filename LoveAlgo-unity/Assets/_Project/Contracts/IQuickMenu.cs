using System;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 퀵 메뉴 UI 외부 계약 (Phase B-3).
    /// 구현: <see cref="LoveAlgo.UI.QuickMenu"/>.
    ///
    /// 외부 호출자(ScheduleUI 등)가 필요로 하는 표면은 BackButton 이벤트 구독뿐. Toggle/Open/Close
    /// 같은 내부 토글 동작은 QuickMenu 인스펙터 OnClick 바인딩 + DOTween 시퀀스라 캡슐화.
    /// </summary>
    public interface IQuickMenu
    {
        /// <summary>퀵 메뉴 "돌아가기" 버튼 클릭 이벤트 (ScheduleUI 가 구독).</summary>
        event Action OnBackRequested;
    }
}
