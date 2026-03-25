using UnityEngine;
using Cysharp.Threading.Tasks;
using LoveAlgo.Core;
using LoveAlgo.Story;
using LoveAlgo.UI;
using LoveAlgo.Schedule;
using LoveAlgo.Shop;

namespace LoveAlgo
{
    /// <summary>
    /// 테스트 빌드 편의 기능 (자동 부트스트랩)
    /// - 타이틀: 스케줄/상점 바로가기 버튼
    /// - 인게임: F1 디버그 패널 (스탯/돈/상점/인벤토리 조작)
    /// 릴리즈 빌드 시 이 파일 제거
    /// </summary>
    public class TestBuildHelper : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            if (FindFirstObjectByType<TestBuildHelper>() != null) return;
            var go = new GameObject("[TestBuildHelper]");
            go.AddComponent<TestBuildHelper>();
            DontDestroyOnLoad(go);
        }

        bool debugPanelOpen;
        Vector2 scrollPos;
        int setStatValue = 30;
        int setMoneyValue = 100000;

        void OnGUI()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            // F1 토글
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1)
            {
                debugPanelOpen = !debugPanelOpen;
                Event.current.Use();
            }

            if (gm.CurrentPhase == GamePhase.Title)
            {
                DrawTitleShortcuts();
            }
            else
            {
                if (debugPanelOpen)
                    DrawDebugPanel();
                else
                    DrawDebugHint();
            }
        }

        // ─────────────────────────────────
        // 타이틀 바로가기
        // ─────────────────────────────────

        void DrawTitleShortcuts()
        {
            float bw = 260f;
            float bh = 40f;
            float x = Screen.width - bw - 20f;
            float y = 20f;

            GUI.Box(new Rect(x - 10, y - 5, bw + 20, 110), "");

            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.yellow }
            };
            GUI.Label(new Rect(x, y, bw, 22), "── 테스트 바로가기 ──", style);
            y += 26f;

            if (GUI.Button(new Rect(x, y, bw, bh), "▶ 스케줄/상점 바로가기 (프롤로그 스킵)"))
            {
                GameManager.Instance?.SkipToDayLoop();
            }
            y += bh + 6f;

            var hintStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 11,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = new Color(0.8f, 0.8f, 0.8f) }
            };
            GUI.Label(new Rect(x, y, bw, 18), "인게임에서 F1 = 디버그 패널", hintStyle);
        }

        // ─────────────────────────────────
        // 디버그 힌트
        // ─────────────────────────────────

        void DrawDebugHint()
        {
            var style = new GUIStyle(GUI.skin.label)
            {
                richText = true,
                fontSize = 12
            };
            GUI.Label(new Rect(10, 10, 280, 20),
                "<color=yellow>[F1] 디버그 패널</color>", style);
        }

        // ─────────────────────────────────
        // 디버그 패널
        // ─────────────────────────────────

        void DrawDebugPanel()
        {
            var area = new Rect(10, 10, 330, 620);
            GUI.Box(area, "");
            GUILayout.BeginArea(area);
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            GUILayout.Label("<b><size=16>디버그 패널</size></b>  <size=11>[F1] 토글</size>", RichStyle());
            GUILayout.Space(5);

            DrawStateInfo();
            GUILayout.Space(5);
            DrawPhaseControls();
            GUILayout.Space(5);
            DrawStatControls();
            GUILayout.Space(5);
            DrawShopControls();
            GUILayout.Space(5);
            DrawInventoryInfo();

            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void DrawStateInfo()
        {
            GUILayout.Label("<b>── 현재 상태 ──</b>", RichStyle());

            var gm = GameManager.Instance;
            var gs = GameState.Instance;

            string phase = gm != null ? gm.CurrentPhase.ToString() : "N/A";
            int day = gm != null ? gm.CurrentDay : 0;
            int actions = gm != null ? gm.RemainingActions : 0;
            int money = gs != null ? gs.Money : 0;

            GUILayout.Label($"Phase: {phase}  |  Day: {day}  |  Actions: {actions}");
            GUILayout.Label($"Money: {MoneyFormat.Currency(money)}");

            if (gs != null)
            {
                GUILayout.Label($"체력:{gs.GetStat("Str")}  지성:{gs.GetStat("Int")}  사교:{gs.GetStat("Soc")}  끈기:{gs.GetStat("Per")}  피로:{gs.GetStat("Fatigue")}");
            }

            if (gs != null)
            {
                string loves = "";
                foreach (var id in GameConstants.HeroineIds)
                {
                    int lv = gs.GetLove(id);
                    if (lv > 0) loves += $"{id}:{lv} ";
                }
                if (!string.IsNullOrEmpty(loves))
                    GUILayout.Label($"호감도: {loves}");
            }
        }

        void DrawPhaseControls()
        {
            GUILayout.Label("<b>── Phase 컨트롤 ──</b>", RichStyle());

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("DayLoop 재시작"))
            {
                GameManager.Instance?.ChangePhase(GamePhase.DayLoop);
            }
            if (GUILayout.Button("Schedule UI만"))
            {
                UIManager.Instance?.ShowOnly(MainUIType.Schedule);
                UIManager.Instance?.ScheduleUI?.ShowAsync(_ => { }).Forget();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("다음 날"))
            {
                GameManager.Instance?.AdvanceDay();
                GameManager.Instance?.ChangePhase(GamePhase.DayLoop);
            }
            if (GUILayout.Button("행동 리셋"))
            {
                GameManager.Instance?.AdvanceDay();
            }
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("타이틀로"))
            {
                GameManager.Instance?.GoToTitle();
            }
            GUILayout.EndHorizontal();
        }

        void DrawStatControls()
        {
            GUILayout.Label("<b>── 스탯/돈 조작 ──</b>", RichStyle());

            var gs = GameState.Instance;
            if (gs == null)
            {
                GUILayout.Label("GameState 없음");
                return;
            }

            GUILayout.BeginHorizontal();
            GUILayout.Label("스탯 값:", GUILayout.Width(55));
            string statInput = GUILayout.TextField(setStatValue.ToString(), GUILayout.Width(50));
            int.TryParse(statInput, out setStatValue);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("체력")) gs.SetStat("Str", setStatValue);
            if (GUILayout.Button("지성")) gs.SetStat("Int", setStatValue);
            if (GUILayout.Button("사교")) gs.SetStat("Soc", setStatValue);
            if (GUILayout.Button("끈기")) gs.SetStat("Per", setStatValue);
            if (GUILayout.Button("피로")) gs.SetStat("Fatigue", setStatValue);
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            GUILayout.BeginHorizontal();
            GUILayout.Label("돈:", GUILayout.Width(30));
            string moneyInput = GUILayout.TextField(setMoneyValue.ToString(), GUILayout.Width(80));
            int.TryParse(moneyInput, out setMoneyValue);
            if (GUILayout.Button("설정")) gs.SetMoney(setMoneyValue);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+10만")) gs.AddMoney(100000);
            if (GUILayout.Button("+50만")) gs.AddMoney(500000);
            if (GUILayout.Button("0원")) gs.SetMoney(0);
            GUILayout.EndHorizontal();

            GUILayout.Space(3);

            if (GUILayout.Button("전체 상태 초기화"))
            {
                gs.ResetAll();
                gs.SetPlayerName("테스터");
                gs.AddMoney(100000);
                HeroinePointTracker.Reset();
            }
        }

        void DrawShopControls()
        {
            GUILayout.Label("<b>── 상점/선물 ──</b>", RichStyle());

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("상점 열기"))
            {
                var scheduleUI = Object.FindFirstObjectByType<ScheduleUI>();
                scheduleUI?.OpenShop();
            }
            if (GUILayout.Button("선물 열기"))
            {
                PopupManager.Instance?.ShowModal<GiftPopup>();
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(3);
            GUILayout.Label("<size=11>테스트 아이템 추가:</size>", RichStyle());

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("음료×3"))   ShopManager.AddItem("consume_energy_drink", 3);
            if (GUILayout.Button("비타민×2")) ShopManager.AddItem("consume_vitamin", 2);
            if (GUILayout.Button("무드등×1")) ShopManager.AddItem("consume_mood_lamp", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("프로틴초코")) ShopManager.AddItem("buff_protein_choco", 1);
            if (GUILayout.Button("노트"))      ShopManager.AddItem("buff_note", 1);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("예은선물")) ShopManager.AddItem("gift_yeun_towel", 1);
            if (GUILayout.Button("다은선물")) ShopManager.AddItem("gift_daeun_pen", 1);
            if (GUILayout.Button("봄선물"))   ShopManager.AddItem("gift_bom_sticker", 1);
            if (GUILayout.Button("희원선물")) ShopManager.AddItem("gift_heewon_novel", 1);
            if (GUILayout.Button("로아선물")) ShopManager.AddItem("gift_roa_light", 1);
            GUILayout.EndHorizontal();
        }

        void DrawInventoryInfo()
        {
            GUILayout.Label("<b>── 인벤토리 ──</b>", RichStyle());

            var inventory = ShopManager.GetInventory();
            if (inventory == null || inventory.Count == 0)
            {
                GUILayout.Label("(비어있음)");
            }
            else
            {
                foreach (var kv in inventory)
                {
                    var item = ItemDatabase.Get(kv.Key);
                    string name = item != null ? item.Name : kv.Key;
                    GUILayout.Label($"  {name} × {kv.Value}");
                }
            }
        }

        static GUIStyle _richStyle;
        static GUIStyle RichStyle()
        {
            if (_richStyle == null)
            {
                _richStyle = new GUIStyle(GUI.skin.label) { richText = true };
            }
            return _richStyle;
        }
    }
}
