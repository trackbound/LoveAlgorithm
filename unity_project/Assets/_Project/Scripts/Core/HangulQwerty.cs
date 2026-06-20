using System.Text;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 한글 입력을 두벌식 키보드 기준 QWERTY 키로 역매핑한다(예: 가→rk, ㅂㅈㄷ→qwe).
    /// 완성형 음절(가-힣)은 초/중/종성으로 분해해 각 자모를 매핑하고, 호환 자모(ㄱ-ㅣ)는 직접 매핑한다.
    /// 영문/숫자/표준 ASCII 특수문자(0x21–0x7E)는 그대로 통과, 그 외(공백·제어·기타 유니코드)는 제거.
    /// 비밀번호 입력의 한글→영문 통일·문자셋 제한에 사용(순수 — EditMode 테스트 대상).
    /// </summary>
    public static class HangulQwerty
    {
        // 초성 19
        static readonly string[] Cho = { "r","R","s","e","E","f","a","q","Q","t","T","d","w","W","c","z","x","v","g" };
        // 중성 21
        static readonly string[] Jung = { "k","o","i","O","j","p","u","P","h","hk","ho","hl","y","n","nj","np","nl","b","m","ml","l" };
        // 종성 28 (index 0 = 받침 없음)
        static readonly string[] Jong = { "","r","R","rt","s","sw","sg","e","f","fr","fa","fq","ft","fx","fv","fg","a","q","qt","t","T","d","w","c","z","x","v","g" };
        // 호환 자모 U+3131–U+3163 (ㄱ..ㅣ) 51개
        static readonly string[] Compat = {
            "r","R","rt","s","sw","sg","e","E","f","fr","fa","fq","ft","fx","fv","fg","a","q","Q","qt","t","T","d","w","W","c","z","x","v","g",
            "k","o","i","O","j","p","u","P","h","hk","ho","hl","y","n","nj","np","nl","b","m","ml","l"
        };

        public static string ToQwerty(string text)
        {
            if (string.IsNullOrEmpty(text)) return text ?? "";
            var sb = new StringBuilder(text.Length * 2);
            foreach (char c in text)
            {
                if (c >= 0xAC00 && c <= 0xD7A3) // 완성형 음절
                {
                    int s = c - 0xAC00;
                    sb.Append(Cho[s / (21 * 28)]);
                    sb.Append(Jung[(s % (21 * 28)) / 28]);
                    sb.Append(Jong[s % 28]);
                }
                else if (c >= 0x3131 && c <= 0x3163) // 호환 자모
                {
                    sb.Append(Compat[c - 0x3131]);
                }
                else if (c >= 0x21 && c <= 0x7E) // 영문/숫자/표준 ASCII 특수문자
                {
                    sb.Append(c);
                }
                // else: 제거(공백·제어·기타)
            }
            return sb.ToString();
        }
    }
}
