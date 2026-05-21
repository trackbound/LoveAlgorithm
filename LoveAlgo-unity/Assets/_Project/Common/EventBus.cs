using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Common
{
    /// <summary>
    /// 모듈 간 강타입 이벤트 버스. 발행자/구독자가 서로 직접 알지 못한 채 통신.
    /// 두 가지 구독 방식:
    /// 1) 수동: EventBus.Subscribe(handler) → OnDestroy에서 Unsubscribe(handler) 수동 호출
    /// 2) 자동: this.SubscribeOnDestroy(handler) (MonoBehaviour 확장) → 오브젝트 파괴 시 자동 해제
    /// Subscribe도 IDisposable 토큰을 반환하므로 ListenerBag.Track(token.Dispose) 같은 패턴도 가능.
    /// 이벤트 타입은 readonly struct 권장 (할당 최소화 + 불변성).
    /// </summary>
    public static class EventBus
    {
        static readonly Dictionary<Type, Delegate> _handlers = new();

        /// <summary>구독 등록. 반환된 IDisposable을 Dispose하면 자동으로 Unsubscribe 호출됨.</summary>
        public static IDisposable Subscribe<T>(Action<T> handler) where T : struct
        {
            var t = typeof(T);
            if (_handlers.TryGetValue(t, out var existing))
                _handlers[t] = Delegate.Combine(existing, handler);
            else
                _handlers[t] = handler;
            return new SubscriptionToken<T>(handler);
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

        sealed class SubscriptionToken<T> : IDisposable where T : struct
        {
            Action<T> _handler;
            public SubscriptionToken(Action<T> handler) { _handler = handler; }
            public void Dispose()
            {
                var h = _handler;
                if (h == null) return;
                _handler = null;
                Unsubscribe(h);
            }
        }
    }

    /// <summary>
    /// MonoBehaviour의 OnDestroy 시점에 EventBus 구독을 자동 해제하기 위한 보조 컴포넌트.
    /// 직접 부착하지 말고 <see cref="EventBusMonoExtensions.SubscribeOnDestroy{T}"/>를 통해 사용.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class EventBusAutoUnsubscriber : MonoBehaviour
    {
        readonly List<IDisposable> _tokens = new();

        public void Track(IDisposable token)
        {
            if (token != null) _tokens.Add(token);
        }

        void OnDestroy()
        {
            for (int i = _tokens.Count - 1; i >= 0; i--)
            {
                try { _tokens[i]?.Dispose(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _tokens.Clear();
        }
    }

    /// <summary>MonoBehaviour용 자동 해제 구독 확장.</summary>
    public static class EventBusMonoExtensions
    {
        /// <summary>
        /// EventBus 구독을 시작하고, 호출한 MonoBehaviour가 파괴될 때 자동으로 해제한다.
        /// 반환 토큰을 보관할 필요 없음 — 수동으로 더 일찍 해제하고 싶을 때만 사용.
        /// </summary>
        public static IDisposable SubscribeOnDestroy<T>(this MonoBehaviour owner, Action<T> handler) where T : struct
        {
            if (owner == null) throw new ArgumentNullException(nameof(owner));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var token = EventBus.Subscribe(handler);
            var monitor = owner.GetComponent<EventBusAutoUnsubscriber>();
            if (monitor == null)
                monitor = owner.gameObject.AddComponent<EventBusAutoUnsubscriber>();
            monitor.Track(token);
            return token;
        }
    }
}
