using System.Collections.Generic;
using System.Text;

namespace LoveAlgo.Common
{
    /// <summary>
    /// 한글 → 두벌식 영문 키 매핑 변환.
    ///
    /// 용도: 비밀번호 입력칸 등에서 OS 한/영 전환 상태와 무관하게 일관된 키 위치로 비밀번호 처리.
    /// 예) "한" (U+D55C) → "gks" (ㅎ+ㅏ+ㄴ → g+k+s)
    ///
    /// 변환 규칙:
    ///   - 완성형 한글 (U+AC00~U+D7A3): 초성+중성+(종성) → 두벌식 영문
    ///   - 호환 자모 (U+3131~U+3163, ㄱㄴㄷ... ㅏㅑㅓ...): 단독 자모 → 두벌식 영문
    ///   - 그 외 문자(ASCII 등): 변환 없이 그대로 반환
    ///
    /// 복합 자모 (ㄳ, ㄺ, ㅘ 등)도 매핑 — 두벌식 표준 (Windows IME 기본).
    /// </summary>
    public static class HangulToDubeolsik
    {
        const int HangulBase    = 0xAC00;   // '가'
        const int HangulLast    = 0xD7A3;   // '힣'
        const int JungsungCount = 21;
        const int JongsungCount = 28;

        // 초성 19개 — 두벌식 단일 키
        // ㄱ  ㄲ  ㄴ  ㄷ  ㄸ  ㄹ  ㅁ  ㅂ  ㅃ  ㅅ  ㅆ  ㅇ  ㅈ  ㅉ  ㅊ  ㅋ  ㅌ  ㅍ  ㅎ
        static readonly string[] ChoEng = {
            "r","R","s","e","E","f","a","q","Q","t","T","d","w","W","c","z","x","v","g"
        };

        // 중성 21개 — 일부는 복합 (ㅘ=hk, ㅙ=ho, ㅚ=hl, ㅝ=nj, ㅞ=np, ㅟ=nl, ㅢ=ml)
        // ㅏ  ㅐ  ㅑ  ㅒ  ㅓ  ㅔ  ㅕ  ㅖ  ㅗ   ㅘ   ㅙ   ㅚ   ㅛ  ㅜ  ㅝ   ㅞ   ㅟ   ㅠ  ㅡ  ㅢ   ㅣ
        static readonly string[] JungEng = {
            "k","o","i","O","j","p","u","P","h","hk","ho","hl","y","n","nj","np","nl","b","m","ml","l"
        };

        // 종성 28개 (0=없음). 일부 복합 (ㄳ=rt, ㄵ=sw, ㄶ=sg, ㄺ=fr, ㄻ=fa, ㄼ=fq, ㄽ=ft, ㄾ=fx, ㄿ=fv, ㅀ=fg, ㅄ=qt)
        //  ()  ㄱ  ㄲ  ㄳ   ㄴ  ㄵ   ㄶ   ㄷ  ㄹ  ㄺ   ㄻ   ㄼ   ㄽ   ㄾ   ㄿ   ㅀ   ㅁ  ㅂ  ㅄ   ㅅ  ㅆ  ㅇ  ㅈ  ㅊ  ㅋ  ㅌ  ㅍ  ㅎ
        static readonly string[] JongEng = {
            "", "r","R","rt","s","sw","sg","e","f","fr","fa","fq","ft","fx","fv","fg","a","q","qt","t","T","d","w","c","z","x","v","g"
        };

        // 호환 자모 (단독 입력 — 조합 안 된 상태) → 두벌식
        // U+3131 ~ U+3163
        static readonly Dictionary<char, string> JamoEng = new Dictionary<char, string>
        {
            // 자음
            {'ㄱ',"r"}, {'ㄲ',"R"}, {'ㄳ',"rt"}, {'ㄴ',"s"}, {'ㄵ',"sw"}, {'ㄶ',"sg"},
            {'ㄷ',"e"}, {'ㄸ',"E"}, {'ㄹ',"f"}, {'ㄺ',"fr"}, {'ㄻ',"fa"}, {'ㄼ',"fq"},
            {'ㄽ',"ft"}, {'ㄾ',"fx"}, {'ㄿ',"fv"}, {'ㅀ',"fg"}, {'ㅁ',"a"}, {'ㅂ',"q"},
            {'ㅃ',"Q"}, {'ㅄ',"qt"}, {'ㅅ',"t"}, {'ㅆ',"T"}, {'ㅇ',"d"}, {'ㅈ',"w"},
            {'ㅉ',"W"}, {'ㅊ',"c"}, {'ㅋ',"z"}, {'ㅌ',"x"}, {'ㅍ',"v"}, {'ㅎ',"g"},
            // 모음
            {'ㅏ',"k"}, {'ㅐ',"o"}, {'ㅑ',"i"}, {'ㅒ',"O"}, {'ㅓ',"j"}, {'ㅔ',"p"},
            {'ㅕ',"u"}, {'ㅖ',"P"}, {'ㅗ',"h"}, {'ㅘ',"hk"}, {'ㅙ',"ho"}, {'ㅚ',"hl"},
            {'ㅛ',"y"}, {'ㅜ',"n"}, {'ㅝ',"nj"}, {'ㅞ',"np"}, {'ㅟ',"nl"}, {'ㅠ',"b"},
            {'ㅡ',"m"}, {'ㅢ',"ml"}, {'ㅣ',"l"},
        };

        /// <summary>
        /// 입력 텍스트 안의 한글 char를 두벌식 영문으로 치환. ASCII/숫자/기호는 그대로.
        /// </summary>
        public static string Convert(string text)
        {
            if (string.IsNullOrEmpty(text)) return text;

            // 빠른 경로 — 한글이 하나도 없으면 원본 그대로 반환
            if (!ContainsHangul(text)) return text;

            var sb = new StringBuilder(text.Length * 2);
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if (ch >= HangulBase && ch <= HangulLast)
                {
                    // 완성형 한글 — 초/중/종성 분해
                    int code = ch - HangulBase;
                    int cho  = code / (JungsungCount * JongsungCount);
                    int jung = (code / JongsungCount) % JungsungCount;
                    int jong = code % JongsungCount;
                    sb.Append(ChoEng[cho]);
                    sb.Append(JungEng[jung]);
                    if (jong > 0) sb.Append(JongEng[jong]);
                }
                else if (JamoEng.TryGetValue(ch, out var jamoStr))
                {
                    sb.Append(jamoStr);
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        /// <summary>완성형 또는 호환 자모 한글 char가 하나라도 있는지.</summary>
        public static bool ContainsHangul(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            for (int i = 0; i < text.Length; i++)
            {
                char ch = text[i];
                if ((ch >= HangulBase && ch <= HangulLast) || JamoEng.ContainsKey(ch)) return true;
            }
            return false;
        }
    }
}
