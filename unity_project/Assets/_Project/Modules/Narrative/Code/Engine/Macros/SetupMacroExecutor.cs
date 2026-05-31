using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// Setup 매크로 — 장면 환경 즉시 세팅
    /// CSV: FX,,Setup:BG=경로|BGM=이름|Char=이름[:슬롯]|Overlay=이름,>
    /// </summary>
    public static class SetupMacroExecutor
    {
        public static async UniTask ExecuteAsync(string rawValue, CancellationToken ct)
        {
            int colonIdx = rawValue.IndexOf(':');
            if (colonIdx < 0 || colonIdx >= rawValue.Length - 1)
            {
                Debug.LogWarning("[Macro] Setup: 파라미터 없음");
                return;
            }

            string spec = rawValue.Substring(colonIdx + 1);
            var entries = spec.Split('|');

            Debug.Log($"[Macro] Setup ({entries.Length}개 항목)");

            var background = ExecutionDependencies.Stage?.Background;
            var character = ExecutionDependencies.Stage?.Character;
            var overlay = ExecutionDependencies.Stage?.VirtualBG;

            foreach (var entry in entries)
            {
                var kv = entry.Split('=');
                if (kv.Length < 2)
                {
                    Debug.LogWarning($"[Macro] Setup: 잘못된 항목 '{entry}'");
                    continue;
                }

                string key = kv[0].Trim();
                string value = kv[1].Trim();

                switch (key)
                {
                    case "BG":
                        if (background != null && !string.Equals(
                            background.CurrentBackground, value, System.StringComparison.OrdinalIgnoreCase))
                        {
                            await background.ChangeBackgroundAsync(value, BGTransition.Cut, 0f, ct);
                            Debug.Log($"[Macro] Setup: BG → '{value}'");
                        }
                        break;

                    case "BGM":
                        if (ExecutionDependencies.Audio != null)
                        {
                            string currentBGM = ExecutionDependencies.Audio.CurrentBGM;
                            if (!string.Equals(currentBGM, value, System.StringComparison.OrdinalIgnoreCase))
                            {
                                await ExecutionDependencies.Audio.ExecuteAsync($"BGM:{value}", ct);
                                Debug.Log($"[Macro] Setup: BGM → '{value}'");
                            }
                        }
                        break;

                    case "Char":
                        var charParts = value.Split(':');
                        string charName = charParts[0];
                        string slotStr = charParts.Length >= 2 ? charParts[1] : "C";

                        if (character != null && !character.IsCharacterOnStage(charName))
                        {
                            await character.ExecuteAsync($"{slotStr}:Enter:{charName}", ct);
                            Debug.Log($"[Macro] Setup: Char → '{charName}' (슬롯 {slotStr})");
                        }
                        break;

                    case "Overlay":
                        if (overlay != null)
                        {
                            await overlay.ExecuteAsync($"{value}:FadeIn:0", ct);
                            Debug.Log($"[Macro] Setup: Overlay → '{value}'");
                        }
                        break;

                    case "Eye":
                        // 합성기와 동일 의미 — Close/Open 즉시 적용
                        var fx = ScreenFX.Instance;
                        if (fx != null)
                        {
                            if (string.Equals(value, "Close", System.StringComparison.OrdinalIgnoreCase))
                            {
                                fx.EyeCloseImmediate();
                                Debug.Log("[Macro] Setup: Eye → Close");
                            }
                            else if (string.Equals(value, "Open", System.StringComparison.OrdinalIgnoreCase))
                            {
                                fx.EyeOpenImmediate();
                                Debug.Log("[Macro] Setup: Eye → Open");
                            }
                            else
                            {
                                Debug.LogWarning($"[Macro] Setup: Eye 값 '{value}' 알 수 없음 (Close|Open만 허용)");
                            }
                        }
                        break;

                    default:
                        Debug.LogWarning($"[Macro] Setup: 알 수 없는 키 '{key}'");
                        break;
                }
            }
        }
    }
}
