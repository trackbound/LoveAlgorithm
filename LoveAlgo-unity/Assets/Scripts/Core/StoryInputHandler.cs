using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using LoveAlgo.Story;
using LoveAlgo.UI;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 스토리 진행 입력 핸들러 (프로덕션)
    /// Space / 마우스 클릭 → 다음 대사
    /// A → Auto 모드 토글
    /// Shift 홀드 → 스킵 모드
    /// </summary>
    public class StoryInputHandler : MonoBehaviour
    {
        public static StoryInputHandler Instance { get; private set; }

        // Raycast 결과 캐시
        readonly List<RaycastResult> raycastResults = new();
        PointerEventData pointerEventData;

        void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(gameObject);
        }

        void Update()
        {
            HandleInput();
        }

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;

            // 팝업이 열려 있으면 스토리 입력 차단
            if (PopupManager.Instance != null && PopupManager.Instance.IsAnyPopupOpen)
                return;

            // 스토리 진행 Phase일 때만 처리
            var phase = GameManager.Instance?.CurrentPhase;
            if (phase != GamePhase.Prologue && phase != GamePhase.DayLoop)
                return;

            var runner = ScriptRunner.Instance;
            var dialogueUI = UIManager.Instance?.DialogueUI;

            // ── Space / 마우스 클릭: 다음 대사 ──
            bool spacePressed = keyboard.spaceKey.wasPressedThisFrame;
            bool mouseClicked = mouse != null && mouse.leftButton.wasPressedThisFrame;

            // 마우스 클릭은 버튼 위에서는 무시
            if (mouseClicked && IsPointerOverButton())
                mouseClicked = false;

            if (spacePressed || mouseClicked)
            {
                if (dialogueUI != null && dialogueUI.IsTyping)
                {
                    dialogueUI.RequestSkip();
                }
                else
                {
                    UISoundManager.Instance?.PlayDialogueNext();
                    runner?.OnClick();
                }
            }

            // ── A: 자동 진행 토글 ──
            if (keyboard.aKey.wasPressedThisFrame)
            {
                runner?.ToggleAutoMode();
                var mode = runner?.IsAutoMode == true ? "ON" : "OFF";
                PopupManager.Instance?.Toast("Auto Mode", mode);
            }

            // ── Shift 홀드: 스킵 모드 ──
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            {
                if (keyboard.sKey.wasPressedThisFrame || keyboard.sKey.isPressed)
                {
                    if (dialogueUI != null && dialogueUI.IsTyping)
                        dialogueUI.RequestSkip();
                    runner?.OnClick();
                }
            }
        }

        /// <summary>
        /// 마우스 위치에 Button이 있는지 확인
        /// </summary>
        bool IsPointerOverButton()
        {
            if (EventSystem.current == null) return false;

            var mouse = Mouse.current;
            if (mouse == null) return false;

            pointerEventData ??= new PointerEventData(EventSystem.current);
            pointerEventData.position = mouse.position.ReadValue();

            raycastResults.Clear();
            EventSystem.current.RaycastAll(pointerEventData, raycastResults);

            foreach (var result in raycastResults)
            {
                if (result.gameObject.GetComponent<Button>() != null ||
                    result.gameObject.GetComponentInParent<Button>() != null)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
