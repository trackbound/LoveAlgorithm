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

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
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
                Instance = null;
        }
    }
}
