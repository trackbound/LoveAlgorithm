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
    /// F1 = GameState 뷰어/편집, F2 = 점프/리셋 패널.
    /// </summary>
    public class DebugPanel : MonoBehaviour
    {
        enum PanelMode { None, GameState, Jump }

        PanelMode mode;
        Vector2 scrollPosJump;
        Vector2 scrollPosState;
        bool isJumping;

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

            if (kb.f1Key.wasPressedThisFrame)
                mode = mode == PanelMode.GameState ? PanelMode.None : PanelMode.GameState;

            if (kb.f2Key.wasPressedThisFrame)
                mode = mode == PanelMode.Jump ? PanelMode.None : PanelMode.Jump;
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
            GUILayout.Label("<b>GameState (F1 닫기)</b>", header);
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
            GUILayout.Label("<b>Debug Panel</b>  (F2 닫기)", header);
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

            // ── 프롤로그 점프 포인트 ──
            GUILayout.Label("<b>프롤로그 점프</b>", section);
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

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        // ══════════════════════════════════════════════
        //  점프 실행
        // ══════════════════════════════════════════════

        void JumpAsync(string lineId)
        {
            var gm = GameManager.Instance;
            if (gm == null || isJumping) return;

            isJumping = true;
            mode = PanelMode.None;

            try
            {
                // 1. 기존 실행 정리
                ScriptRunner.Instance?.Stop();
                gm.CleanupStage();

                // 2. 플레이어 이름 (미설정 시 기본값)
                if (string.IsNullOrEmpty(gm.PlayerName))
                    gm.SetPlayerName("테스트");
                GameState.Instance?.SetPlayerName(gm.PlayerName);

                // 3. Phase → Prologue, UI 전환
                gm.SetCurrentPhase(GamePhase.Prologue);
                UIManager.Instance?.ShowOnly(MainUIType.Dialogue);

                var dialogueUI = UIManager.Instance?.DialogueUI;
                dialogueUI?.Clear();
                dialogueUI?.ClearLog();
                dialogueUI?.HideImmediate();

                // 4. 화면 효과 초기화
                ScreenFX.Instance?.SetClear();
                ScreenFX.Instance?.EyeOpenImmediate();

                // 5. 프롤로그 종료 이벤트 연결 + 스크립트 실행
                var runner = ScriptRunner.Instance;
                if (runner != null)
                {
                    var flow = gm.Flow;
                    if (flow != null)
                    {
                        runner.OnScriptEnd -= flow.OnPrologueEnd;
                        runner.OnScriptEnd += flow.OnPrologueEnd;
                    }
                    runner.StartScriptFrom(gm.PrologueScript, lineId, 0).Forget();
                }
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
