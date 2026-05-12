using System;
using System.Collections.Generic;
using UnityEngine;
using Cysharp.Threading.Tasks;
using LoveAlgo.Story;
using LoveAlgo.Core;

namespace LoveAlgo.Core
{
    /// <summary>
    /// Quick Play 데이터 로더 - 에디터에서 설정한 값으로 게임 시작
    /// </summary>
    public class QuickPlayLoader : MonoBehaviour
    {
        [Header("Quick Play 설정")]
        [Tooltip("Quick Play 기능 활성화 (에디터 전용)")]
        #pragma warning disable CS0414
        [SerializeField] bool enableQuickPlay = true;
        #pragma warning restore CS0414

        void Awake()
        {
#if UNITY_EDITOR
            if (!enableQuickPlay) return;
            
            // EditorPrefs에서 QuickPlay 데이터 확인
            string json = UnityEditor.EditorPrefs.GetString("LoveAlgo_QuickPlayData", "");
            if (string.IsNullOrEmpty(json)) return;

            try
            {
                var data = JsonUtility.FromJson<QuickPlayData>(json);
                if (data == null || !data.enabled) return;

                // 데이터 적용
                ApplyQuickPlayDataAsync(data).Forget();

                // 사용 후 비활성화 (한 번만 적용)
                data.enabled = false;
                UnityEditor.EditorPrefs.SetString("LoveAlgo_QuickPlayData", JsonUtility.ToJson(data));
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[QuickPlayLoader] Failed to parse data: {e.Message}");
            }
#endif
        }

#if UNITY_EDITOR
        async UniTaskVoid ApplyQuickPlayDataAsync(QuickPlayData data)
        {
            // 프레임 대기 (GameManager, GameState 초기화 대기)
            await UniTask.DelayFrame(3);

            Debug.Log($"[QuickPlay] Applying: Script={data.scriptPath}, LineID={data.startLineId}");

            // GameState 적용
            var gameState = GameState.Instance;
            if (gameState != null)
            {
                // 플레이어 이름
                if (!string.IsNullOrEmpty(data.playerName))
                {
                    gameState.SetPlayerName(data.playerName);
                }

                // 스탯
                foreach (var stat in data.stats)
                {
                    gameState.SetStat(stat.key, stat.value);
                }

                // 호감도
                foreach (var love in data.lovePoints)
                {
                    gameState.SetLove(love.key, love.value);
                }

                // 플래그
                if (data.flags != null)
                {
                    foreach (var flag in data.flags)
                    {
                        gameState.SetFlag(flag, true);
                    }
                }

                // 돈
                gameState.SetMoney(data.money);

                Debug.Log($"[QuickPlay] GameState applied: {data.playerName}, Money={MoneyFormat.Currency(data.money)}");
            }

            // GameManager 적용
            var gameManager = GameManager.Instance;
            if (gameManager != null)
            {
                // Phase를 스토리 모드로 변경
                gameManager.ChangePhase(GamePhase.Prologue);
            }

            // 스크립트 실행
            if (!string.IsNullOrEmpty(data.scriptPath))
            {
                await UniTask.DelayFrame(2); // UI 초기화 대기

                var scriptRunner = ScriptRunner.Instance;
                if (scriptRunner != null)
                {
                    // UI 표시
                    UI.UIManager.Instance?.ShowOnly(UI.MainUIType.Dialogue);

                    // 스크립트 로드
                    var asset = Resources.Load<TextAsset>($"Story/{data.scriptPath}");
                    if (asset != null)
                    {
                        scriptRunner.LoadScript(asset);
                        
                        // 특정 LineID부터 시작하거나 처음부터 시작
                        if (!string.IsNullOrEmpty(data.startLineId))
                        {
                            scriptRunner.RunFrom(data.startLineId);
                        }
                        else
                        {
                            scriptRunner.Run();
                        }
                        
                        Debug.Log($"[QuickPlay] Script started: {data.scriptPath}");
                    }
                    else
                    {
                        Debug.LogError($"[QuickPlay] Script not found: Story/{data.scriptPath}");
                    }
                }
            }
        }
#endif

        // QuickPlayData, StatEntry는 LoveAlgo.Editor.UIEngine.QuickPlayWindow에서 정의됨
        // 에디터 코드에서만 사용하므로 여기서는 중복 정의하지 않음
        // JsonUtility로 역직렬화 시 내부적으로 처리됨
        
        [Serializable]
        public class QuickPlayData
        {
            public bool enabled;
            public string scriptPath;
            public string startLineId;
            public string playerName;
            public int currentDay;
            public int money;
            public List<StatEntry> stats = new();
            public List<StatEntry> lovePoints = new();
            public List<string> flags = new();
        }

        [Serializable]
        public class StatEntry
        {
            public string key;
            public int value;
            
            public StatEntry() { }
            public StatEntry(string key, int value)
            {
                this.key = key;
                this.value = value;
            }
        }
    }
}
