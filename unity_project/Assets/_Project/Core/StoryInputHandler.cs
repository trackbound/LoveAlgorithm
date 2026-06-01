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
    /// Shift 꾹 → 빠른 재생 (스킵+자동진행, 쿨다운 적용)
    /// Ctrl 꾹 → 초고속 재생
    /// </summary>
    public class StoryInputHandler : SingletonMonoBehaviour<StoryInputHandler>
    {

        [Header("Shift 스킵 설정")]
        [SerializeField] float skipInterval = 0.08f;  // Shift 스킵 간격 (초)

        [Header("Ctrl 초고속 스킵 설정")]
        [SerializeField] float ctrlSkipInterval = 0.02f;  // Ctrl 스킵 간격 (초)

        float lastSkipTime;

        // Raycast 결과 캐시
        readonly List<RaycastResult> raycastResults = new();
        PointerEventData pointerEventData;

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
            if (PopupSystem.Instance != null && PopupSystem.Instance.IsAnyPopupOpen)
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
                else if (runner != null && runner.IsWaitingForClick)
                {
                    LoveAlgo.Modules.Audio.AudioManager.Instance?.PlayDialogueNext();
                    runner.OnClick();
                }
            }

            // ── A: 자동 진행 토글 ──
            if (keyboard.aKey.wasPressedThisFrame)
            {
                runner?.ToggleAutoMode();
                var mode = runner?.IsAutoMode == true ? "ON" : "OFF";
                PopupSystem.Instance?.Toast("Auto Mode", mode);
            }

            // ── Ctrl 꾹: 초고속 재생 (Shift보다 빠름) ──
            bool ctrlPressed = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
            if (ctrlPressed)
            {
                if (Time.unscaledTime - lastSkipTime >= ctrlSkipInterval)
                {
                    lastSkipTime = Time.unscaledTime;

                    if (dialogueUI != null && dialogueUI.IsTyping)
                        dialogueUI.RequestSkip();
                    else
                        runner?.OnClick();
                }
                return; // Ctrl 우선, Shift 중복 방지
            }

            // ── Shift 꾹: 빠른 재생 (쿨다운 적용) ──
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
            {
                if (Time.unscaledTime - lastSkipTime >= skipInterval)
                {
                    lastSkipTime = Time.unscaledTime;

                    if (dialogueUI != null && dialogueUI.IsTyping)
                        dialogueUI.RequestSkip();
                    else
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
