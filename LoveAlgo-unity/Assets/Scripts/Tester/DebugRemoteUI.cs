using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using LoveAlgo.Story;
using LoveAlgo.UI;
using LoveAlgo.Core;

namespace LoveAlgo.Tester
{
    /// <summary>
    /// 테스터용 디버그 리모콘 UI
    /// 화면 구석에 단축키 안내 표시 + 디버그 전용 기능
    /// 
    /// ※ 스토리 진행 입력(Space/Click, Auto, Skip)은 StoryInputHandler가 처리
    /// </summary>
    public class DebugRemoteUI : MonoBehaviour
    {
        [Header("UI")]
        [SerializeField] GameObject panel;
        [SerializeField] TMP_Text helpText;

        [Header("설정")]
        [SerializeField] bool showOnStart = true;
        #pragma warning disable CS0414
        [SerializeField] KeyCode toggleKey = KeyCode.F1;  // Inspector에서 참조용
        #pragma warning restore CS0414

        bool isVisible;

        const string HelpContent = @"<b>[테스터 리모콘]</b>
<color=#FFD700>F1</color>  리모콘 표시/숨김
<color=#FFD700>F2</color>  데모 점프 헬퍼
<color=#FFD700>F3</color>  상태 모니터 (스탯/호감도)
<color=#aaaaaa>─── 프로덕션 (StoryInputHandler) ───</color>
<color=#88CC88>Space/Click</color>  다음 대사
<color=#88CC88>A</color>  자동 진행 ON/OFF
<color=#88CC88>Shift+S</color>  스킵 모드
<color=#aaaaaa>─── 디버그 전용 ───</color>
<color=#FFD700>R</color>  처음부터 다시
<color=#FFD700>N</color>  다음 10줄 스킵
<color=#FFD700>B</color>  방금 줄 다시 (연출 포함)
<color=#FFD700>V</color>  5줄 전 다시 (연출 포함)";

        void Start()
        {
            SetupUI();
            SetVisible(showOnStart);
        }

        void SetupUI()
        {
            if (helpText != null)
            {
                helpText.text = HelpContent;
            }
        }

        void Update()
        {
            HandleInput();
        }

        void HandleInput()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            // F1: 리모콘 표시/숨김
            if (keyboard.f1Key.wasPressedThisFrame)
            {
                ToggleVisible();
            }

            // 스토리 진행 중일 때만 디버그 기능 처리
            var phase = GameManager.Instance?.CurrentPhase;
            if (phase != GamePhase.Prologue && phase != GamePhase.DayLoop)
                return;

            // R: 처음부터 다시
            if (keyboard.rKey.wasPressedThisFrame)
            {
                RestartScript();
            }

            // N: 다음 10줄 스킵
            if (keyboard.nKey.wasPressedThisFrame)
            {
                SkipLines(10);
            }

            // B: 방금 줄 다시 (1개 Text 전, 연출 포함)
            if (keyboard.bKey.wasPressedThisFrame)
            {
                RewindText(1);
            }

            // V: 5줄 전 다시 (5개 Text 전, 연출 포함)
            if (keyboard.vKey.wasPressedThisFrame)
            {
                RewindText(5);
            }
        }

        #region 디버그 기능

        void RestartScript()
        {
            var runner = ScriptRunner.Instance;
            runner?.Stop();
            
            // UI 초기화
            UIManager.Instance?.DialogueUI?.ShowImmediate();
            StageManager.Instance?.Character?.ClearAll();
            StageManager.Instance?.VirtualBG?.HideImmediate();
            ScreenFX.Instance?.SetClear();
            AudioManager.Instance?.StopBGMImmediate();
            
            runner?.Run();
            PopupManager.Instance?.Toast("Restart", "처음부터 다시 시작");
        }

        void SkipLines(int count)
        {
            var runner = ScriptRunner.Instance;
            if (runner == null) return;

            for (int i = 0; i < count; i++)
            {
                UIManager.Instance?.DialogueUI?.RequestSkip();
                runner.OnClick();
            }
            PopupManager.Instance?.Toast("Skip", $"{count}줄 스킵");
        }

        void RewindText(int textCount)
        {
            var runner = ScriptRunner.Instance;
            if (runner == null) return;

            runner.Rewind(textCount);
            PopupManager.Instance?.Toast("Rewind", $"{textCount}줄 전 다시 재생");
        }

        #endregion

        #region UI

        public void ToggleVisible()
        {
            SetVisible(!isVisible);
        }

        public void SetVisible(bool visible)
        {
            isVisible = visible;
            if (panel != null)
            {
                panel.SetActive(visible);
            }
        }

        #endregion
    }
}
