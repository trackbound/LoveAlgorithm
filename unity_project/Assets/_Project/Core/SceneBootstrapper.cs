using UnityEngine;
using UnityEngine.Serialization;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 씬 부트스트래퍼 — 플레이 시작 시 지정 오브젝트 활성/비활성 정리
    /// 
    /// 사용법:
    /// 1. 빈 GameObject "[Bootstrapper]"에 이 컴포넌트 부착
    /// 2. 인스펙터에서 활성화/비활성화할 오브젝트들을 각각 드래그&드롭
    /// 3. 에디터 시인성을 위해 대상들을 원하는 초기 상태로 배치 가능
    /// 4. 플레이 시 DefaultExecutionOrder(-1000) 으로 다른 Awake보다 먼저 실행
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class SceneBootstrapper : MonoBehaviour
    {
        [Header("플레이 시 자동 활성화할 오브젝트")]
        [Tooltip("배열 순서대로 활성화됩니다. 의존 관계가 있으면 순서에 유의하세요.")]
        [FormerlySerializedAs("targets")]
        [SerializeField] GameObject[] activateOnPlay;

        [Header("플레이 시 자동 비활성화할 오브젝트")]
        [Tooltip("에디터 디버그용 이미지 등, 시작과 동시에 꺼둘 오브젝트를 등록하세요.")]
        [SerializeField] GameObject[] deactivateOnPlay;

        void Awake()
        {
            int activatedCount = SetObjectsActive(activateOnPlay, true);
            int deactivatedCount = SetObjectsActive(deactivateOnPlay, false);

            if (activatedCount > 0 || deactivatedCount > 0)
                Debug.Log($"[SceneBootstrapper] 활성화 {activatedCount}개, 비활성화 {deactivatedCount}개 완료");
        }

        static int SetObjectsActive(GameObject[] objects, bool active)
        {
            if (objects == null) return 0;

            int count = 0;
            for (int i = 0; i < objects.Length; i++)
            {
                var go = objects[i];
                if (go == null || go.activeSelf == active) continue;

                go.SetActive(active);
                count++;
            }

            return count;
        }
    }
}
