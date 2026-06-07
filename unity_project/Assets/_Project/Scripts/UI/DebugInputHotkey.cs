#if UNITY_EDITOR || DEVELOPMENT_BUILD
using LoveAlgo.Common; // DebugInput, Log
using UnityEngine;
using UnityEngine.InputSystem; // Keyboard

namespace LoveAlgo.UI
{
    /// <summary>
    /// 플레이 중 입력 로깅(<see cref="DebugInput"/>)을 단축키 <b>F8</b>로 토글. 씬 배선 없이 자가 스폰
    /// (<see cref="RuntimeInitializeOnLoadMethodAttribute"/>) — 어느 씬에서든 동작. 토글 시 상태를 항상 콘솔에 알린다.
    ///
    /// **에디터/개발 빌드 전용** — 파일 전체가 <c>#if UNITY_EDITOR || DEVELOPMENT_BUILD</c>라 **릴리즈(프로덕션)
    /// 빌드에선 컴파일 자체가 제외**된다(스폰·단축키 없음). 에디터에선 EditorPrefs와 동기화(메뉴 토글과 일치).
    /// </summary>
    sealed class DebugInputHotkey : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("[DebugInputHotkey]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<DebugInputHotkey>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.f8Key.wasPressedThisFrame) return;

            DebugInput.Enabled = !DebugInput.Enabled;
            Log.Info($"[Input] 입력 로깅 {(DebugInput.Enabled ? "ON" : "OFF")} (F8)"); // 토글 알림은 항상 표시(게이트 무관).
#if UNITY_EDITOR
            UnityEditor.EditorPrefs.SetBool(DebugInput.PrefKey, DebugInput.Enabled); // 메뉴 체크와 동기화.
#endif
        }
    }
}
#endif
