using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Common
{
    /// <summary>
    /// 모듈 인터페이스 등록·조회 (싱글톤 대체).
    /// 모듈 진입점({Name}Module.cs)에서 자기 인터페이스를 Register, 다른 모듈은 Get/TryGet으로 사용.
    /// 직접 클래스 참조 대신 인터페이스로만 의존 → 모듈 교체·테스트 용이.
    ///
    /// 두 API:
    /// - Get&lt;T&gt;(): 필수 서비스 조회. 미등록 시 ServiceNotRegisteredException throw — 호출처가
    ///                 null 체크를 잊어도 컴파일러 도움 없이 즉시 fail-fast.
    /// - TryGet&lt;T&gt;(): 선택적 서비스 조회. 미등록 시 null 반환. 호출처에서 명시적으로 null 처리.
    /// </summary>
    public static class Services
    {
        static readonly Dictionary<Type, object> _services = new();

        public static void Register<T>(T service) where T : class
        {
            var t = typeof(T);
            if (_services.TryGetValue(t, out var existing))
            {
                // 같은 인스턴스 재등록은 idempotent — 도메인 리로드 등으로 Awake가 두 번
                // 호출되는 정상 케이스. silent하게 통과.
                if (ReferenceEquals(existing, service)) return;

                // 다른 인스턴스로 덮어쓰는 경우는 거의 항상 사고(중복 모듈 GameObject 등).
                // 스택 트레이스를 남겨 추적 가능하도록 LogError로 격상.
                Debug.LogError(
                    $"[Services] {t.Name} 다른 인스턴스가 이미 등록됨 — 새 인스턴스로 덮어씀. " +
                    $"기존: {(existing is UnityEngine.Object oldObj && oldObj != null ? oldObj.name : existing.GetType().Name)}, " +
                    $"신규: {(service is UnityEngine.Object newObj && newObj != null ? newObj.name : service.GetType().Name)}");
            }
            _services[t] = service;
        }

        public static void Unregister<T>() where T : class => _services.Remove(typeof(T));

        /// <summary>
        /// 필수 서비스 조회. 미등록 시 throw — 호출처는 결과를 그대로 dereference 가능.
        /// 옵셔널 경로에서는 <see cref="TryGet{T}"/> 사용.
        /// </summary>
        public static T Get<T>() where T : class
        {
            if (_services.TryGetValue(typeof(T), out var s)) return (T)s;
            throw new ServiceNotRegisteredException(typeof(T));
        }

        /// <summary>선택적 서비스 조회. 미등록 시 null 반환.</summary>
        public static T TryGet<T>() where T : class
        {
            return _services.TryGetValue(typeof(T), out var s) ? (T)s : null;
        }

        public static bool Has<T>() where T : class => _services.ContainsKey(typeof(T));

        /// <summary>씬 전환/도메인 리로드 시 정리.</summary>
        public static void Clear() => _services.Clear();
    }

    /// <summary>
    /// <see cref="Services.Get{T}"/>가 미등록 타입에 대해 던지는 예외.
    /// 옵셔널 조회는 <see cref="Services.TryGet{T}"/> 사용.
    /// </summary>
    public class ServiceNotRegisteredException : Exception
    {
        public Type ServiceType { get; }
        public ServiceNotRegisteredException(Type t)
            : base($"[Services] {t.Name} 미등록. 호출 전에 Register 했는지 확인하거나, 옵셔널이면 TryGet 사용.")
        {
            ServiceType = t;
        }
    }
}
