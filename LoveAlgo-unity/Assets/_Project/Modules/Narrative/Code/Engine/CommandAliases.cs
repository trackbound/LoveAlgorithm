using System;
using System.Collections.Generic;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// CSV 작성 편의용 명령어 별칭(alias) + 케이스 정규화.
    /// 모든 핸들러가 진입 시 호출 → 정규화된 PascalCase canonical 토큰으로 변환.
    /// 작가는 `Shake`, `shake`, `SHAKE`, `CamShake` 모두 동일하게 작성 가능.
    /// </summary>
    public static class CommandAliases
    {
        // ── FX 명령 alias ──────────────────────────────────────
        static readonly Dictionary<string, string> fxAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            // 짧은 별칭 → canonical
            { "Shake",       "CamShake" },
            { "Zoom",        "CamZoom" },
            { "Pan",         "CamPan" },
            { "Reset",       "CamReset" },
            { "Tint",        "ColorTint" },
            { "Blink",       "EyeBlink" },
            { "Open",        "EyeOpen" },
            { "Close",       "EyeClose" },

            // canonical → self (대소문자 정규화)
            { "FadeOut",     "FadeOut" },
            { "FadeIn",      "FadeIn" },
            { "Flash",       "Flash" },
            { "CamShake",    "CamShake" },
            { "StageShake",  "StageShake" },
            { "DialogueShake","DialogueShake" },
            { "CamZoom",     "CamZoom" },
            { "CamPan",      "CamPan" },
            { "CamReset",    "CamReset" },
            { "ColorTint",   "ColorTint" },
            { "EyeOpen",     "EyeOpen" },
            { "EyeClose",    "EyeClose" },
            { "EyeCloseImmediate", "EyeCloseImmediate" },
            { "EyeBlink",    "EyeBlink" },
            { "CharShake",   "CharShake" },
            { "CharJump",    "CharJump" },
            { "CharDim",     "CharDim" },
            { "CharGlitch",  "CharGlitch" },

            // Camera Preset (D5) — Preset: 짧은 alias도 허용
            { "CamPreset",   "CamPreset" },
            { "Preset",      "CamPreset" },

            // 매크로
            { "DayStart",    "DayStart" },
            { "DayEnd",      "DayEnd" },
            { "NextDay",     "NextDay" },
            { "SceneStart",  "SceneStart" },
            { "SceneEnd",    "SceneEnd" },
            { "Setup",       "Setup" },
            { "Wait",        "Wait" },
            { "DialogueHide","DialogueHide" },
            { "DialogueShow","DialogueShow" },
            { "Video",       "Video" },

            // Flow → FX 별칭
            { "Loading",      "LoadingScene" },
            { "LoadingScene", "LoadingScene" },
        };

        /// <summary>FX/매크로 명령 토큰 정규화. 알 수 없으면 입력 그대로 반환(런타임에서 워닝).</summary>
        public static string NormalizeFX(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            return fxAliases.TryGetValue(token.Trim(), out var canonical) ? canonical : token;
        }

        /// <summary>FX/매크로 화이트리스트 — Preflight validator용.</summary>
        public static bool IsKnownFX(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            return fxAliases.ContainsKey(token.Trim());
        }

        // ── BG transition alias ─────────────────────────────────
        static readonly Dictionary<string, string> bgTransitionAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Cut",       "Cut" },
            { "Fade",      "Fade" },
            { "Cross",     "CrossFade" },
            { "CrossFade", "CrossFade" },
        };

        public static string NormalizeBGTransition(string token)
        {
            if (string.IsNullOrEmpty(token)) return "CrossFade"; // 기본
            return bgTransitionAliases.TryGetValue(token.Trim(), out var canonical) ? canonical : token;
        }

        // ── CG action alias ─────────────────────────────────────
        static readonly Dictionary<string, string> cgActionAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "Exit",  "Exit" },
            { "Close", "Exit" }, // Close → Exit canonical
            { "Hide",  "Exit" },
        };

        public static string NormalizeCGAction(string token)
        {
            if (string.IsNullOrEmpty(token)) return token;
            return cgActionAliases.TryGetValue(token.Trim(), out var canonical) ? canonical : token;
        }

        // ── Char action alias ───────────────────────────────────
        static readonly HashSet<string> charActions = new(StringComparer.OrdinalIgnoreCase)
        {
            "Enter", "Exit", "Emote", "EnterUp", "ExitDown", "Clear", "Move",
        };

        /// <summary>주어진 토큰이 Char 액션 키워드인지 (Char 단축 문법용).</summary>
        public static bool IsCharAction(string token)
        {
            if (string.IsNullOrEmpty(token)) return false;
            return charActions.Contains(token.Trim());
        }

        // ── Char slot alias ─────────────────────────────────────
        static readonly Dictionary<string, string> slotAliases = new(StringComparer.OrdinalIgnoreCase)
        {
            { "L",      "L" }, { "Left",   "L" },
            { "C",      "C" }, { "Center", "C" }, { "Centre", "C" },
            { "R",      "R" }, { "Right",  "R" },
        };

        /// <summary>슬롯 토큰을 L/C/R로 정규화. 슬롯이 아니면 null 반환.</summary>
        public static string NormalizeSlot(string token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            return slotAliases.TryGetValue(token.Trim(), out var canonical) ? canonical : null;
        }
    }
}
