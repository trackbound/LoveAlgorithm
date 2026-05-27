using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 배경 레이어 외부 계약 (Phase B-8c-1).
    /// 구현: <see cref="LoveAlgo.Story.BackgroundLayer"/>.
    ///
    /// 호출자: BGLineExecutor (CSV 인라인 명령), SceneStartMacroExecutor/SetupMacroExecutor
    /// (장면 시작 배경 세팅), GameFlowController (스케줄 진입 시 기본 BG), SessionController
    /// (세션 리셋 시 Clear), SaveDataSerializer (CurrentBG 직렬화), StageRestorer (로드 복원).
    ///
    /// 인스펙터 binding (Image / fade duration / sprite cache) 등은 캡슐화.
    /// </summary>
    public interface IBackgroundLayer
    {
        /// <summary>현재 표시 중인 배경 이름 (세이브/직렬화용).</summary>
        string CurrentBackground { get; }

        /// <summary>CSV 인라인 명령 실행: "name:transition:duration" 등.</summary>
        UniTask ExecuteAsync(string value, CancellationToken ct = default);

        /// <summary>배경 변경 (전환 효과 + 페이드).</summary>
        UniTask ChangeBackgroundAsync(string bgName, BGTransition transition, float duration, CancellationToken ct = default);

        /// <summary>현재 배경 즉시 클리어.</summary>
        void Clear();
    }
}
