#if UNITY_EDITOR || DEVELOPMENT_BUILD
using UnityEngine;
using UnityEngine.SceneManagement; // SceneManager (타이틀 씬 가드)

namespace LoveAlgo.UI
{
    /// <summary>
    /// [테스트 빌드 전용] 타이틀에서 첫실행 인트로 오버레이를 원할 때 다시 띄우는 디버그 트리거.
    /// <c>UNITY_EDITOR || DEVELOPMENT_BUILD</c>에서만 컴파일 — 릴리스(비-Development) 빌드엔 통째로 빠진다(#if 가드).
    /// <c>[RuntimeInitializeOnLoadMethod]</c>로 자가 스폰(씬 배선 0, <see cref="StoryEditorPanel"/>와 동일 관례),
    /// 타이틀 씬(빌드 인덱스 0)에서 오버레이가 떠 있지 않을 때만 작은 IMGUI 버튼을 노출(모바일 탭/PC 클릭 공통).
    /// <see cref="FirstLaunchFlag"/>는 건드리지 않고 <see cref="FirstLaunchBootstrap.SpawnOverlay"/>만 호출 —
    /// 영속 첫구동 플래그를 보존한 채 인트로 흐름만 재생한다.
    /// </summary>
    public sealed class DevFirstLaunchTrigger : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            var go = new GameObject("DevFirstLaunchTrigger");
            DontDestroyOnLoad(go);
            go.AddComponent<DevFirstLaunchTrigger>();
        }

        GUIStyle _style;

        void OnGUI()
        {
            // 타이틀(진입 씬)에서만, 이미 오버레이가 떠 있지 않을 때만 노출.
            if (SceneManager.GetActiveScene().buildIndex != 0) return;
            if (FindAnyObjectByType<FirstLaunchDirector>() != null) return;

            if (_style == null)
                _style = new GUIStyle(GUI.skin.button) { fontSize = 22 };

            const float w = 240f, h = 72f, pad = 16f;
            if (GUI.Button(new Rect(pad, pad, w, h), "▶ 인트로 다시", _style))
                FirstLaunchBootstrap.SpawnOverlay();
        }
    }
}
#endif
