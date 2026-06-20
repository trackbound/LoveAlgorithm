using LoveAlgo.Core; // OverlayGate
using UnityEngine;
using UnityEngine.InputSystem; // Keyboard

namespace LoveAlgo.UI
{
    /// <summary>
    /// 오버레이(설정/세이브로드/Extra·모달) 공용 키보드 라우터. 씬 배선 없이 자가 스폰
    /// (<see cref="RuntimeInitializeOnLoadMethodAttribute"/> — DebugInputHotkey 미러)해 어느 씬에서든 1개만 동작한다.
    ///
    /// 역할: <b>ESC = 최상단 닫기</b>(<see cref="OverlayGate.CloseTop"/>) · <b>Enter = 최상단 확정</b>
    /// (<see cref="OverlayGate.ConfirmTop"/>). 입력은 항상 게이트 <b>최상단 1개</b>로만 라우팅되므로 팝업이
    /// 여러 겹 떠 있어도 중복 처리되지 않는다(설정 위에 확인 모달 → ESC는 모달만 닫음). 게이트가 비어 있으면
    /// (오버레이 없음) 아무 키도 가로채지 않아 게임플레이 입력과 충돌하지 않는다.
    ///
    /// 의미는 각 오버레이가 게이트 등록 시 정한다: 설정/세이브로드/Extra는 닫기액션만(Enter 무동작),
    /// yes/no 모달은 닫기=아니오·확인=예(ModalView). 라우터는 어느 버튼인지 모르고 "최상단에 ESC/Enter"만 전달.
    /// </summary>
    sealed class OverlayHotkeyRouter : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Spawn()
        {
            var go = new GameObject("[OverlayHotkeyRouter]") { hideFlags = HideFlags.HideAndDontSave };
            DontDestroyOnLoad(go);
            go.AddComponent<OverlayHotkeyRouter>();
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;
            if (!OverlayGate.IsBlocked) return; // 오버레이 없을 땐 키를 가로채지 않음(게임플레이 입력 보호).

            if (kb.escapeKey.wasPressedThisFrame)
                OverlayGate.CloseTop();
            else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                OverlayGate.ConfirmTop();
        }
    }
}
