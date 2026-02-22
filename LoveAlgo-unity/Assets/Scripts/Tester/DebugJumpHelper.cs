using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;
using LoveAlgo.Story;
using LoveAlgo.UI;

namespace LoveAlgo.Tester
{
    /// <summary>
    /// QA/개발용 데모 점프 헬퍼
    /// 빌드본에서 특정 위치로 빠르게 점프할 수 있는 UI 제공
    /// </summary>
    public class DebugJumpHelper : MonoBehaviour
    {
        [Header("UI 설정")]
        [SerializeField] GameObject panelRoot;
        [SerializeField] Transform buttonContainer;
        [SerializeField] Button buttonPrefab;
        [SerializeField] Button closeButton;
        
        [Header("점프 위치 목록")]
        [SerializeField] List<JumpPoint> jumpPoints = new()
        {
            new JumpPoint("프롤로그 시작", "Prologue", null),  // 처음부터
            new JumpPoint("로아 소개 합류", "Prologue", "roa_intro"),
            new JumpPoint("1일차 아침", "Prologue", "DEMO_day1_morning"),
            new JumpPoint("캠퍼스(강의실)", "Prologue", "DEMO_campus"),
            new JumpPoint("하예은 첫 만남", "Prologue", "yeun_intro"),
            new JumpPoint("서다은 매점", "Prologue", "DEMO_daeun_bookstore"),
            new JumpPoint("학생회관", "Prologue", "DEMO_student_center"),
            new JumpPoint("하예은 CG(입부신청)", "Prologue", "DEMO_yeun_cg"),
            new JumpPoint("희원 첫만남", "Prologue", "DEMO_heewon_first"),
            new JumpPoint("봄 첫만남", "Prologue", "DEMO_bom_first"),
            new JumpPoint("프롤로그 END", "Prologue", "DEMO_prologue_end"),
        };

        bool isVisible;
        
        [Serializable]
        public class JumpPoint
        {
            public string displayName;
            public string scriptName;
            public string lineId;  // null이면 스크립트 처음부터
            
            public JumpPoint(string display, string script, string line)
            {
                displayName = display;
                scriptName = script;
                lineId = line;
            }
        }

        void Awake()
        {
            // 개발 빌드에서만 활성화
#if !DEVELOPMENT_BUILD && !UNITY_EDITOR
            gameObject.SetActive(false);
            return;
#endif
            
            if (panelRoot != null)
                panelRoot.SetActive(false);
                
            closeButton?.onClick.AddListener(Hide);
            
            CreateButtons();
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;
            
            // F2: 점프 헬퍼 토글
            if (keyboard.f2Key.wasPressedThisFrame)
            {
                Toggle();
            }
        }

        void Toggle()
        {
            if (isVisible)
                Hide();
            else
                Show();
        }

        void Show()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(true);
                isVisible = true;
            }
        }

        void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
                isVisible = false;
            }
        }

        void CreateButtons()
        {
            if (buttonContainer == null || buttonPrefab == null) return;
            
            foreach (var point in jumpPoints)
            {
                var btn = Instantiate(buttonPrefab, buttonContainer);
                btn.gameObject.SetActive(true);
                
                var text = btn.GetComponentInChildren<TMP_Text>();
                if (text != null)
                    text.text = point.displayName;
                    
                // 클로저 캡처
                var scriptName = point.scriptName;
                var lineId = point.lineId;
                
                btn.onClick.AddListener(() => JumpTo(scriptName, lineId));
            }
        }

        void JumpTo(string scriptName, string lineId)
        {
            Hide();
            JumpToInternal(scriptName, lineId);
        }

        void JumpToInternal(string scriptName, string lineId)
        {
            // 0. 이전 BGM 정리
            AudioManager.Instance?.StopBGMImmediate();
            
            // 1. 게임 상태 초기화 (기본값)
            GameState.Instance?.ResetAll();
            
            // 2. UI 정리
            UIManager.Instance?.ShowOnly(MainUIType.Dialogue);
            UIManager.Instance?.DialogueUI?.ShowImmediate();  // CanvasGroup alpha 복원
            
            // 3. 화면 효과 초기화
            var fx = ScreenFX.Instance;
            if (fx != null)
            {
                fx.SetClear();  // 즉시 밝게
            }
            
            // 4. 스테이지 정리
            var stage = StageManager.Instance;
            if (stage != null)
            {
                stage.Character?.ClearAll();
                stage.VirtualBG?.HideImmediate();
            }
            
            // 5. 스크립트 로드 및 실행
            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                // 스크립트 로드
                var asset = Resources.Load<TextAsset>($"Story/{scriptName}");
                if (asset == null)
                {
                    Debug.LogError($"[DebugJumpHelper] 스크립트 '{scriptName}'를 찾을 수 없습니다.");
                    return;
                }
                runner.LoadScript(asset);
                
                if (!string.IsNullOrEmpty(lineId))
                {
                    // 특정 LineID부터 실행
                    runner.RunFrom(lineId);
                }
                else
                {
                    // 처음부터 실행
                    runner.Run();
                }
            }
            
            Debug.Log($"[DebugJumpHelper] 점프: {scriptName} → {lineId ?? "(처음부터)"}");
        }

#if UNITY_EDITOR
        [ContextMenu("자동 UI 생성")]
        void CreateDefaultUI()
        {
            // 에디터에서 빠르게 UI 생성용
            Debug.Log("프리팹에서 직접 UI를 구성하세요.");
        }
#endif
    }
}
