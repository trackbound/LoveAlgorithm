using System;
using System.Collections.Generic;
using LoveAlgo.Common;
using LoveAlgo.LockScreen.Data;
using LoveAlgo.LockScreen.Events;
using UnityEngine;

namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// PC잠금 핵심 로직.
    /// 모드 분기, 비번 해시 검증, 오류 카운터, ToDo 추출, 안내 문구·시계 조회.
    /// </summary>
    public class LockScreenController : MonoBehaviour, ILockScreen
    {
        [Header("Data")]
        [SerializeField] ToDoListSO toDoList;
        [SerializeField] LockScreenContentSO content;

        [Header("Settings")]
        [Tooltip("열쇠 아이콘 노출 임계치 (실패 횟수)")]
        [SerializeField] int keyIconThreshold = 3;

        public LockScreenMode CurrentMode { get; private set; } = LockScreenMode.Normal;
        public int FailCount { get; private set; }
        public bool ShowKeyIcon => FailCount >= keyIconThreshold;
        public LockScreenContentSO Content => content;

        public bool IsPasswordSet
            => PlayerPrefs.HasKey(PrefsKeys.PasswordHash)
            && !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefsKeys.PasswordHash));

        string clockOverride;

        public event Action<int> OnPasswordFailed;
        public event Action OnUnlocked;
        public event Action<bool> OnPasswordSet;

        // ── 모드 진입 ──
        public void OpenForFirstSetup() => OpenMode(LockScreenMode.FirstSetup);
        public void OpenForNormal()     => OpenMode(LockScreenMode.Normal);
        /// <summary>기획서 §오류/분실: 기존 비번 확인 X — 바로 새 비번 설정 흐름.</summary>
        public void OpenForReset()      => OpenMode(LockScreenMode.Reset);

        void OpenMode(LockScreenMode mode)
        {
            CurrentMode = mode;
            FailCount = 0;
            EventBus.Publish(new LockScreenOpenedEvent(mode));
        }

        // ── 비번 처리 ──
        public bool SetPassword(string pwd)
        {
            if (!PasswordHasher.IsValidPassword(pwd))
            {
                Debug.LogWarning($"[LockScreen] SetPassword: invalid format (require {PasswordHasher.MinLength}~{PasswordHasher.MaxLength} chars)");
                return false;
            }
            string salt = PasswordHasher.GenerateSalt();
            string hash = PasswordHasher.Hash(pwd, salt);
            PlayerPrefs.SetString(PrefsKeys.PasswordSalt, salt);
            PlayerPrefs.SetString(PrefsKeys.PasswordHash, hash);
            PlayerPrefs.Save();

            // 첫 시작만 isFirstTime=true. Reset은 false (덮어쓰기).
            bool isFirstTime = CurrentMode == LockScreenMode.FirstSetup;
            FailCount = 0;
            OnPasswordSet?.Invoke(isFirstTime);
            EventBus.Publish(new PasswordSetEvent(isFirstTime));
            return true;
        }

        public bool VerifyPassword(string pwd)
        {
            if (!PasswordHasher.IsValidPassword(pwd)) return RegisterFail();
            if (!IsPasswordSet) return RegisterFail();

            string salt = PlayerPrefs.GetString(PrefsKeys.PasswordSalt, "");
            string stored = PlayerPrefs.GetString(PrefsKeys.PasswordHash, "");
            string input = PasswordHasher.Hash(pwd, salt);

            if (input == stored)
            {
                FailCount = 0;
                OnUnlocked?.Invoke();
                EventBus.Publish(new UnlockedEvent());
                return true;
            }
            return RegisterFail();
        }

        bool RegisterFail()
        {
            FailCount++;
            OnPasswordFailed?.Invoke(FailCount);
            EventBus.Publish(new PasswordFailedEvent(FailCount, ShowKeyIcon));
            return false;
        }

        public void ClearPassword()
        {
            PlayerPrefs.DeleteKey(PrefsKeys.PasswordHash);
            PlayerPrefs.DeleteKey(PrefsKeys.PasswordSalt);
            PlayerPrefs.Save();
            FailCount = 0;
        }

        // ── ToDo / 콘텐츠 ──
        public IReadOnlyList<ToDoItemSO> GetRandomToDos(int count = 3)
        {
            if (toDoList == null) return Array.Empty<ToDoItemSO>();
            return toDoList.PickRandom(count);
        }

        public string GetRoaMessage(int index)
        {
            if (content == null || content.roaMessages == null) return "";
            if (index < 0 || index >= content.roaMessages.Length) return "";
            return content.roaMessages[index] ?? "";
        }

        public string GetHint(LockScreenHint kind)
        {
            if (content == null) return DefaultHint(kind);
            switch (kind)
            {
                case LockScreenHint.FirstSetup: return content.hintFirstSetup;
                case LockScreenHint.Complete:   return content.hintComplete;
                case LockScreenHint.Normal:     return content.hintNormal;
                case LockScreenHint.Forgot:     return content.hintForgot;
                default: return "";
            }
        }

        static string DefaultHint(LockScreenHint kind)
        {
            // Content SO 미할당 폴백 (CLAUDE.md §4)
            switch (kind)
            {
                case LockScreenHint.FirstSetup: return "앞으로 사용할 비밀번호를 입력해주세요.\n최대 7자까지 입력 가능합니다.";
                case LockScreenHint.Complete:   return "비밀번호 설정 완료!";
                case LockScreenHint.Normal:     return "비밀번호를 입력해주세요.";
                case LockScreenHint.Forgot:     return "비밀번호를 잊으셨다면 우측 하단 열쇠 모양 버튼을 눌러주세요.";
                default: return "";
            }
        }

        public string GetClockTime()
        {
            if (!string.IsNullOrEmpty(clockOverride)) return clockOverride;
            if (content != null && !string.IsNullOrEmpty(content.fixedClockTime)) return content.fixedClockTime;
            return DateTime.Now.ToString("HH:mm");
        }

        public void SetClockOverride(string hhmm) { clockOverride = hhmm; }
    }
}
