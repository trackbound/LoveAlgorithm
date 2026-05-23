using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;
using LoveAlgo.Story;
using LoveAlgo.UI;

namespace LoveAlgo.DevTools
{
    /// <summary>
    /// 빌드용 디버그 패널 (IMGUI).
    /// 패널: F1 = GameState 뷰어/편집, F2 = 점프/리셋 패널.
    /// 인게임 단축키:
    ///   F3  = 고속 모드 토글 (auto+0속도) | F4 = 다음 선택지까지 스킵
    ///   F5  = 퀵세이브 (슬롯 99)         | F9 = 퀵로드
    ///   F6  = 다음 날로 (Day+1)
    ///   F10 = UI 전체 숨김/표시          | F11 = 디버그 오버레이
    ///   Ctrl+S    = 스크린샷 저장
    ///   Shift+F12 = LockScreen 비번 리셋 + 씬 리로드
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        enum PanelMode { None, GameState, Jump }

        const int QuickSaveSlot = 99;

        PanelMode mode;
        Vector2 scrollPosJump;
        Vector2 scrollPosState;
        bool isJumping;

        // ── 토글 상태 ──
        bool fastMode;
        bool uiHidden;
        bool showOverlay;
        bool showHelp;
        Vector2 scrollPosHelp;
        readonly List<Canvas> hiddenCanvases = new List<Canvas>();

        // ── 단축키 치트시트 (Key, Desc) — 그룹 순서대로 ──
        static readonly (string key, string desc)[] HotkeyTable =
        {
            // ── 도움말 ──
            ("F1",            "이 단축키 도움말 토글"),
            // ── 패널 그룹 ──
            ("F2",            "GameState 뷰어/편집 패널 토글"),
            ("F3",            "점프/리셋 패널 토글"),
            // ── 자주 쓰는 도구 (Esc 옆 ` 키 — 콘솔 관습) ──
            ("`",             "시나리오 편집기 토글 (현재 스크립트 라이브 편집)"),
            // ── 스크립트 흐름 그룹 ──
            ("F5",            "다음 날로 진행 [DayLoop 전용, Mark:day{N}* 점프]"),
            ("F6",            "다음 선택지(Choice)까지 점프 [스크립트 실행 중 전용]"),
            ("F7",            "스크립트 고속 진행 토글 [스크립트 실행 중 전용]"),
            // ── 뷰 토글 그룹 ──
            ("F10",           "모든 UI Canvas 숨김/표시 (스크린샷용)"),
            ("F11",           "디버그 오버레이 토글 (FPS / Phase / LineID)"),
            // ── 모디파이어 (안전 그룹 — 모두 Ctrl+Shift+letter) ──
            ("Ctrl+Shift+S",  "퀵세이브 [Prologue/DayLoop 전용, 슬롯 99]"),
            ("Ctrl+Shift+L",  "퀵로드 [어디서나, 슬롯 99]"),
            ("Ctrl+Shift+P",  "스크린샷 저장 (persistentDataPath/dev_screenshots)"),
            ("Ctrl+Shift+R",  "LockScreen 비번 리셋 + 씬 리로드 (첫 실행 재현)"),
        };

        // ── GameState 편집용 임시 값 ──
        string editMoney = "";
        string editStr = "", editInt = "", editSoc = "", editPer = "", editFatigue = "";
        string editDay = "", editActions = "";
        string editLoveRoa = "", editLoveDaeun = "", editLoveYeeun = "";
        string editLoveHeewon = "", editLoveBom = "";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AutoCreate()
        {
            if (FindAnyObjectByType<DebugPanel>() != null) return;
            var go = new GameObject("[DebugPanel]");
            go.AddComponent<DebugPanel>();
            DontDestroyOnLoad(go);
        }

