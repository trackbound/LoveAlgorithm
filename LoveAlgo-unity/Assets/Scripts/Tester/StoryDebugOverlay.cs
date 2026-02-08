using UnityEngine;
using LoveAlgo;
using LoveAlgo.Story;
using TMPro;
using System.Text;

namespace LoveAlgo.Tester
{
    /// <summary>
    /// 플레이 중 F2를 눌러 현재 게임 상태를 실시간으로 확인하는 디버그 오버레이.
    /// 스크립트 위치, 게임 상태(스탯/호감도/플래그) 표시.
    /// 
    /// 단축키:
    /// - F2: 오버레이 표시/숨김
    /// - Shift+1~4: 스탯 +10 (Str/Int/Soc/Per)
    /// - Ctrl+1~5: 호감도 +10 (Roa/Yeun/Daeun/Bom/Heewon)
    /// 
    /// </summary>
    public class StoryDebugOverlay : MonoBehaviour
    {
        [Header("Toggle")]
        [SerializeField] KeyCode toggleKey = KeyCode.F2;
        
        [Header("UI References")]
        [SerializeField] GameObject overlayPanel;
        [SerializeField] TMP_Text scriptInfoText;
        [SerializeField] TMP_Text statsText;
        [SerializeField] TMP_Text lovePointsText;
        [SerializeField] TMP_Text flagsText;
        
        [Header("Settings")]
        [SerializeField] float updateInterval = 0.5f;
        [SerializeField] int maxFlagsToShow = 10;
        
        bool isVisible;
        float lastUpdateTime;
        StringBuilder sb = new StringBuilder();
        
        // 히로인 목록 (GameConstants 참조)
        static string[] Heroines => GameConstants.HeroineIds;
        static string[] HeroineNames => GameConstants.HeroineNames;
        
        void Start()
        {
            if (overlayPanel != null)
                overlayPanel.SetActive(false);
        }
        
        void Update()
        {
            // F1 토글
            if (Input.GetKeyDown(toggleKey))
            {
                isVisible = !isVisible;
                if (overlayPanel != null)
                    overlayPanel.SetActive(isVisible);
                
                if (isVisible)
                    UpdateOverlay();
            }
            
            // 주기적 업데이트
            if (isVisible && Time.time - lastUpdateTime > updateInterval)
            {
                UpdateOverlay();
                lastUpdateTime = Time.time;
            }
        }
        
        void UpdateOverlay()
        {
            UpdateScriptInfo();
            UpdateStats();
            UpdateLovePoints();
            UpdateFlags();
        }
        
        void UpdateScriptInfo()
        {
            if (scriptInfoText == null) return;
            
            sb.Clear();
            sb.AppendLine("<b>[ 스크립트 정보 ]</b>");
            
            var runner = ScriptRunner.Instance;
            if (runner != null)
            {
                // 현재 스크립트명 (리플렉션 또는 public 프로퍼티 필요)
                sb.AppendLine($"상태: {(runner.IsRunning ? "<color=green>실행 중</color>" : "<color=yellow>대기</color>")}");
                
                // 현재 라인 정보 (ScriptRunner에 접근 가능하면)
                var currentLine = runner.CurrentLine;
                if (currentLine != null)
                {
                    sb.AppendLine($"LineID: <color=cyan>{currentLine.LineID ?? "(없음)"}</color>");
                    sb.AppendLine($"Type: <color=orange>{currentLine.Type}</color>");
                    sb.AppendLine($"Index: {runner.CurrentIndex}");
                    
                    if (!string.IsNullOrEmpty(currentLine.Speaker))
                        sb.AppendLine($"Speaker: {currentLine.Speaker}");
                }
            }
            else
            {
                sb.AppendLine("<color=gray>ScriptRunner 없음</color>");
            }
            
            scriptInfoText.text = sb.ToString();
        }
        
