using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 독백 딤 오버레이 외부 계약 (Phase B-8a).
    /// 구현: <see cref="LoveAlgo.Stage.MonologueDim"/>.
    /// 호출자: TextLineExecutor (monologue 전환), ScriptRunner (안전망 정리),
    /// GameFlowJumper/SessionController/StageRestorer (라이프사이클 클리어/복원), SaveDataSerializer.
    /// </summary>
    public interface IMonologueDim
    {
        /// <summary>독백 딤 표시 여부 (세이브 IsMonologueDimShowing + 전환 결정에 사용).</summary>
        bool IsShowing { get; }

        /// <summary>딤 페이드 인. duration/alpha 음수면 인스펙터 기본값 사용.</summary>
        UniTask ShowAsync(float duration = -1f, float targetAlpha = -1f, CancellationToken ct = default);

        /// <summary>딤 페이드 아웃. duration 음수면 인스펙터 기본값 사용.</summary>
        UniTask HideAsync(float duration = -1f, CancellationToken ct = default);

        void HideImmediate();

        /// <summary>알파 즉시 적용 표시 (세이브 복원용). 음수면 인스펙터 기본값.</summary>
        void ShowImmediate(float alpha = -1f);
    }
}
