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
        /// <summary>
        /// 새 salt 생성 (16바이트 랜덤 → Base64).
        /// </summary>
        public static string GenerateSalt()
        {
            byte[] bytes = new byte[16];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// (salt + pin) SHA256 해시 → Base64.
        /// </summary>
        public static string Hash(string pin, string saltBase64)
        {
            if (pin == null) pin = "";
            if (saltBase64 == null) saltBase64 = "";

            using (var sha = SHA256.Create())
            {
                byte[] saltBytes = Convert.FromBase64String(saltBase64);
                byte[] pinBytes = Encoding.UTF8.GetBytes(pin);

                byte[] combined = new byte[saltBytes.Length + pinBytes.Length];
                Buffer.BlockCopy(saltBytes, 0, combined, 0, saltBytes.Length);
                Buffer.BlockCopy(pinBytes, 0, combined, saltBytes.Length, pinBytes.Length);

                byte[] hash = sha.ComputeHash(combined);
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// 4자리 PIN 형식 검증 (0-9 4자).
        /// </summary>
        public static bool IsValidPin4(string pin)
        {
            if (string.IsNullOrEmpty(pin) || pin.Length != 4) return false;
            for (int i = 0; i < 4; i++)
            {
                if (pin[i] < '0' || pin[i] > '9') return false;
            }
            return true;
        }
    }
}
