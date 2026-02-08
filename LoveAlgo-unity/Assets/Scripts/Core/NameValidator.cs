using System.Text.RegularExpressions;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 플레이어 이름 유효성 검증
    /// </summary>
    public static class NameValidator
    {
        // 길이 제한
        public const int MinLength = 2;
        public const int MaxLengthKorean = 6;    // 한글 최대 6자
        public const int MaxLengthEnglish = 12;  // 영어 최대 12자

        // 허용 패턴: 한글, 영문, 숫자만
        static readonly Regex ValidPattern = new Regex(@"^[가-힣a-zA-Z0-9]+$");

        // 금지어 (필요시 확장)
        static readonly string[] BannedWords = { "admin", "관리자", "운영자", "gm", "GM" };

        public enum Result
        {
            Valid,
            Empty,
            TooShort,
            TooLong,
            InvalidCharacter,
            BannedWord
        }

        /// <summary>
        /// 이름 유효성 검증
        /// </summary>
        public static Result Validate(string name)
        {
            // 빈 값 체크
            if (string.IsNullOrWhiteSpace(name))
                return Result.Empty;

            string trimmed = name.Trim();

            // 최소 길이
            if (trimmed.Length < MinLength)
                return Result.TooShort;

            // 최대 길이 (한글 포함 여부로 판단)
            int maxLength = ContainsKorean(trimmed) ? MaxLengthKorean : MaxLengthEnglish;
            if (trimmed.Length > maxLength)
                return Result.TooLong;

            // 허용 문자 체크
            if (!ValidPattern.IsMatch(trimmed))
                return Result.InvalidCharacter;

            // 금지어 체크
            string lower = trimmed.ToLower();
            foreach (var banned in BannedWords)
            {
                if (lower.Contains(banned.ToLower()))
                    return Result.BannedWord;
            }

            return Result.Valid;
        }

        /// <summary>
        /// 간단한 유효성 체크 (bool)
        /// </summary>
        public static bool IsValid(string name)
        {
            return Validate(name) == Result.Valid;
        }

        /// <summary>
        /// 결과에 따른 에러 메시지
        /// </summary>
        public static string GetErrorMessage(Result result)
        {
            return result switch
            {
                Result.Empty => "이름을 입력해주세요.",
                Result.TooShort => $"이름은 {MinLength}자 이상이어야 합니다.",
                Result.TooLong => "이름이 너무 깁니다.",
                Result.InvalidCharacter => "한글, 영문, 숫자만 사용할 수 있습니다.",
                Result.BannedWord => "사용할 수 없는 이름입니다.",
                _ => ""
            };
        }

        /// <summary>
        /// 한글 포함 여부
        /// </summary>
        static bool ContainsKorean(string text)
        {
            foreach (char c in text)
            {
                if (c >= '가' && c <= '힣')
                    return true;
            }
            return false;
        }
    }
}
