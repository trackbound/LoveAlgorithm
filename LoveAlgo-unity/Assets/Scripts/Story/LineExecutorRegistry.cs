using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// ILineExecutor 등록 및 조회 레지스트리
    /// Type 문자열 → 실행기 매핑을 관리합니다.
    /// </summary>
    public class LineExecutorRegistry
    {
        private readonly Dictionary<string, ILineExecutor> _executors = new Dictionary<string, ILineExecutor>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 실행기를 등록합니다.
        /// </summary>
        public void Register(ILineExecutor executor)
        {
            if (executor == null) throw new ArgumentNullException(nameof(executor));
            if (string.IsNullOrEmpty(executor.Type)) throw new ArgumentException("Executor Type cannot be null or empty.", nameof(executor.Type));

            _executors[executor.Type] = executor;
        }

        /// <summary>
        /// Type에 해당하는 실행기를 조회합니다.
        /// </summary>
        public bool TryGetExecutor(string type, out ILineExecutor executor)
        {
            return _executors.TryGetValue(type, out executor);
        }

        /// <summary>
        /// 등록된 모든 Type을 반환합니다.
        /// </summary>
        public IEnumerable<string> RegisteredTypes => _executors.Keys;

        /// <summary>
        /// 모든 실행기를 제거합니다.
        /// </summary>
        public void Clear() => _executors.Clear();
    }

    /// <summary>
    /// ILineExecutor 자동 등록을 위한 특성
    /// 클래스에 이 특성을 붙이면 Assembly 스캔 시 자동 등록됩니다.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class LineExecutorAttribute : Attribute
    {
        public string Type { get; }
        public LineExecutorAttribute(string type) => Type = type;
    }
}

