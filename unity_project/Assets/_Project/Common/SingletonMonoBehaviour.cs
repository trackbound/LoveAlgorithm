using DG.Tweening;
using UnityEngine;

namespace LoveAlgo
{
    /// <summary>
    /// MonoBehaviour 싱글톤 베이스 클래스
    /// 중복 인스턴스 파괴 + Instance 프로퍼티 자동 관리
    /// </summary>
    public abstract class SingletonMonoBehaviour<T> : MonoBehaviour where T : SingletonMonoBehaviour<T>
    {
        public static T Instance { get; private set; }

        /// <summary>Instance가 유효한지 (파괴 예정이 아닌지) 확인</summary>
        public static bool IsAlive => Instance != null;

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[Singleton] {typeof(T).Name} 중복 인스턴스 파괴: {gameObject.name}");
                Destroy(gameObject);
                return;
            }
            Instance = (T)this;
            OnSingletonAwake();
        }

        /// <summary>
        /// 싱글톤 초기화 시 호출 (Awake 대체)
        /// 서브클래스에서 override하여 초기화 로직 작성
        /// </summary>
        protected virtual void OnSingletonAwake() { }

        protected virtual void OnDestroy()
        {
            if (Instance == this)
            {
                DOTween.Kill(this);
                Instance = null;
            }
        }
    }
}
