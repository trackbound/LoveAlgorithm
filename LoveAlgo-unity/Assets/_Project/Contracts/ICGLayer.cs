using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// CG 레이어 외부 계약 (Phase B-8b).
    /// 구현: <see cref="LoveAlgo.Stage.CGLayer"/>.
    /// 호출자: CGLineExecutor (Execute), SaveDataSerializer/StageRestorer (세이브/복원),
    /// GameFlowJumper/SessionController (Clear).
    /// </summary>
    public interface ICGLayer
    {
        bool IsShowing { get; }
        string CurrentCG { get; }

        /// <summary>CSV 인라인 명령 실행: "name:duration" 등.</summary>
        UniTask ExecuteAsync(string value, CancellationToken ct = default);

        UniTask ShowAsync(string cgName, float duration = 1f, CancellationToken ct = default);

        /// <summary>즉시 비활성 + 상태 클리어.</summary>
        void Clear();
    }
}
