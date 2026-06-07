using UnityEditor;
using LoveAlgo.Common; // DebugInput

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// 입력 디버그 로깅 전역 토글 메뉴(Tools/Debug/Input Logging, 체크 표시). 상태를 EditorPrefs(<see cref="DebugInput.PrefKey"/>)에
    /// 영속하고, 도메인 리로드/플레이 진입 후에도 <see cref="InitializeOnLoadAttribute"/> 정적 생성자가 <see cref="DebugInput.Enabled"/>를
    /// 복원한다(정적 필드는 리로드 시 리셋되므로). 플레이 중엔 단축키(F8, DebugInputHotkey)로도 토글 — 같은 PrefKey 공유.
    /// </summary>
    [InitializeOnLoad]
    static class DebugInputMenu
    {
        const string MenuPath = "Tools/Debug/Input Logging";

        // 에디터 로드/도메인 리로드/플레이 진입 직후마다 실행 → 정적 플래그 복원.
        static DebugInputMenu() => DebugInput.Enabled = EditorPrefs.GetBool(DebugInput.PrefKey, false);

        [MenuItem(MenuPath, priority = 1000)]
        static void Toggle()
        {
            DebugInput.Enabled = !DebugInput.Enabled;
            EditorPrefs.SetBool(DebugInput.PrefKey, DebugInput.Enabled);
        }

        [MenuItem(MenuPath, validate = true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, DebugInput.Enabled);
            return true;
        }
    }
}
