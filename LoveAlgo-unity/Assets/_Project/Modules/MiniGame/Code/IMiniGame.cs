using Cysharp.Threading.Tasks;
using System.Threading;

namespace LoveAlgo.MiniGame
{
    /// <summary>
    /// 미니게임 모듈 외부 계약.
    /// 구현: <see cref="MiniGameModule"/>.
    /// </summary>
    public interface IMiniGame
    {
        /// <summary>
        /// 미니게임 실행. 완료까지 대기.
        /// 반환값: 획득 보너스 포인트 (0이면 실패/스킵).
        /// </summary>
        UniTask<int> LaunchAsync(string gameName, string heroineId);

        /// <summary>미니게임 진행 중인지.</summary>
        bool IsRunning { get; }
    }
}
