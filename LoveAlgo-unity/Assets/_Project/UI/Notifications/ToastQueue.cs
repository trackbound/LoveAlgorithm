using System;
using System.Collections.Generic;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 토스트 요청 (단일 메시지). 큐 dedup용 Equals 구현.
    /// </summary>
    public readonly struct ToastRequest : IEquatable<ToastRequest>
    {
        public readonly string Title;
        public readonly string Message;
        public readonly float Duration;

        public ToastRequest(string title, string message, float duration)
        {
            Title = title ?? "";
            Message = message ?? "";
            Duration = duration;
        }

        public bool Equals(ToastRequest other)
            => Title == other.Title && Message == other.Message;
        // Duration은 dedup 키에서 제외 — 같은 텍스트가 살짝 다른 duration으로 두 번 와도 1번만 보여줌

        public override bool Equals(object obj) => obj is ToastRequest r && Equals(r);
        public override int GetHashCode()
            => (Title?.GetHashCode() ?? 0) ^ ((Message?.GetHashCode() ?? 0) << 1);
    }

    /// <summary>
    /// 토스트 대기열 (Phase D8 폴리쉬).
    /// 정책:
    ///   1) 현재 재생 중인 토스트와 같은 요청 → 드롭 (즉시 재요청 dedup)
    ///   2) 큐 뒷자리에 같은 요청 → 드롭 (연속 dedup)
    ///   3) 큐 길이 ≥ MaxPending → 드롭 (FIFO 보호; 새 요청이 손해)
    ///   4) 그 외 → 큐에 추가
    ///
    /// MonoBehaviour 의존성 없음 — EditMode 테스트로 dedup/cap 정책 검증 가능.
    /// </summary>
    public sealed class ToastQueue
    {
        readonly List<ToastRequest> _pending;
        ToastRequest _current;
        bool _hasCurrent;
        readonly int _maxPending;

        public ToastQueue(int maxPending = 4)
        {
            _maxPending = maxPending > 0 ? maxPending : 1;
            _pending = new List<ToastRequest>(_maxPending);
        }

        public int MaxPending => _maxPending;
        public int PendingCount => _pending.Count;
        public bool HasCurrent => _hasCurrent;

        /// <summary>요청을 큐에 추가 시도. 정책 위반(dedup/cap)이면 false.</summary>
        public bool TryEnqueue(ToastRequest req)
        {
            // 1) 현재 재생 중인 것과 동일 → 드롭
            if (_hasCurrent && _current.Equals(req)) return false;

            // 2) 큐 마지막 항목과 동일 → 드롭 (연속 dedup)
            if (_pending.Count > 0 && _pending[_pending.Count - 1].Equals(req)) return false;

            // 3) 큐 캡 초과 → 드롭
            if (_pending.Count >= _maxPending) return false;

            _pending.Add(req);
            return true;
        }

        /// <summary>다음 재생할 항목을 꺼냄. 없으면 false 반환 (HasCurrent도 false).</summary>
        public bool TryDequeueNext(out ToastRequest req)
        {
            if (_pending.Count == 0)
            {
                _hasCurrent = false;
                req = default;
                return false;
            }

            req = _pending[0];
            _pending.RemoveAt(0);
            _current = req;
            _hasCurrent = true;
            return true;
        }

        /// <summary>현재 재생 끝. 다음 항목이 있으면 TryDequeueNext 호출자가 책임짐.</summary>
        public void MarkCurrentFinished() => _hasCurrent = false;

        /// <summary>씬 전환/긴급 중단 — 전부 비우기.</summary>
        public void Clear()
        {
            _pending.Clear();
            _hasCurrent = false;
        }
    }
}
