using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.UI;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// DayEnd 매크로 — 하루 마무리 연출.
    /// CSV:
    ///   FX,,DayEnd,await                   # 기본 = Wake (눈 감은 상태로 종료)
    ///   FX,,DayEnd:Wake,await              # 명시
    ///   FX,,DayEnd:Cut,await               # 풀 암전 종료, eye state 없음 (다음 씬 페이드인용)
    ///   FX,,DayEnd:0.8,await               # 페이드 시간만
    ///   FX,,DayEnd:0.8:Cut,await           # 페이드 시간 + 모드
    /// </summary>
    public static class DayEndMacroExecutor
    {
        public enum DayEndMode { Wake, Cut }

        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            var cfg = FXDefaultsConfig.Instance;
            float defFadeOut = cfg != null ? cfg.dayEndFadeOut : 0.8f;
            float defFadeIn  = cfg != null ? cfg.dayEndFadeIn  : 0.3f;

            ParseArgs(parts, defFadeOut, out float fadeDuration, out DayEndMode mode);

            float totalDuration = 5.0f;
            float startTime = Time.time;
            Debug.Log($"[Macro] DayEnd (mode={mode}, fade={fadeDuration}s, total={totalDuration}s)");

            var dialogueUI = ExecutionDependencies.DialogueUI;
            var fx = ScreenFX.Instance;
            var stage = ExecutionDependencies.Stage;

            SaveManager.CapturePendingScreenshot();

            if (fx != null)
                await fx.FadeOutAsync(fadeDuration, ct);

            dialogueUI?.HideImmediate();
            stage?.Character?.ClearAll();
            stage?.VirtualBG?.HideImmediate();
            PopupSystem.Instance?.Get<PlaceNotification>()?.HideImmediate();

            if (ExecutionDependencies.Audio != null)
                await ExecutionDependencies.Audio.ExecuteAsync("BGM:Stop", ct);

            var bgLine = new ScriptLine("", LineType.BG, "", "BG_BlackCut", NextType.Immediate);
            var bgExec = new Handlers.BGLineExecutor();
            await bgExec.ExecuteAsync(bgLine, ct);

            // Wake: 눈 감음 + 살짝 fade-in (BG_BlackCut 위에서 alpha→0이지만 eye가 가림)
            //       → 다음날 눈 감은 상태로 모놀로그 가능
            // Cut:  눈 잔존 정리 + 풀 암전 유지
            //       → 다음 씬 페이드인이 직접 reveal
            if (mode == DayEndMode.Wake)
            {
                fx?.EyeCloseImmediate();
                if (fx != null)
                    await fx.FadeInAsync(defFadeIn, ct);
            }
            else // Cut
            {
                fx?.ResetAll(); // 잔존 eye/tint 정리, fade는 유지(검은 상태)
            }

            if (GameManager.Instance != null)
                await GameManager.Instance.AutoSaveAsync("macro:DayEnd");

            float elapsed = Time.time - startTime;
            float remaining = totalDuration - elapsed;
            if (remaining > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(remaining), cancellationToken: ct);

            Debug.Log($"[Macro] DayEnd 완료 (mode={mode})");
        }

        /// <summary>인자 파싱 — `DayEnd[:duration|mode[:mode]]`</summary>
        static void ParseArgs(string[] parts, float defaultDuration, out float duration, out DayEndMode mode)
        {
            duration = defaultDuration;
            mode = DayEndMode.Wake;

            for (int i = 1; i < parts.Length; i++)
            {
                string token = parts[i];
                if (string.IsNullOrEmpty(token)) continue;

                if (float.TryParse(token, out float d))
                    duration = d;
                else if (TryParseMode(token, out var m))
                    mode = m;
            }
        }

        static bool TryParseMode(string token, out DayEndMode mode)
        {
            if (string.Equals(token, "Wake", StringComparison.OrdinalIgnoreCase)) { mode = DayEndMode.Wake; return true; }
            if (string.Equals(token, "Cut",  StringComparison.OrdinalIgnoreCase)) { mode = DayEndMode.Cut;  return true; }
            mode = DayEndMode.Wake;
            return false;
        }
    }
}
