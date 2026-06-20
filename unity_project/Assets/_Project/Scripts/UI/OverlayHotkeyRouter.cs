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
    ///
    /// <para>한 프레임 가드: 키는 <b>직전 프레임에 이미 떠 있던</b> 오버레이에만 전달한다. 이름 입력 Enter가
    /// 입력칸 onSubmit으로 확인 모달을 여는 바로 그 프레임에, 같은 Enter가 라우터의 ConfirmTop(=예)까지 먹어
    /// 모달이 뜨자마자 확정되던 버그를 막는다(모달은 한 번 더 Enter/클릭해야 확정).</para>
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

        // 직전 프레임 종료 시점의 차단 상태. 이번 프레임에 "막 열린" 오버레이를, 그걸 연 바로 그 키가
        // 같은 프레임에 다시 작동시키지 않도록 한다(예: 이름 입력 Enter가 onSubmit으로 확인 모달을 열면서
        // 동시에 그 모달의 ConfirmTop=예까지 먹어 곧장 넘어가던 버그). 키는 "이전부터 떠 있던" 오버레이에만 전달.
        bool _wasBlockedLastFrame;

        void Update()
        {
            var kb = Keyboard.current;
            bool blockedNow = OverlayGate.IsBlocked;
            if (kb != null && _wasBlockedLastFrame) // 이번 프레임에 새로 열린 오버레이엔 키를 전달하지 않음
            {
                if (kb.escapeKey.wasPressedThisFrame)
                    OverlayGate.CloseTop();
                else if (kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame)
                    OverlayGate.ConfirmTop();
            }
            _wasBlockedLastFrame = blockedNow;
        }
    }
}
