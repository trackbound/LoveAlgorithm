using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LoveAlgo.Common
{
    /// <summary>
    /// 코드에서 추가한 UnityEvent 리스너의 일괄 해제용 컨테이너.
    /// MonoBehaviour의 OnDestroy에서 Dispose 한 번이면 모두 정리.
    /// 인스펙터에서 붙인 리스너는 건드리지 않음 (RemoveListener는 그 핸들러만 떼어냄).
    /// </summary>
    public sealed class ListenerBag
    {
        readonly List<Action> _cleanups = new();

        public void Bind(Button button, UnityAction call)
        {
            if (button == null || call == null) return;
            button.onClick.AddListener(call);
            _cleanups.Add(() => { if (button != null) button.onClick.RemoveListener(call); });
        }

        public void Bind(UnityEvent ev, UnityAction call)
        {
            if (ev == null || call == null) return;
            ev.AddListener(call);
            _cleanups.Add(() => { if (ev != null) ev.RemoveListener(call); });
        }

        public void Bind<T>(UnityEvent<T> ev, UnityAction<T> call)
        {
            if (ev == null || call == null) return;
            ev.AddListener(call);
            _cleanups.Add(() => { if (ev != null) ev.RemoveListener(call); });
        }

        public void Track(Action cleanup)
        {
            if (cleanup != null) _cleanups.Add(cleanup);
        }

        public void Dispose()
        {
            for (int i = _cleanups.Count - 1; i >= 0; i--)
            {
                try { _cleanups[i]?.Invoke(); }
                catch (Exception e) { Debug.LogException(e); }
            }
            _cleanups.Clear();
        }
    }
}
