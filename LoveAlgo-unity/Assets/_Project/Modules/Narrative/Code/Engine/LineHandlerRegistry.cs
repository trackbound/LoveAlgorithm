using System.Collections.Generic;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine
{
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

        public static void Clear()
        {
            _executors.Clear();
        }
    }
}
