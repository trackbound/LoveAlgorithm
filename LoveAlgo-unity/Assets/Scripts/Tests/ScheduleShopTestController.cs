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
    /// 스케줄/상점 시스템 테스트용 런타임 컨트롤러
    /// IMGUI로 디버그 패널 표시, 게임 상태 조작 및 각 시스템 직접 실행
    /// </summary>
    public class ScheduleShopTestController : MonoBehaviour
    {
        [Header("시작 설정")]
        [SerializeField] int startMoney = 100000;
#pragma warning disable CS0414 // 인스펙터 설정용 필드 (향후 사용 예정)
        [SerializeField] int startDay = 1;
#pragma warning restore CS0414
        [SerializeField] bool autoStartDayLoop = true;

        bool panelOpen = true;
        Vector2 scrollPos;

        // 스탯 조작용
        int setStatValue = 30;
        int setMoneyValue = 100000;

        void Start()
        {
            // 초기 상태 설정
            var gs = GameState.Instance;
            if (gs != null)
            {
                gs.ResetAll();
                gs.SetPlayerName("테스트");
                gs.AddMoney(startMoney);
            }

            HeroinePointTracker.Reset();

            var gm = GameManager.Instance;
            if (gm != null && autoStartDayLoop)
            {
                // 약간의 딜레이 후 DayLoop 진입 (모든 싱글톤 초기화 대기)
                Invoke(nameof(StartDayLoop), 0.5f);
            }
        }

        void StartDayLoop()
        {
            var gm = GameManager.Instance;
            if (gm == null) return;

            gm.ChangePhase(GamePhase.DayLoop);
        }

        void OnGUI()
        {
            // F1 토글
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.F1)
            {
                panelOpen = !panelOpen;
                Event.current.Use();
            }

            if (!panelOpen) 
            {
                GUI.Label(new Rect(10, 10, 250, 20), "<color=yellow>[F1] 테스트 패널 열기</color>", RichStyle());
                return;
            }

            var area = new Rect(10, 10, 320, 700);
            GUI.Box(area, "");
            GUILayout.BeginArea(area);
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            GUILayout.Label("<b><size=16>스케줄/상점 테스트</size></b>  <size=11>[F1] 토글</size>", RichStyle());
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
            GUILayout.Label($"Money: {money:N0}원");

            if (gs != null)
            {
                GUILayout.Label($"체력:{gs.GetStat("Str")}  지성:{gs.GetStat("Int")}  사교:{gs.GetStat("Soc")}  끈기:{gs.GetStat("Per")}  피로:{gs.GetStat("Fatigue")}");
            }

            // 호감도 표시
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
            if (GUILayout.Button("DayLoop 시작"))
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
            if (GUILayout.Button("행동 리셋 (2)"))
            {
                // RemainingActions를 직접 세팅할 수 없으므로 AdvanceDay 우회
                GameManager.Instance?.AdvanceDay();
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

            if (GUILayout.Button("전체 상태 초기화 (ResetAll)"))
            {
                gs.ResetAll();
                gs.SetPlayerName("테스트");
                gs.AddMoney(startMoney);
                HeroinePointTracker.Reset();
            }
        }

        void DrawShopControls()
        {
            GUILayout.Label("<b>── 상점/선물 ──</b>", RichStyle());

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("상점 열기"))
            {
                // ShopPopup은 ScheduleUI 내 크로스페이드 패널
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

            // 히로인 선물 잔여 포인트
            GUILayout.Space(3);
            GUILayout.Label("<size=11>히로인 선물 잔여:</size>", RichStyle());
            foreach (var id in GameConstants.HeroineIds)
            {
                int remaining = ShopManager.GetRemainingGiftPoints(id);
                GUILayout.Label($"  {id}: {remaining}/8");
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
