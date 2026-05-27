using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 스케줄 UI 외부 계약 (Phase B-6).
    /// 구현: <see cref="LoveAlgo.Schedule.ScheduleUI"/>.
    ///
    /// 외부 표면 ISP: ShowAsync(진입) + UsedLoadingToday(세이브 동기화) + ResetDailyLimits(일일 리셋).
    /// HideAsync/OpenShop 은 ScheduleUI 내부 흐름(콜백/Button.onClick 바인딩)에서만 쓰여 외부 노출 불요.
    /// 탭 전환, 슬롯 클릭, 스탯 갱신 등 큰 면적의 동작은 ScheduleUI 캡슐화 내부 유지.
    /// </summary>
    public interface IScheduleUI
    {
        /// <summary>오늘 상하차를 이미 했는지 (하루 1회 제한, 세이브 동기화).</summary>
        bool UsedLoadingToday { get; set; }

        /// <summary>UI 표시 (항상 스케줄 패널로 시작). onSelected = 슬롯 선택 콜백.</summary>
        UniTask ShowAsync(Action<ScheduleType> onSelected, CancellationToken ct = default);

        /// <summary>하루 시작 시 제한 초기화.</summary>
        void ResetDailyLimits();
    }
}
