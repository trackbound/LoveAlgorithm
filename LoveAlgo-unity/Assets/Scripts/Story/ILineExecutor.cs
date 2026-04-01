using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Story
{
    /// <summary>
    /// CSV 스크립트 라인 실행기 인터페이스
    /// 각 Type별 실행 로직을 분리하여 OCP를 준수합니다.
    /// </summary>
    public interface ILineExecutor
    {
        /// <summary>
        /// 이 실행기가 처리하는 Type 이름
        /// </summary>
        string Type { get; }

        /// <summary>
        /// 스크립트 라인을 비동기로 실행합니다.
        /// </summary>
        UniTask ExecuteAsync(ScriptLine line, CancellationToken ct);
    }
}
