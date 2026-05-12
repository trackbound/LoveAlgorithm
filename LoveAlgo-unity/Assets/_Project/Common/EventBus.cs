using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Common
{
    /// <summary>
    /// 모듈 간 강타입 이벤트 버스. 발행자/구독자가 서로 직접 알지 못한 채 통신.
    /// 사용법:
    ///   EventBus.Publish(new MyEvent { Foo = 1 });
    ///   EventBus.Subscribe&lt;MyEvent&gt;(OnMyEvent);
    ///   EventBus.Unsubscribe&lt;MyEvent&gt;(OnMyEvent); // 핸들러 해제 시 반드시
    /// 이벤트 타입은 readonly struct 권장 (할당 최소화 + 불변성).
    /// </summary>
    public static class EventBus
    {
        static readonly Dictionary<Type, Delegate> _handlers = new();

        public static void Subscribe<T>(Action<T> handler) where T : struct
        {
            var t = typeof(T);
            if (_handlers.TryGetValue(t, out var existing))
                _handlers[t] = Delegate.Combine(existing, handler);
            else
                _handlers[t] = handler;
        }

        public static void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            var t = typeof(T);
            if (!_handlers.TryGetValue(t, out var existing)) return;
            var remaining = Delegate.Remove(existing, handler);
            if (remaining == null) _handlers.Remove(t);
            else _handlers[t] = remaining;
        }

        public static void Publish<T>(T evt) where T : struct
        {
            if (!_handlers.TryGetValue(typeof(T), out var d)) return;
            try
            {
                ((Action<T>)d).Invoke(evt);
            }
            catch (Exception e)
            {
                Debug.LogError($"[EventBus] {typeof(T).Name} 핸들러 예외: {e}");
            }
        }

        /// <summary>씬 전환/도메인 리로드 시 정리.</summary>
        public static void Clear() => _handlers.Clear();
    }
}