        void UpdateStats()
        {
            if (statsText == null) return;
            
            sb.Clear();
            sb.AppendLine("<b>[ 플레이어 스탯 ]</b>");
            
            var state = GameState.Instance;
            if (state != null)
            {
                sb.AppendLine($"이름: <color=white>{state.PlayerName}</color>");
                sb.AppendLine($"체력(Str): <color=green>{state.GetStat("Str")}</color>");
                sb.AppendLine($"지성(Int): <color=blue>{state.GetStat("Int")}</color>");
                sb.AppendLine($"사교성(Soc): <color=yellow>{state.GetStat("Soc")}</color>");
                sb.AppendLine($"끈기(Per): <color=orange>{state.GetStat("Per")}</color>");
                sb.AppendLine($"피로(Fatigue): <color=red>{state.GetStat("Fatigue")}</color>");
                sb.AppendLine($"소지금: <color=yellow>₩{state.Money:N0}</color>");
            }
            else
            {
                sb.AppendLine("<color=gray>GameState 없음</color>");
            }
            
            statsText.text = sb.ToString();
        }
        
        void UpdateLovePoints()
        {
            if (lovePointsText == null) return;
            
            sb.Clear();
            sb.AppendLine("<b>[ 호감도 ]</b>");
            
            var state = GameState.Instance;
            if (state != null)
            {
                for (int i = 0; i < Heroines.Length; i++)
                {
                    int love = state.GetLove(Heroines[i]);
                    string color = GetLoveColor(love);
                    string bar = GetProgressBar(love, 100);
                    sb.AppendLine($"{HeroineNames[i]}: <color={color}>{love}</color> {bar}");
                }
            }
            else
            {
                sb.AppendLine("<color=gray>GameState 없음</color>");
            }
            
            lovePointsText.text = sb.ToString();
        }
        
        void UpdateFlags()
        {
            if (flagsText == null) return;
            
            sb.Clear();
            sb.AppendLine("<b>[ 활성 플래그 ]</b>");
            
            var state = GameState.Instance;
            if (state != null)
            {
                var flags = state.GetAllFlags();
                int count = 0;
                
                foreach (var kvp in flags)
                {
                    if (kvp.Value) // true인 플래그만
                    {
                        if (count >= maxFlagsToShow)
                        {
                            sb.AppendLine($"<color=gray>... 외 {flags.Count - count}개</color>");
                            break;
                        }
                        sb.AppendLine($"• <color=green>{kvp.Key}</color>");
                        count++;
                    }
                }
                
                if (count == 0)
                    sb.AppendLine("<color=gray>(없음)</color>");
            }
            else
            {
                sb.AppendLine("<color=gray>GameState 없음</color>");
            }
            
            flagsText.text = sb.ToString();
        }
        
        string GetLoveColor(int love)
        {
            if (love >= 80) return "#FF69B4"; // Hot Pink
            if (love >= 50) return "#FF6B6B"; // Coral
            if (love >= 30) return "#FFB347"; // Orange
            if (love >= 10) return "#87CEEB"; // Sky Blue
            return "#808080"; // Gray
        }
        
        string GetProgressBar(int current, int max)
        {
            const int barLength = 10;
            int filled = Mathf.RoundToInt((float)current / max * barLength);
            filled = Mathf.Clamp(filled, 0, barLength);
            
            string bar = new string('█', filled) + new string('░', barLength - filled);
            return $"<color=#555555>[</color>{bar}<color=#555555>]</color>";
        }
        
        // 디버그용 단축키 추가
        void LateUpdate()
        {
            if (!isVisible) return;
            
            // Shift+F1: 스탯 +10
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha1))
            {
                GameState.Instance?.AddStat("Str", 10);
                Debug.Log("[Debug] Str +10");
            }
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha2))
            {
                GameState.Instance?.AddStat("Int", 10);
                Debug.Log("[Debug] Int +10");
            }
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha3))
            {
                GameState.Instance?.AddStat("Soc", 10);
                Debug.Log("[Debug] Soc +10");
            }
            if (Input.GetKey(KeyCode.LeftShift) && Input.GetKeyDown(KeyCode.Alpha4))
            {
                GameState.Instance?.AddStat("Per", 10);
                Debug.Log("[Debug] Per +10");
            }
            
            // Ctrl+1~5: 호감도 +10
            if (Input.GetKey(KeyCode.LeftControl))
            {
                for (int i = 0; i < Heroines.Length && i < 5; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        GameState.Instance?.AddLove(Heroines[i], 10);
                        Debug.Log($"[Debug] {Heroines[i]} Love +10");
                    }
                }
            }
        }
    }
}
