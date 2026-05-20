using System.Collections.Generic;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// LoadScript/JumpToIndex 등 스크립트 흐름이 끊기는 시점에 stale 상태를
    /// 폐기하고 싶은 라인 실행기가 구현. (예: CG enter했는데 exit 전에 점프된 경우)
    /// </summary>
    public interface IResettableExecutor
    {
        void ResetState();
    }

    /// <summary>
    /// 라인 실행기 레지스트리 — LineType별 ILineExecutor 매핑
    /// </summary>
    public static class LineHandlerRegistry
    {
        static readonly Dictionary<LineType, ILineExecutor> _executors = new();

        public static void Register(ILineExecutor executor)
        {
            _executors[executor.Type] = executor;
        }

        public static bool TryGet(LineType type, out ILineExecutor executor)
        {
            return _executors.TryGetValue(type, out executor);
        }

        /// <summary>등록된 모든 IResettableExecutor의 ResetState 호출. 점프/스크립트 로드 시점에 부른다.</summary>
        public static void ResetAllExecutorState()
        {
            foreach (var exec in _executors.Values)
            {
                if (exec is IResettableExecutor r) r.ResetState();
            }
        }

        public static void Clear()
        {
            _executors.Clear();
        }
    }
}
