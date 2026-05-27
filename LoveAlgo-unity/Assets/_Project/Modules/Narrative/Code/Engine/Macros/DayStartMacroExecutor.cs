using System;
using LoveAlgo.Contracts;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Story.StoryEngine.Macros
{
    /// <summary>
    /// DayStart 매크로 — 하루 시작 + (선택) 페이드 reveal
    /// CSV:
    ///   FX,,DayStart,>                       # 인자 없음 — eye 정리만
    ///   FX,,DayStart:3,>                     # actions만
    ///   FX,,DayStart:BG_Room_Morning,await   # 기본 = Wake (eye-closed 뒤에서 BG 교체, FadeIn 없음)
    ///   FX,,DayStart:BG_Cafe:Cut,await       # Cut — BG Cut → EyeOpen → FadeIn → 짧은 여운
    ///   FX,,DayStart:BG_Cafe:Cut:3,await     # BG + mode + actions
    ///   FX,,DayStart:BG_Cafe:3:Cut,await     # 순서 자유 (숫자=actions, Wake/Cut/Reveal=mode)
    /// </summary>
    public static class DayStartMacroExecutor
    {
        public enum DayStartMode { Wake, Cut }

        public static async UniTask ExecuteAsync(string[] parts, CancellationToken ct)
        {
            ParseArgs(parts, out string bgPath, out int actions, out DayStartMode mode);

            var gm = GameManager.Instance;
            if (gm != null)
            {
                gm.AdvanceDay(actions);
                Debug.Log($"[Macro] DayStart → Day {gm.CurrentDay}, Actions={actions}, mode={mode}, bg={bgPath ?? "(없음)"}");
            }

            var fx = ScreenFX.Instance;
            var cfg = FXDefaultsConfig.Instance;

            // BG 교체 (Cut 전환 — eye/fade 뒤에서 즉시)
            if (bgPath != null)
            {
                var bgLine = new ScriptLine("", LineType.BG, "", $"{bgPath}:Cut", NextType.Immediate);
                var bgExec = new Handlers.BGLineExecutor();
                await bgExec.ExecuteAsync(bgLine, ct);
            }

            if (mode == DayStartMode.Cut)
            {
                // Cut/Reveal: 잔존 eye 정리 → FadeIn으로 직접 reveal → 짧은 여운
                fx?.ResetAll();

                float fadeIn = cfg != null ? cfg.sceneStartFadeEyeOpen : 0.6f;
                if (fx != null)
                    await fx.FadeInAsync(fadeIn, ct);

                float pause = cfg != null ? cfg.sceneStartPauseAfterFadeIn : 0.4f;
                if (pause > 0f)
                    await UniTask.Delay(TimeSpan.FromSeconds(pause), cancellationToken: ct);
            }
            else // Wake
            {
                // Wake (기본): bgPath 있으면 eye-closed 상태 유지, 없으면 eye 정리
                // 작가가 다음 라인에서 명시적으로 EyeOpen 호출.
                if (bgPath == null)
                {
                    fx?.EyeOpenImmediate();
                    Debug.Log("[Macro] DayStart: Eye 해제 (배경 미지정)");
                }
                else
                {
                    Debug.Log("[Macro] DayStart: BG 교체 완료 (EyeClose 유지 — Wake mode)");
                }
            }
        }

        /// <summary>
        /// 인자 파싱 — bgPath/actions/mode 순서 자유.
        /// 숫자 → actions, Wake/Cut/Reveal → mode, 그 외 → bgPath (첫 번째 비매칭 토큰).
        /// </summary>
        static void ParseArgs(string[] parts, out string bgPath, out int actions, out DayStartMode mode)
        {
            bgPath = null;
            actions = GameConstants.ActionsPerDay;
            mode = DayStartMode.Wake;

            for (int i = 1; i < parts.Length; i++)
            {
                string token = parts[i];
                if (string.IsNullOrEmpty(token)) continue;

                if (int.TryParse(token, out int a))
                    actions = a;
                else if (TryParseMode(token, out var m))
                    mode = m;
                else if (bgPath == null)
                    bgPath = token;
            }
        }

        static bool TryParseMode(string token, out DayStartMode mode)
        {
            if (string.Equals(token, "Wake",   StringComparison.OrdinalIgnoreCase)) { mode = DayStartMode.Wake; return true; }
            if (string.Equals(token, "Cut",    StringComparison.OrdinalIgnoreCase)) { mode = DayStartMode.Cut;  return true; }
            if (string.Equals(token, "Reveal", StringComparison.OrdinalIgnoreCase)) { mode = DayStartMode.Cut;  return true; }
            mode = DayStartMode.Wake;
            return false;
        }
    }
}
