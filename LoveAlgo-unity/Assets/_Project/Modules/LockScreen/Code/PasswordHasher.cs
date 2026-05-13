using System;
using System.Security.Cryptography;
using System.Text;

namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// 비밀번호 단방향 해시 유틸 (SHA256 + salt).
    /// </summary>
    public static class PasswordHasher
    {
        /// <summary>최소 비번 길이.</summary>
        public const int MinLength = 1;

        /// <summary>최대 비번 길이 (기획서: 7자).</summary>
        public const int MaxLength = 7;

        /// <summary>새 salt 생성 (16바이트 랜덤 → Base64).</summary>
        public static string GenerateSalt()
        {
            byte[] bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>(salt + pwd) SHA256 해시 → Base64.</summary>
        public static string Hash(string pwd, string saltBase64)
        {
            if (pwd == null) pwd = "";
            if (saltBase64 == null) saltBase64 = "";

            using (var sha = SHA256.Create())
            {
                byte[] saltBytes = Convert.FromBase64String(saltBase64);
                byte[] pwdBytes = Encoding.UTF8.GetBytes(pwd);

                byte[] combined = new byte[saltBytes.Length + pwdBytes.Length];
                Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
                Buffer.BlockCopy(pwdBytes, 0, combined, saltBytes.Length, pwdBytes.Length);

                byte[] hash = sha.ComputeHash(combined);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// 비밀번호 형식 검증.
        /// 기획서: 최대 7자, 문자 제한 없음 (한글 포함 자유 입력).
        /// </summary>
        public static bool IsValidPassword(string pwd, int minLen = MinLength, int maxLen = MaxLength)
        {
            if (string.IsNullOrEmpty(pwd)) return false;
            if (pwd.Length < minLen || pwd.Length > maxLen) return false;
            return true;
        }
    }
}