        // ── 점프 포인트 (Label, LineID) ──
        static readonly (string label, string lineId)[] JumpPoints =
        {
            ("Day 1 — 기상",             "DEMO_1"),
            ("Day 1 — 첫 수업 (다은)",    "DEMO_2"),
            ("Day 1 — 캠퍼스 (예은)",     "DBG_CAMPUS"),
            ("Day 1 — 자취방 밤 (로아)",   "DBG_NIGHT"),
            ("Day 2 — 동아리 위기",       "STUDENT"),
            ("Day 2 — 편의점 (희원)",     "DBG_STORE"),
            ("Day 3 — 게시판 (봄)",       "DBG_BOM"),
            ("Day 3 — 로아 엔딩",         "DBG_ENDING"),
        };

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null) return;

            bool ctrl = kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed;
            bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;

            // ── 도움말 (관습: F1) ──
            if (kb.f1Key.wasPressedThisFrame) showHelp = !showHelp;

            // ── 패널 그룹 (F2~F3) ──
            if (kb.f2Key.wasPressedThisFrame)
                mode = mode == PanelMode.GameState ? PanelMode.None : PanelMode.GameState;
            if (kb.f3Key.wasPressedThisFrame)
                mode = mode == PanelMode.Jump ? PanelMode.None : PanelMode.Jump;

            // ── 시나리오 편집기 (` 백틱 — 게임 콘솔 관습 키) ──
            if (kb.backquoteKey.wasPressedThisFrame)
                LoveAlgo.DevTools.ScenarioEditor.ScenarioEditorIMGUI.Instance?.Toggle();

            // ── 스크립트 흐름 그룹 (F5~F7) ──
            if (kb.f5Key.wasPressedThisFrame) AdvanceToNextDay();   // 다음 날
            if (kb.f6Key.wasPressedThisFrame) SkipToNextChoice();   // 다음 선택지
            if (kb.f7Key.wasPressedThisFrame) ToggleFastMode();     // 고속 토글

            // ── 뷰 토글 그룹 (F10~F11) ──
            if (kb.f10Key.wasPressedThisFrame) ToggleUIHide();
            if (kb.f11Key.wasPressedThisFrame) showOverlay = !showOverlay;

            // ── 모디파이어 단축키 (모두 Ctrl+Shift+letter 패턴으로 통일) ──
            if (ctrl && shift)
            {
                if (kb.sKey.wasPressedThisFrame) QuickSave();           // Save
                if (kb.lKey.wasPressedThisFrame) QuickLoad();           // Load
                if (kb.pKey.wasPressedThisFrame) TakeScreenshot();      // Print (screenshot)
                if (kb.rKey.wasPressedThisFrame) ResetToFirstStart();   // Reset (LockScreen + scene)
            }
        }

        void OnGUI()
        {
            switch (mode)
            {
                case PanelMode.GameState:
                    DrawGameStatePanel();
                    break;
                case PanelMode.Jump:
                    DrawJumpPanel();
                    break;
            }

            if (showOverlay) DrawDebugOverlay();
            if (fastMode) DrawFastModeBadge();
            if (uiHidden) DrawUIHiddenBadge();
            if (showHelp) DrawHelpPanel();
            else DrawHelpHint();
        }

        // ══════════════════════════════════════════════
        //  F1: GameState 뷰어/편집
        // ══════════════════════════════════════════════

        void DrawGameStatePanel()
        {
            var gm = GameManager.Instance;
            var gs = GameState.Instance;

            float panelW = 360f;
            float panelH = Mathf.Min(Screen.height * 0.9f, 720f);
            float x = 12f;
            float y = 12f;

            GUI.Box(new Rect(x, y, panelW, panelH), "");
            GUILayout.BeginArea(new Rect(x + 8, y + 8, panelW - 16, panelH - 16));
            scrollPosState = GUILayout.BeginScrollView(scrollPosState);

            var header = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 };
            var section = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label("<b>GameState (F2 닫기)</b>", header);
            GUILayout.Space(4);

            if (gm == null || gs == null)
            {
                GUILayout.Label("GameManager / GameState 없음");
                GUILayout.EndScrollView();
                GUILayout.EndArea();
                return;
            }

            // ── 게임 정보 (읽기) ──
            GUILayout.Label($"Phase: <b>{gm.CurrentPhase}</b>  |  Player: <b>{gm.PlayerName}</b>", section);
            GUILayout.Space(6);
            DrawLine();

            // ── Day / Actions ──
            GUILayout.Label("<b>Day / Actions</b>", section);
            GUILayout.BeginHorizontal();
            GUILayout.Label("Day:", GUILayout.Width(60));
            editDay = GUILayout.TextField(editDay, GUILayout.Width(60));
            if (GUILayout.Button("Set", GUILayout.Width(40)) && int.TryParse(editDay, out int newDay))
                gm.CurrentDay = Mathf.Max(1, newDay);
            GUILayout.Space(12);
            GUILayout.Label("Actions:", GUILayout.Width(60));
            editActions = GUILayout.TextField(editActions, GUILayout.Width(60));
            if (GUILayout.Button("Set", GUILayout.Width(40)) && int.TryParse(editActions, out int newAct))
                gm.RemainingActions = Mathf.Max(0, newAct);
            GUILayout.EndHorizontal();
            GUILayout.Label($"  현재: Day {gm.CurrentDay} / Actions {gm.RemainingActions}", section);
            GUILayout.Space(6);
            DrawLine();

            // ── Money ──
            GUILayout.Label("<b>Money</b>", section);
            GUILayout.BeginHorizontal();
            GUILayout.Label($"현재: {MoneyFormat.Currency(gs.Money)}", GUILayout.Width(160));
            editMoney = GUILayout.TextField(editMoney, GUILayout.Width(80));
            if (GUILayout.Button("Set", GUILayout.Width(40)) && int.TryParse(editMoney, out int newMoney))
            {
                gs.AddMoney(newMoney - gs.Money);
            }
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            DrawLine();

            // ── Stats ──
            GUILayout.Label("<b>Stats</b>", section);
            DrawStatRow("Str (체력)", "Str", ref editStr, gs);
            DrawStatRow("Int (지성)", "Int", ref editInt, gs);
            DrawStatRow("Soc (사교)", "Soc", ref editSoc, gs);
            DrawStatRow("Per (끈기)", "Per", ref editPer, gs);
            DrawStatRow("Fatigue (피로)", "Fatigue", ref editFatigue, gs);
            GUILayout.Space(6);
            DrawLine();

            // ── Love ──
            GUILayout.Label("<b>Love (호감도)</b>", section);
            DrawLoveRow("로아", "Roa", ref editLoveRoa, gs);
            DrawLoveRow("다은", "SeoDaEun", ref editLoveDaeun, gs);
            DrawLoveRow("예은", "HaYeEun", ref editLoveYeeun, gs);
            DrawLoveRow("희원", "DoHeewon", ref editLoveHeewon, gs);
            DrawLoveRow("봄", "LeeBom", ref editLoveBom, gs);
            GUILayout.Space(6);
            DrawLine();

            // ── 빠른 설정 프리셋 ──
            GUILayout.Label("<b>프리셋</b>", section);
            if (GUILayout.Button("100만원 추가"))
                gs.AddMoney(1000000 - gs.Money);
            if (GUILayout.Button("올스탯 MAX"))
            {
                int max = GameConstants.MaxStat;
                gs.SetStat("Str", max);
                gs.SetStat("Int", max);
                gs.SetStat("Soc", max);
                gs.SetStat("Per", max);
                gs.SetStat("Fatigue", 0);
            }
            if (GUILayout.Button("전체 초기화"))
            {
                gs.ResetAll();
                gm.CurrentDay = 1;
                gm.RemainingActions = GameConstants.ActionsPerDay;
            }

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawStatRow(string label, string statName, ref string editField, GameState gs)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"  {label}: {gs.GetStat(statName)}", GUILayout.Width(180));
            editField = GUILayout.TextField(editField, GUILayout.Width(60));
            if (GUILayout.Button("Set", GUILayout.Width(40)) && int.TryParse(editField, out int val))
                gs.SetStat(statName, val);
            GUILayout.EndHorizontal();
        }

        void DrawLoveRow(string displayName, string id, ref string editField, GameState gs)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"  {displayName}: {gs.GetLove(id)}", GUILayout.Width(180));
            editField = GUILayout.TextField(editField, GUILayout.Width(60));
            if (GUILayout.Button("Set", GUILayout.Width(40)) && int.TryParse(editField, out int val))
                gs.SetLove(id, val);
            GUILayout.EndHorizontal();
        }

        // ══════════════════════════════════════════════
        //  F2: 점프 / 리셋
        // ══════════════════════════════════════════════

        void DrawJumpPanel()
        {
            float panelW = 340f;
            float panelH = Mathf.Min(Screen.height * 0.85f, 720f);
            float x = Screen.width - panelW - 12f;
            float y = 12f;

            GUI.Box(new Rect(x, y, panelW, panelH), "");

            GUILayout.BeginArea(new Rect(x + 8, y + 8, panelW - 16, panelH - 16));
            scrollPosJump = GUILayout.BeginScrollView(scrollPosJump);

            var gm = GameManager.Instance;
            var gs = GameState.Instance;

            var header = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 15 };
            var section = new GUIStyle(GUI.skin.label) { richText = true };
            GUILayout.Label("<b>Debug Panel</b>  (F3 닫기)", header);
            var hintStyle = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 10, wordWrap = true };
            GUILayout.Label(
                "<color=#aaaaaa>F1 도움말  F2 상태  F3 점프  ` 편집기\n" +
                "F5 →날  F6 →선택지  F7 고속  F10 UI숨김  F11 오버레이\n" +
                "Ctrl+Shift+ S 세이브 / L 로드 / P 스샷 / R 첫실행리셋</color>", hintStyle);
            GUILayout.Space(6);

            // ── 현재 상태 ──
            if (gm != null)
            {
                GUILayout.Label($"Phase: {gm.CurrentPhase}  |  Day: {gm.CurrentDay}  |  Actions: {gm.RemainingActions}");
                if (gs != null)
                    GUILayout.Label($"Money: {gs.Money}  |  Fatigue: {gs.GetStat("Fatigue")}");
            }

            GUILayout.Space(8);
            DrawLine();

            // ── Phase 점프 ──
            GUILayout.Label("<b>Phase 점프</b>", section);
            GUILayout.Space(4);

            if (GUILayout.Button("→ 타이틀"))
                GameManager.Instance?.GoToTitle();

            if (GUILayout.Button("→ 유저네임 입력"))
                GameManager.Instance?.Flow?.ChangePhase(GamePhase.Username);

            if (GUILayout.Button("→ 프롤로그 (처음부터)"))
            {
                var flow = GameManager.Instance?.Flow;
                if (flow != null)
                {
                    ScriptRunner.Instance?.Stop();
                    GameManager.Instance?.CleanupStage();
                    if (string.IsNullOrEmpty(gm?.PlayerName))
                        gm?.SetPlayerName("테스트");
                    GameState.Instance?.SetPlayerName(gm?.PlayerName);
                    flow.ChangePhase(GamePhase.Prologue);
                    ScriptRunner.Instance?.StartScript(gm.PrologueScript).Forget();
                }
            }

            if (GUILayout.Button("→ 스케줄 (DayLoop)"))
                GameManager.Instance?.SkipToDayLoop();

            GUILayout.Space(8);
            DrawLine();

            // ── Mark 자동 점프 (CSV의 `Flow,,Mark:label,>`에서 자동 수집) ──
            var marks = MarkRegistry.All;
            if (marks != null && marks.Count > 0)
            {
                GUILayout.Label($"<b>Mark 점프</b> <color=#888888>({marks.Count}개)</color>", section);
                GUILayout.Space(4);
                GUI.enabled = !isJumping;
                foreach (var (idx, label) in marks)
                {
                    if (GUILayout.Button($"→ {label}"))
                        JumpToMarkAsync(label, idx);
                }
                GUI.enabled = true;
                GUILayout.Space(8);
                DrawLine();
            }

            // ── 프롤로그 점프 포인트 (수동 등록 — Mark 없는 라인 ID로 점프 시 사용) ──
            GUILayout.Label("<b>프롤로그 점프 (수동)</b>", section);
            GUILayout.Space(4);

            GUI.enabled = !isJumping;
            foreach (var (label, lineId) in JumpPoints)
            {
                if (GUILayout.Button(label))
                    JumpAsync(lineId);
            }
            GUI.enabled = true;

            GUILayout.Space(8);
            DrawLine();

            // ── 리셋 ──
            GUILayout.Label("<b>리셋</b>", section);
            GUILayout.Space(4);

            if (GUILayout.Button("전체 초기화"))
            {
                if (gs != null && gm != null)
                {
                    gs.ResetAll();
                    gm.CurrentDay = 1;
                    gm.RemainingActions = GameConstants.ActionsPerDay;
                }
            }

            if (GUILayout.Button("인벤토리 전체 클리어"))
                Shop.ShopManager.Reset();

            if (GUILayout.Button("잠금화면 비번 리셋 + 씬 리로드 (첫 실행 재현)  [Ctrl+Shift+R]"))
                ResetToFirstStart();

            GUILayout.Space(8);
            DrawLine();
            GUILayout.Label("<b>로그</b>", section);
            GUILayout.Space(4);
            bool verbose = StageSyncLog.Verbose;
            string vlabel = verbose
                ? "<color=#88ff88>● 무대 동기화 verbose 로그 ON</color> (클릭 → 끄기)"
                : "○ 무대 동기화 verbose 로그 OFF (클릭 → 켜기)";
            var vstyle = new GUIStyle(GUI.skin.button) { richText = true };
            if (GUILayout.Button(vlabel, vstyle))
                StageSyncLog.ToggleVerbose();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ══════════════════════════════════════════════
        //  첫 실행 재현 (LockScreen 비번 리셋 + 씬 리로드)
        // ══════════════════════════════════════════════

        void ResetToFirstStart()
        {
            var ls = LoveAlgo.Common.Services.TryGet<LoveAlgo.LockScreen.ILockScreen>()
                     as LoveAlgo.LockScreen.LockScreenController;
            if (ls != null)
            {
                ls.ClearPassword();
            }
            else
            {
                PlayerPrefs.DeleteKey("lock_screen.password_hash");
                PlayerPrefs.DeleteKey("lock_screen.password_salt");
                PlayerPrefs.Save();
            }

            GameState.Instance?.ResetAll();

            mode = PanelMode.None;
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(active.buildIndex);
        }

        // ══════════════════════════════════════════════
        //  단축키 핸들러 (F3~F11, Ctrl+S)
        // ══════════════════════════════════════════════

        void ToggleFastMode()
        {
            var runner = ScriptRunner.Instance;
            if (runner == null) return;
            if (!runner.IsRunning)
            {
                Debug.LogWarning("[DebugPanel] 고속 토글 무시 — 스크립트 실행 중이 아닙니다.");
                return;
            }
            fastMode = !fastMode;
            if (fastMode)
            {
                runner.SetAutoDelay(1f);
                runner.SetAutoMode(true);
                Debug.Log("[DebugPanel] 고속 모드 ON");
            }
            else
            {
                runner.SetAutoMode(false);
                runner.SetAutoDelay(PlayerPrefs.GetFloat("AutoSpeed", 0.5f));
                Debug.Log("[DebugPanel] 고속 모드 OFF");
            }
        }

        void SkipToNextChoice()
        {
            var runner = ScriptRunner.Instance;
            if (runner == null || !runner.IsRunning)
            {
                Debug.LogWarning("[DebugPanel] 스크립트 실행 중 아님");
                return;
            }
            int total = runner.LineCount;
            for (int i = runner.CurrentIndex + 1; i < total; i++)
            {
                var line = runner.GetLine(i);
                if (line != null && line.Type == LineType.Choice)
                {
                    Debug.Log($"[DebugPanel] 다음 선택지로 점프 (무대 동기화): index {i} ({line.LineID})");
                    runner.JumpWithStateSyncAsync(i).Forget();
                    return;
                }
            }
            Debug.LogWarning("[DebugPanel] 현재 스크립트에 다음 선택지 없음");
        }

        void QuickSave()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            var phase = gm.CurrentPhase;
            if (phase != GamePhase.Prologue && phase != GamePhase.DayLoop)
            {
                StageSyncLog.Warn("QuickSave", $"무시 — 현재 페이즈 '{phase}'는 저장 대상 아님 (Prologue/DayLoop만)");
                return;
            }
            var runner = ScriptRunner.Instance;
            string script = runner?.CurrentScriptName ?? "-";
            int lineIdx = runner?.CurrentIndex ?? -1;
            string lineId = runner?.CurrentLine?.LineID ?? "-";
            StageSyncLog.Info("QuickSave", $"phase={phase} script={script} line={lineIdx} lineId={lineId}");

            gm.Save(QuickSaveSlot, usePendingThumbnail: false, customLabel: "[QuickSave]");
        }

        void QuickLoad()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;
            if (gm.CurrentPhase == GamePhase.Transitioning)
            {
                StageSyncLog.Warn("QuickLoad", "무시 — 페이즈 전환 중");
                return;
            }
            if (!SaveManager.Exists(QuickSaveSlot))
            {
                StageSyncLog.Warn("QuickLoad", $"슬롯 {QuickSaveSlot} 비어있음 — 먼저 Ctrl+Shift+S로 저장하세요");
                return;
            }
            // 미리 로드해서 타겟 정보 로그
            var preview = SaveManager.Load(QuickSaveSlot);
            if (preview != null)
                StageSyncLog.Info("QuickLoad",
                    $"target script={preview.ScriptName ?? "-"} line={preview.LineIndex} lineId={preview.LineId ?? "-"} " +
                    $"phase={preview.Phase} day={preview.CurrentDay} BG={preview.CurrentBG ?? "-"} BGM={preview.CurrentBGM ?? "-"}");

            gm.LoadGame(QuickSaveSlot);
        }

        /// <summary>
        /// F6 — Day 카운터 +1 + 가장 가까운 `day{N}*` Mark 라인으로 점프.
        ///
        /// CSV 작성 컨벤션:
        ///   `,Flow,,Mark:day1_morning,>`  / `,Flow,,Mark:day1_school,>` / `,Flow,,Mark:day2_morning,>` ...
        ///
        /// 처리 순서:
        ///   1. GameManager.AdvanceDay() — CurrentDay++, RemainingActions 리셋
        ///   2. MarkRegistry에서 `day{newDay}` prefix 일치하는 Mark 검색
        ///   3. 발견 시 ScriptRunner.JumpWithStateSyncAsync — 무대 자동 합성
        ///   4. 없으면 경고 (Mark가 안 박혀있다는 뜻)
        /// </summary>
        void AdvanceToNextDay()
        {
            var gm = GameManager.Instance;
            var runner = ScriptRunner.Instance;
            if (gm == null) return;

            if (gm.CurrentPhase != GamePhase.DayLoop)
            {
                StageSyncLog.Warn("NextDay", $"무시 — 현재 페이즈 '{gm.CurrentPhase}'에서는 의미 없음 (DayLoop 전용)");
                return;
            }

            int prevDay = gm.CurrentDay;
            // 1) 카운터 진행
            gm.AdvanceDay();
            int newDay = gm.CurrentDay;
            StageSyncLog.Section("NextDay", $"day {prevDay} → {newDay}");

            // 2) Mark 검색 — `day{newDay}` prefix 우선 (예: day2_morning)
            string prefix = $"day{newDay}";
            int targetIdx = FindMarkIndexByPrefix(prefix);

            // 3) 없으면 현재 위치 이후 가장 가까운 Mark로 폴백
            if (targetIdx < 0 && runner != null)
            {
                int curr = runner.CurrentIndex;
                targetIdx = FindNextMarkAfter(curr);
            }

            if (targetIdx < 0 || runner == null)
            {
                StageSyncLog.Warn("NextDay", $"Day {newDay} Mark 없음 (`{prefix}*` 컨벤션) — 카운터만 +1");
                return;
            }

            StageSyncLog.Info("NextDay", $"phase=DayLoop day={prevDay}→{newDay}, target=line {targetIdx + 1}");
            // 같은 스크립트 내 점프지만 안전한 일관성 위해 GameFlowJumper 사용
            string scriptName = runner.CurrentScriptName ?? "Prologue";
            GameFlowJumper.JumpToScriptAsync(scriptName, targetIdx + 1, GamePhase.DayLoop).Forget();
        }

        /// <summary>주어진 prefix로 시작하는 첫 Mark의 인덱스. 없으면 -1.</summary>
        static int FindMarkIndexByPrefix(string prefix)
        {
            var marks = MarkRegistry.All;
            if (marks == null) return -1;
            foreach (var (idx, label) in marks)
                if (label != null && label.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                    return idx;
            return -1;
        }

        /// <summary>currentIndex 이후 첫 Mark 인덱스. 없으면 -1.</summary>
        static int FindNextMarkAfter(int currentIndex)
        {
            var marks = MarkRegistry.All;
            if (marks == null) return -1;
            foreach (var (idx, _) in marks)
                if (idx > currentIndex) return idx;
            return -1;
        }

        void ToggleUIHide()
        {
            uiHidden = !uiHidden;
            if (uiHidden)
            {
                hiddenCanvases.Clear();
                var all = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Exclude);
                foreach (var c in all)
                {
                    if (c == null || !c.isRootCanvas || !c.enabled) continue;
                    // DebugPanel은 IMGUI라 Canvas 아님 — 안전
                    c.enabled = false;
                    hiddenCanvases.Add(c);
                }
                Debug.Log($"[DebugPanel] UI 숨김 ({hiddenCanvases.Count}개 Canvas)");
            }
            else
            {
                foreach (var c in hiddenCanvases)
                    if (c != null) c.enabled = true;
                hiddenCanvases.Clear();
                Debug.Log("[DebugPanel] UI 표시 복원");
            }
        }

        void TakeScreenshot()
        {
            string dir = System.IO.Path.Combine(Application.persistentDataPath, "dev_screenshots");
            System.IO.Directory.CreateDirectory(dir);
            string name = $"screenshot_{System.DateTime.Now:yyyyMMdd_HHmmss}.png";
            string path = System.IO.Path.Combine(dir, name);
            ScreenCapture.CaptureScreenshot(path);
            Debug.Log($"[DebugPanel] 스크린샷 저장 → {path}");
        }

        // ══════════════════════════════════════════════
        //  오버레이 / 상태 뱃지
        // ══════════════════════════════════════════════

        void DrawDebugOverlay()
        {
            var gm = GameManager.Instance;
            var runner = ScriptRunner.Instance;

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                richText = true,
                normal = { textColor = Color.yellow }
            };

            const float W = 380f;
            const float H = 110f;
            float x = 6f;
            float y = Screen.height - H - 6f;

            GUI.Box(new Rect(x, y, W, H), "");
            GUILayout.BeginArea(new Rect(x + 6, y + 4, W - 12, H - 8));

            float fps = Time.smoothDeltaTime > 0f ? 1f / Time.smoothDeltaTime : 0f;
            GUILayout.Label($"<b>FPS:</b> {Mathf.RoundToInt(fps)}", style);

            if (gm != null)
                GUILayout.Label($"<b>Phase:</b> {gm.CurrentPhase}   <b>Day:</b> {gm.CurrentDay}   <b>Act:</b> {gm.RemainingActions}", style);

            if (runner != null && runner.IsRunning)
            {
                var line = runner.CurrentLine;
                GUILayout.Label($"<b>Script:</b> {runner.CurrentScriptName ?? "-"}  [idx {runner.CurrentIndex}]", style);
                GUILayout.Label($"<b>Line:</b> {line?.LineID ?? "-"}  ({line?.Type})", style);
            }

            GUILayout.EndArea();
        }

        void DrawFastModeBadge()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
            var rect = new Rect(Screen.width - 160, 4, 156, 22);
            GUI.Box(rect, "");
            GUI.Label(new Rect(rect.x + 8, rect.y + 2, rect.width - 16, rect.height), "<color=#ffaa00><b>▶▶ FAST MODE (F3)</b></color>", style);
        }

        void DrawUIHiddenBadge()
        {
            var style = new GUIStyle(GUI.skin.label) { fontSize = 12, richText = true };
            var rect = new Rect(Screen.width - 160, 28, 156, 20);
            GUI.Box(rect, "");
            GUI.Label(new Rect(rect.x + 8, rect.y + 2, rect.width - 16, rect.height), "<color=#88ccff>UI HIDDEN (F10)</color>", style);
        }

        // ══════════════════════════════════════════════
        //  F12: 단축키 도움말
        // ══════════════════════════════════════════════

        void DrawHelpHint()
        {
            // 우하단 작은 안내 — "F12로 단축키 보기"
            var style = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true, alignment = TextAnchor.MiddleRight };
            var rect = new Rect(Screen.width - 200, Screen.height - 22, 196, 18);
            GUI.Label(rect, "<color=#888888>F1 — 단축키 도움말</color>", style);
        }

        void DrawHelpPanel()
        {
            const float W = 540f;
            float H = Mathf.Min(Screen.height * 0.8f, 60f + HotkeyTable.Length * 24f);
            float x = (Screen.width - W) * 0.5f;
            float y = (Screen.height - H) * 0.5f;

            // 배경 (반투명 어둡게)
            var prevColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.85f);
            GUI.DrawTexture(new Rect(x, y, W, H), Texture2D.whiteTexture);
            GUI.color = prevColor;
            GUI.Box(new Rect(x, y, W, H), "");

            GUILayout.BeginArea(new Rect(x + 16, y + 12, W - 32, H - 24));

            var header = new GUIStyle(GUI.skin.label) { richText = true, fontSize = 16, alignment = TextAnchor.MiddleCenter };
            GUILayout.Label("<b>개발 단축키 — Dev Hotkeys</b>", header);
            GUILayout.Space(8);

            scrollPosHelp = GUILayout.BeginScrollView(scrollPosHelp);

            var keyStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true, fontSize = 13, alignment = TextAnchor.MiddleLeft,
                normal = { textColor = new Color(1f, 0.85f, 0.4f) }
            };
            var descStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true, fontSize = 13, alignment = TextAnchor.MiddleLeft, wordWrap = true,
                normal = { textColor = Color.white }
            };

            foreach (var (key, desc) in HotkeyTable)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(key, keyStyle, GUILayout.Width(110));
                GUILayout.Label(desc, descStyle);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            GUILayout.EndScrollView();

            GUILayout.Space(6);
            var footStyle = new GUIStyle(GUI.skin.label)
            {
                richText = true, fontSize = 11, alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.7f, 0.7f, 0.7f) }
            };
            GUILayout.Label("F1 다시 눌러 닫기", footStyle);

            GUILayout.EndArea();
        }

        // ══════════════════════════════════════════════
        //  점프 실행
        // ══════════════════════════════════════════════

        /// <summary>
        /// Mark 라벨로 점프 — GameFlowJumper에 위임.
        /// Mark 라인 다음 인덱스로 진입 (Mark는 메타 라인, no-op).
        /// </summary>
        void JumpToMarkAsync(string label, int markIndex)
        {
            var gm = GameManager.Instance;
            var runner = ScriptRunner.Instance;
            if (gm == null || runner == null || isJumping) return;

            int targetIndex = Mathf.Min(markIndex + 1, runner.LineCount - 1);
            if (targetIndex < 0) return;

            isJumping = true;
            mode = PanelMode.None;

            string scriptName = runner.CurrentScriptName ?? gm.PrologueScript;
            var phase = GameFlowJumper.InferPhaseFromScript(scriptName);

            Debug.Log($"[DebugPanel] Mark 점프 → '{label}' (line {targetIndex})");
            DoJumpToIndexAsync(scriptName, targetIndex, phase).Forget();
        }

        async UniTaskVoid DoJumpToIndexAsync(string scriptName, int index, GamePhase phase)
        {
            try
            {
                await GameFlowJumper.JumpToScriptAsync(scriptName, index, phase);
            }
            finally
            {
                isJumping = false;
            }
        }

        void JumpAsync(string lineId)
        {
            var gm = GameManager.Instance;
            if (gm == null || isJumping) return;

            isJumping = true;
            mode = PanelMode.None;
            DoJumpByLineIdAsync(gm.PrologueScript, lineId).Forget();
        }

        async UniTaskVoid DoJumpByLineIdAsync(string scriptName, string lineId)
        {
            try
            {
                await GameFlowJumper.JumpToScriptByLineIdAsync(scriptName, lineId, GamePhase.Prologue);
            }
            finally
            {
                isJumping = false;
            }
        }

        static void DrawLine()
        {
            GUILayout.Box("", GUILayout.ExpandWidth(true), GUILayout.Height(1));
            GUILayout.Space(4);
        }
    }
}
