using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Common
{
    /// <summary>
    /// 모듈 인터페이스 등록·조회 (싱글톤 대체).
    /// 모듈 진입점({Name}Module.cs)에서 자기 인터페이스를 Register, 다른 모듈은 Get으로 사용.
    /// 직접 클래스 참조 대신 인터페이스로만 의존 → 모듈 교체·테스트 용이.
    /// 사용법:
    ///   Services.Register&lt;IStage&gt;(this);
    ///   var stage = Services.Get&lt;IStage&gt;();
    /// </summary>
    public static class Services
    {
        static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T service) where T : class
        {
            var t = typeof(T);
            if (_services.ContainsKey(t))
                Debug.LogWarning($"[Services] {t.Name} 중복 등록 — 덮어씀");
            _services[t] = service;
        }

        public static void Unregister<T>() where T : class => _services.Remove(typeof(T));

        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) return (T)s;
            Debug.LogError($"[Services] {typeof(T).Name} 미등록 — null 반환");
            return null;
        }

        public static T TryGet<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
        }

        public static bool Has<T>() where T : class => _services.ContainsKey(typeof(T));

        /// <summary>씬 전환/도메인 리로드 시 정리.</summary>
        public static void Clear() => _services.Clear();
    }
}
