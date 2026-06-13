using System;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 플레이어 이름 토큰(<c>{{Player}}</c>) 치환 순수 유틸(MoneyFormat 형제 — EditMode 테스트 대상).
    /// CSV는 화자 칼럼·본문 양쪽에 토큰을 쓴다(대소문자 무관). 치환은 엔진(NarrativeController)이
    /// 인라인 태그 분해 **전** 원문에 수행한다 — 분해 후 치환하면 태그 CharIndex가 어긋난다.
    /// 이름 미설정(빈 값)이면 <see cref="FallbackName"/> — UsernameScreen 입력 전 안전망.
    /// </summary>
    public static class PlayerNameFormat
    {
        public const string Token = "{{Player}}";

        /// <summary>주인공 예약 화자 ID — 캐릭터 코드(c01~c05)와 같은 축에서 로그/뷰가 주인공을 판별.
        /// StageView 슬롯 매칭엔 미등록이라 표정 명령은 무해 no-op.</summary>
        public const string PlayerSpeakerId = "player";

        public const string FallbackName = "플레이어";

        /// <summary>화자 칼럼이 플레이어 토큰인가(공백/대소문자 허용).</summary>
        public static bool IsPlayerSpeaker(string speaker)
            => speaker != null && speaker.Trim().Equals(Token, StringComparison.OrdinalIgnoreCase);

        /// <summary>문자열 내 모든 토큰을 이름으로 치환(대소문자 무관). 이름 빈 값 = 폴백.</summary>
        public static string Apply(string text, string playerName)
        {
            if (string.IsNullOrEmpty(text)) return text;
            string name = string.IsNullOrWhiteSpace(playerName) ? FallbackName : playerName.Trim();
            return text.Replace(Token, name, StringComparison.OrdinalIgnoreCase);
        }
    }
}
