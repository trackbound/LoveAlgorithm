using System;
using System.Collections.Generic;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LoveAlgo.Common
{
    /// <summary>
    /// UI 이벤트 리스너 자동 정리 헬퍼.
    /// OnDestroy 시점에 <see cref="Dispose"/> 한 번이면 모든 등록 해제 → 메모리 누수/null delegate 호출 방지.
    ///
    /// 사용:
    /// <code>
    /// readonly ListenerBag _listeners = new();
    /// void Awake()
    /// {
    ///     _listeners.Bind(myButton, OnClick);
    ///     _listeners.Bind(myInputField.onSubmit, OnSubmit);
    ///     _listeners.Bind(myToggle.onValueChanged, OnToggleChanged);
    /// }
    /// void OnDestroy() => _listeners.Dispose();
    /// </code>
    /// </summary>
    public sealed class ListenerBag
    {
        readonly List<Action> _unbinders = new();

        // ── Button ──────────────────────────────────────────────
        public void Bind(Button button, UnityAction action)
        {
            if (button == null || action == null) return;
            button.onClick.AddListener(action);
            _unbinders.Add(() => { if (button != null) button.onClick.RemoveListener(action); });
        }

        // ── UnityEvent (no-arg) ─────────────────────────────────
        public void Bind(UnityEvent evt, UnityAction action)
        {
            if (evt == null || action == null) return;
            evt.AddListener(action);
            _unbinders.Add(() => { if (evt != null) evt.RemoveListener(action); });
        }

        // ── UnityEvent<T> (1-arg) ───────────────────────────────
        public void Bind<T>(UnityEvent<T> evt, UnityAction<T> action)
        {
            if (evt == null || action == null) return;
            evt.AddListener(action);
            _unbinders.Add(() => { if (evt != null) evt.RemoveListener(action); });
        }

        /// <summary>등록한 모든 리스너 해제. 멱등 — 여러 번 호출해도 안전.</summary>
        public void Dispose()
        {
            for (int i = _unbinders.Count - 1; i >= 0; i--)
            {
                try { _unbinders[i]?.Invoke(); }
                catch { /* destroyed targets — safe to ignore */ }
            }
            _unbinders.Clear();
        }
    }
}
