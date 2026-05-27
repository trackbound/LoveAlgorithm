using UnityEditor;
using LoveAlgo.Contracts;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Story;
using LoveAlgo.Schedule;

namespace LoveAlgo.Editor
{
    /// <summary>
    /// 플레이모드 디버그 리모콘
    /// Window → LoveAlgo → Debug Remote
    /// </summary>
    public class DebugRemoteWindow : EditorWindow
    {
        int setDay = 1;
        int setMoney = 100000;
        int setActions = 2;

        [MenuItem("Tools/LoveAlgo/Debug/Remote")]
        static void Open()
        {
            var w = GetWindow<DebugRemoteWindow>("Debug Remote");
            w.minSize = new Vector2(280, 400);
        }

        void OnGUI()
        {
            if (!Application.isPlaying)
            {
                EditorGUILayout.HelpBox("플레이 모드에서만 사용 가능합니다.", MessageType.Info);
                return;
            }

            var gm = GameManager.Instance;
            if (gm == null)
            {
                EditorGUILayout.HelpBox("GameManager 인스턴스를 찾을 수 없습니다.", MessageType.Warning);
                return;
            }

            // ── 현재 상태 ──
            EditorGUILayout.LabelField("현재 상태", EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Phase", gm.CurrentPhase.ToString());
            EditorGUILayout.LabelField("Day", gm.CurrentDay.ToString());
            EditorGUILayout.LabelField("Actions", gm.RemainingActions.ToString());

            var gs = GameState.Instance;
            if (gs != null)
            {
                EditorGUILayout.LabelField("Money", MoneyFormat.Currency(gs.Money));
                EditorGUILayout.Space(4);
                EditorGUILayout.LabelField("스탯", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("  체력", gs.GetStat("Str").ToString());
                EditorGUILayout.LabelField("  지성", gs.GetStat("Int").ToString());
                EditorGUILayout.LabelField("  사교성", gs.GetStat("Soc").ToString());
                EditorGUILayout.LabelField("  끈기", gs.GetStat("Per").ToString());
                EditorGUILayout.LabelField("  피로", gs.GetStat("Fatigue").ToString());
            }

            EditorGUILayout.Space(8);
            DrawSeparator();

            // ── Phase 전환 ──
            EditorGUILayout.LabelField("Phase 전환", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("→ Title"))
                    gm.Flow.ChangePhase(GamePhase.Title);
                if (GUILayout.Button("→ Schedule"))
                    JumpToSchedule(gm);
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("→ Username"))
                    gm.Flow.ChangePhase(GamePhase.Username);
                if (GUILayout.Button("→ Ending"))
                    gm.Flow.ChangePhase(GamePhase.Ending);
            }

            EditorGUILayout.Space(8);
            DrawSeparator();

            // ── 값 설정 ──
            EditorGUILayout.LabelField("값 설정", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                setDay = EditorGUILayout.IntField("Day", setDay);
                if (GUILayout.Button("적용", GUILayout.Width(50)))
                    gm.CurrentDay = setDay;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                setActions = EditorGUILayout.IntField("Actions", setActions);
                if (GUILayout.Button("적용", GUILayout.Width(50)))
                    gm.RemainingActions = setActions;
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                setMoney = EditorGUILayout.IntField("Money", setMoney);
                if (GUILayout.Button("적용", GUILayout.Width(50)))
                    gs?.SetMoney(setMoney);
            }

            EditorGUILayout.Space(4);

            if (GUILayout.Button("소지금 +100,000"))
                gs?.AddMoney(100000);
            if (GUILayout.Button("피로 초기화"))
                gs?.SetStat("Fatigue", 0);
            if (GUILayout.Button("전 스탯 +10"))
            {
                if (gs != null)
                {
                    gs.AddStat("Str", 10);
                    gs.AddStat("Int", 10);
                    gs.AddStat("Soc", 10);
                    gs.AddStat("Per", 10);
                }
            }

            EditorGUILayout.Space(8);
            DrawSeparator();

            // ── 상점/인벤토리 ──
            EditorGUILayout.LabelField("상점 / 인벤토리", EditorStyles.boldLabel);

            if (GUILayout.Button("인벤토리 전체 클리어"))
                Shop.ShopManager.Reset();

            if (GUILayout.Button("소모품 전부 1개씩 지급"))
            {
                foreach (var item in Shop.ItemDatabase.GetByCategory(Shop.ItemCategory.Consumable))
                    Shop.ShopManager.AddItem(item.Id);
            }

            if (GUILayout.Button("세션버프 전부 1개씩 지급"))
            {
                foreach (var item in Shop.ItemDatabase.GetByCategory(Shop.ItemCategory.SessionBuff))
                    Shop.ShopManager.AddItem(item.Id);
            }

            EditorGUILayout.Space(8);
            DrawSeparator();

            // ── 리셋 ──
            EditorGUILayout.LabelField("리셋", EditorStyles.boldLabel);

            if (GUILayout.Button("GameState 초기화"))
            {
                gs?.ResetAll();
                gm.CurrentDay = 1;
                gm.RemainingActions = GameConstants.ActionsPerDay;
            }

            // 자동 갱신
            Repaint();
        }

        void JumpToSchedule(GameManager gm)
        {
            // 상태 정리
            ScriptRunner.Instance?.Stop();
            gm.CleanupStage();

            // 플레이어 이름 미설정 시 기본값
            if (string.IsNullOrEmpty(gm.PlayerName))
                gm.SetPlayerName("테스트");

            // GameState 초기화 안 됐으면 처리
            if (GameState.Instance != null && string.IsNullOrEmpty(GameState.Instance.PlayerName))
                GameState.Instance.SetPlayerName(gm.PlayerName);

            gm.RemainingActions = Mathf.Max(gm.RemainingActions, 1);

            // DayLoop Phase로 전환 (→ EnterDayLoop → ShowScheduleUI)
            gm.Flow.ChangePhase(GamePhase.DayLoop);
        }

        static void DrawSeparator()
        {
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorGUILayout.Space(4);
        }
    }
}
