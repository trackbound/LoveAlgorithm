using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 대사 named color 치환 (Phase D13).
    /// CSV 대사의 `&lt;color=name&gt;...&lt;/color&gt;` 를 SO 매핑에 따라 `&lt;color=#RRGGBBAA&gt;` 로 변환.
    ///
    /// 호출 흐름 (DialogueUI):
    ///   1) raw 대사 → DialogueColorPalette.ApplyNamedColors(text) [D13]
    ///   2) 결과 → 기존 directive 파서 (wait/sfx/emote/speed)
    ///   3) 결과 → DialogueEffectsParser.Parse (shake/wave/emph) [D9]
    ///   4) CleanText → TMP. 이미 hex로 치환됐으므로 TMP가 native &lt;color&gt; 처리.
    ///
    /// 정책:
    ///   - 값이 '#'으로 시작 → 그대로 통과 (이미 hex, TMP가 처리)
    ///   - 값이 SO 팔레트에 있음 → '#RRGGBBAA' 로 치환
    ///   - 값이 팔레트에 없음 → LogWarning + 원본 그대로 통과 (게임 안 멈춤)
    ///   - 팔레트 없음 → hex 색만 동작 (역호환)
    ///
    /// 중첩(`&lt;color=A&gt;...&lt;color=B&gt;...&lt;/color&gt;...&lt;/color&gt;`)은 TMP가 내부 스택으로
    /// 처리 — 우리는 이름 치환만 책임짐. close 태그 매칭 같은 stack 안 짜도 됨.
    /// </summary>
    public static class DialogueColorPalette
    {
        const string ResourcesPath = "Data/DialogueColorPalette";

        static Dictionary<string, Color> _cachedLookup;
        static bool _resourcesLoaded;
        static bool _loadAttempted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void DomainReload()
        {
            _cachedLookup = null;
            _resourcesLoaded = false;
            _loadAttempted = false;
        }

        /// <summary>Resources에서 SO 1회 로드 후 dictionary 캐시. SO 없으면 null 캐시 (재시도 안 함).</summary>
        static Dictionary<string, Color> GetOrLoadPalette()
        {
            if (_loadAttempted) return _cachedLookup;
            _loadAttempted = true;

            var so = Resources.Load<DialogueColorPaletteSO>(ResourcesPath);
            if (so == null)
            {
                Debug.Log("[DialogueColorPalette] DialogueColorPalette.asset 없음 — hex 색만 동작");
                return null;
            }

            _cachedLookup = so.BuildLookup();
            _resourcesLoaded = true;
            Debug.Log($"[DialogueColorPalette] {_cachedLookup.Count}개 named color 등록");
            return _cachedLookup;
        }

        /// <summary>EditMode 테스트 / 리소스 변경 후 강제 재로드용.</summary>
        public static void ReloadForTests()
        {
            _cachedLookup = null;
            _resourcesLoaded = false;
            _loadAttempted = false;
        }

        /// <summary>프로덕션 호출 — Resources에서 SO 로드해 치환. SO 없으면 원본 그대로.</summary>
        public static string ApplyNamedColors(string text)
        {
            return ApplyNamedColors(text, GetOrLoadPalette());
        }

        /// <summary>
        /// 순수 함수 버전 — 외부 palette dict 주입. EditMode 테스트용.
        /// palette가 null이거나 빈 dict면 hex만 통과.
        /// </summary>
        public static string ApplyNamedColors(string text, IDictionary<string, Color> palette)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";

            // 빠른 패스: '<color=' 자체가 없으면 변환 불필요
            if (text.IndexOf("<color=", StringComparison.OrdinalIgnoreCase) < 0)
                return text;

            var sb = new StringBuilder(text.Length + 32);
            int i = 0;
            while (i < text.Length)
            {
                if (!StartsWithColorOpen(text, i))
                {
                    sb.Append(text[i]);
                    i++;
                    continue;
                }

                // '<color=' 발견. '>' 까지 값 추출.
                int valStart = i + "<color=".Length;
                int valEnd = text.IndexOf('>', valStart);
                if (valEnd < 0)
                {
                    // 닫는 '>' 없음 — 원본 그대로 두고 한 글자 진행
                    sb.Append(text[i]);
                    i++;
                    continue;
                }

                string value = text.Substring(valStart, valEnd - valStart).Trim();
                string replacement = ResolveColorToken(value, palette);
                sb.Append("<color=").Append(replacement).Append('>');
                i = valEnd + 1;
            }

            return sb.ToString();
        }

        static bool StartsWithColorOpen(string text, int idx)
        {
            const string Open = "<color=";
            if (idx + Open.Length > text.Length) return false;
            for (int k = 0; k < Open.Length; k++)
            {
                char a = text[idx + k];
                char b = Open[k];
                // 대소문자 무시 (영문)
                if (a >= 'A' && a <= 'Z') a = (char)(a + 32);
                if (b >= 'A' && b <= 'Z') b = (char)(b + 32);
                if (a != b) return false;
            }
            return true;
        }

        static string ResolveColorToken(string value, IDictionary<string, Color> palette)
        {
            if (string.IsNullOrEmpty(value)) return value;

            // 양 끝 따옴표 허용 (`<color="heroine_roa">` 같은 사용자 입력)
            string stripped = value.Trim('"', '\'');

            // hex (`#...`) → 그대로
            if (stripped.Length > 0 && stripped[0] == '#') return value;

            // 팔레트 lookup
            if (palette != null && palette.TryGetValue(stripped, out var c))
                return ColorToHexLiteral(c);

            // 알 수 없는 이름 — LogWarning + 원본 통과 (TMP가 알 수도 있고 모를 수도 있고)
            Debug.LogWarning($"[DialogueColorPalette] 알 수 없는 색 이름 '{stripped}' — 팔레트 SO에 추가하거나 hex(#RRGGBB)로 적어주세요.");
            return value;
        }

        static string ColorToHexLiteral(Color c)
        {
            // RRGGBBAA 형식 — TMP가 인식
            byte r = (byte)Mathf.Clamp(Mathf.RoundToInt(c.r * 255f), 0, 255);
            byte g = (byte)Mathf.Clamp(Mathf.RoundToInt(c.g * 255f), 0, 255);
            byte b = (byte)Mathf.Clamp(Mathf.RoundToInt(c.b * 255f), 0, 255);
            byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(c.a * 255f), 0, 255);
            return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2") + a.ToString("X2");
        }
    }
}
