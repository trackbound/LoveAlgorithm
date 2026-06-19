using System.Collections.Generic;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 명령별 인자 갯수 시그니처 — Preflight validator용.
    /// MinArgs: 명령 토큰 제외 최소 인자 (0 = 인자 없음 허용).
    /// MaxArgs: 최대 인자 (int.MaxValue = 무제한).
    /// </summary>
    public static class FXCommandSignatures
    {
        public struct Sig
        {
            public int MinArgs;
            public int MaxArgs;
            public string Hint; // 사람-친화 힌트 (작가용)

            public Sig(int min, int max, string hint) { MinArgs = min; MaxArgs = max; Hint = hint; }
        }

        // PascalCase canonical 키 (CommandAliases.NormalizeFX 후 토큰)
        public static readonly Dictionary<string, Sig> Map = new()
        {
            // Screen Fade / Flash
            { "FadeOut",      new Sig(0, 1, "[duration]") },
            { "FadeIn",       new Sig(0, 1, "[duration]") },
            { "Flash",        new Sig(0, 1, "[duration]") },

            // Shake — 0~2 인자, 인자 자리에 숫자/프리셋(Weak/Medium/Strong) 가능
            { "CamShake",      new Sig(0, 2, "[duration[:strength]]") },
            { "StageShake",    new Sig(0, 2, "[duration[:strength]]") },
            { "DialogueShake", new Sig(0, 2, "[duration[:strength]]") },

            // Camera
            { "CamZoom",  new Sig(0, 2, "[zoomLevel[:duration]]") },
            { "CamPan",   new Sig(2, 3, "x:y[:duration]") },
            { "CamReset", new Sig(0, 1, "[duration]") },

            // Tint
            { "ColorTint", new Sig(1, 3, "preset[:alpha[:duration]]") },

            // Eye
            { "EyeOpen",           new Sig(0, 1, "[duration]") },
            { "EyeClose",          new Sig(0, 1, "[duration]") },
            { "EyeCloseImmediate", new Sig(0, 0, "(인자 없음)") },
            { "EyeBlink",          new Sig(0, 3, "[close[:open[:hold]]]") },

            // Character FX (FX 라인 — Char는 별도)
            { "CharShake",  new Sig(0, 3, "[slot[:strength[:duration]]]") },
            { "CharJump",   new Sig(0, 3, "[slot[:height[:duration]]]") },
            { "CharDim",    new Sig(0, 3, "[slot[:alpha[:duration]]]") },
            { "CharGlitch", new Sig(0, 3, "[slot[:strength[:duration]]]") },

            // 매크로
            { "DayStart",     new Sig(0, 3, "[bgPath][:Wake|Cut|Reveal][:actionCount]") },
            { "DayEnd",       new Sig(0, 2, "[fadeDuration][:Wake|Cut]") },
            { "NextDay",      new Sig(0, 3, "[Wake|Cut][:bgPath][:actionCount]") },
            { "SceneStart",   new Sig(0, 2, "[bgPath[:EyeClose]]") },
            { "SceneEnd",     new Sig(0, 1, "[fadeDuration]") },
            { "Setup",        new Sig(1, int.MaxValue, "BG=...|BGM=...|Char=...[:슬롯]|Overlay=...|Eye=Close|Open") },
            { "Wait",         new Sig(0, 1, "[seconds]") },
            { "DialogueHide", new Sig(0, 0, "(인자 없음)") },
            { "DialogueShow", new Sig(0, 0, "(인자 없음)") },
            { "Video",        new Sig(1, 3, "파일명[:Loop|:NoSkip]") },
            { "LoadingScene", new Sig(0, 2, "[displayTime][:characterKey]") },
        };

        /// <summary>주어진 canonical 명령과 인자 개수가 시그니처에 부합하는지.</summary>
        public static bool TryValidate(string canonicalCommand, int argCount, out Sig sig, out string error)
        {
            sig = default;
            error = null;
            if (!Map.TryGetValue(canonicalCommand, out sig))
            {
                error = $"알 수 없는 FX 명령: '{canonicalCommand}'";
                return false;
            }
            if (argCount < sig.MinArgs)
            {
                error = $"{canonicalCommand}: 인자 부족 (필요 ≥{sig.MinArgs}, 입력 {argCount}). 형식: {sig.Hint}";
                return false;
            }
            if (argCount > sig.MaxArgs)
            {
                error = $"{canonicalCommand}: 인자 과다 (허용 ≤{sig.MaxArgs}, 입력 {argCount}). 형식: {sig.Hint}";
                return false;
            }
            return true;
        }
    }
}
