using System.Threading;
using Cysharp.Threading.Tasks;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 스크립트 라인 실행 인터페이스
    /// </summary>
    public interface ILineExecutor
    {
        LineType Type { get; }
        UniTask<bool> ExecuteAsync(ScriptLine line, CancellationToken ct);
    }
}
