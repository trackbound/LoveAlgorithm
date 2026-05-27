using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// VirtualBG 오버레이 외부 계약 (Phase B-8b).
    /// 구현: <see cref="LoveAlgo.Stage.VirtualBGOverlay"/>.
    /// 호출자: OverlayLineExecutor + SetupMacroExecutor (Execute), SaveDataSerializer/StageRestorer
    /// (세이브/복원), GameFlowJumper/SessionController (HideImmediate), CharacterLayer
    /// (표정 변경 시 SwitchAsync — Stage 내부 동일 인터페이스 통과).
    /// </summary>
    public interface IVirtualBGOverlay
    {
        bool IsShowing { get; }
        string CurrentOverlay { get; }

        /// <summary>CSV 인라인 명령 실행: "name:FadeIn:duration" 등.</summary>
        UniTask ExecuteAsync(string value, CancellationToken ct = default);

        UniTask ShowAsync(string overlayName, float duration = 0.5f, float targetAlpha = 0.7f, CancellationToken ct = default);

        /// <summary>현재 오버레이 → 새 오버레이로 크로스페이드 (표정 변경 시).</summary>
        UniTask SwitchAsync(string overlayName, float duration = 0f, CancellationToken ct = default);

        /// <summary>오버레이 페이드 아웃 (CharacterLayer 가 로아 퇴장 시 자동 호출).</summary>
        UniTask HideAsync(float duration = 0.5f, CancellationToken ct = default);

        void HideImmediate();
    }
}
