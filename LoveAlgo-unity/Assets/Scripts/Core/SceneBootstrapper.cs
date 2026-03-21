using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 씬 부트스트래퍼 — 에디터에서 비활성화된 오브젝트들을 플레이 시 자동 활성화
    /// 
    /// 사용법:
    /// 1. 빈 GameObject "[Bootstrapper]"에 이 컴포넌트 부착
    /// 2. 인스펙터에서 활성화할 오브젝트들을 드래그&드롭
    /// 3. 에디터 시인성을 위해 대상들을 SetActive(false) 상태로 배치 가능
    /// 4. 플레이 시 DefaultExecutionOrder(-1000) 으로 다른 Awake보다 먼저 실행
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("플레이 시 자동 활성화할 오브젝트")]
        [Tooltip("배열 순서대로 활성화됩니다. 의존 관계가 있으면 순서에 유의하세요.")]
        [SerializeField] GameObject[] targets;

        void Awake()
        {
            if (targets == null) return;

            int count = 0;
            for (int i = 0; i < targets.Length; i++)
            {
                var go = targets[i];
                if (go == null || go.activeSelf) continue;

                go.SetActive(true);
                count++;
            }

            if (count > 0)
                Debug.Log($"[SceneBootstrapper] {count}개 오브젝트 활성화 완료");
        }
    }
}
