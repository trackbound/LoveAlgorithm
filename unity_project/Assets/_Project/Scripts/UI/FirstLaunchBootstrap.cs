using LoveAlgo.Common; // Log
using UnityEngine;
using UnityEngine.SceneManagement; // SceneManager (진입 씬 가드)

namespace LoveAlgo.UI
{
    /// <summary>
    /// 앱 구동 직후(첫 씬=타이틀 로드 후) 1회 실행되어, 설치 후 **첫 구동**이면 인트로 오버레이 프리팹을 띄운다
    /// (<c>Resources/UI/FirstLaunchOverlay</c>). 오버레이를 탭하면 오버레이가 새 게임을 발행 → 기존 새게임→프롤로그
    /// 경로 그대로. 이후 구동은 <see cref="FirstLaunchFlag"/>로 스킵되어 타이틀이 정상 표시된다.
    ///
    /// <para>씬을 건드리지 않는 런타임 스폰이라 Title/Game 씬을 수정하지 않는다. 프리팹이 자체 Canvas(높은
    /// sortingOrder)를 포함해 어느 씬 위든 단독 렌더된다. 프리팹이 없으면 <b>미마킹+스킵</b>(fail-open) — 프리팹을
    /// 추가하면 다음 첫 구동에 표시된다.</para>
    /// </summary>
    public static class FirstLaunchBootstrap
    {
        const string OverlayResourcePath = "UI/FirstLaunchOverlay";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnStartup()
        {
            if (FirstLaunchFlag.Seen) return; // 재구동: 타이틀 정상
            // 앱 진입 씬(빌드 인덱스 0 = 타이틀)에서만 표시 — 에디터에서 게임 씬을 직접 Play할 때 오버레이가 끼어들지 않게.
            if (SceneManager.GetActiveScene().buildIndex != 0) return;

            if (SpawnOverlay() == null) return; // 프리팹 없음 → 미마킹(프리팹이 생기면 다음 첫 구동에 표시)
            FirstLaunchFlag.MarkSeen(); // 표시 성공 후 기록 — 중도 종료해도 이후 구동은 타이틀
        }

        /// <summary>
        /// 인트로 오버레이 프리팹을 로드·생성해 인스턴스를 반환(플래그 무관 — 순수 스폰).
        /// 첫 구동 경로(<see cref="OnStartup"/>)와 테스트 빌드의 온디맨드 트리거(DevFirstLaunchTrigger)가 공유한다.
        /// 프리팹이 없으면 경고 후 null.
        /// </summary>
        public static GameObject SpawnOverlay()
        {
            var prefab = Resources.Load<GameObject>(OverlayResourcePath);
            if (prefab == null)
            {
                Log.Warn($"[FirstLaunch] 오버레이 프리팹 없음: Resources/{OverlayResourcePath} — 스킵(타이틀로). " +
                         "프리팹 추가 시 표시.");
                return null;
            }
            return Object.Instantiate(prefab);
        }
    }
}
