using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Contracts
{
    /// <summary>
    /// 눈 감기/뜨기 마스크 외부 계약 (Phase B-8a).
    /// 구현: <see cref="LoveAlgo.Stage.EyeMask"/>. 호출자: ScreenFX (Stage 모듈 내부지만 IStage 통과).
    ///
    /// Top/Bottom Image 인스펙터 필드는 캡슐화 — 인터페이스 비노출.
    /// </summary>
    public interface IEyeMask
    {
        /// <summary>눈 감김 상태 (세이브 데이터 IsEyeClosed 동기화용).</summary>
        bool IsClosed { get; }

        UniTask OpenAsync(float duration = 1f, CancellationToken ct = default);
        UniTask CloseAsync(float duration = 1f, CancellationToken ct = default);

        /// <summary>한 번 깜박이기 (close → hold → open).</summary>
        UniTask BlinkAsync(float closeDuration = 0.1f, float openDuration = 0.15f,
            float holdTime = 0.05f, CancellationToken ct = default);

        void CloseImmediate();
        void OpenImmediate();
    }
}
