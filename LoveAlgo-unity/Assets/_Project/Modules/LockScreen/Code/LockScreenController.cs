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
    /// 모드 분기, 비번 해시 검증, 오류 카운터, ToDo 추출.
    /// </summary>
    public class LockScreenController : MonoBehaviour, ILockScreen
    {
        [Header("Data")]
        [SerializeField] ToDoListSO toDoList;
        [SerializeField] LockScreenContentSO content;

        [Header("Settings")]
        [Tooltip("열쇠 아이콘 노출 임계치 (실패 횟수)")]
        [SerializeField] int keyIconThreshold = 3;

        // ── 상태 ──────────────────────────────────────────────
        public LockScreenMode CurrentMode { get; private set; } = LockScreenMode.Normal;
        public int FailCount { get; private set; }
        public bool ShowKeyIcon => FailCount >= keyIconThreshold;

        public bool IsPasswordSet
            => PlayerPrefs.HasKey(PrefsKeys.PasswordHash)
            && !string.IsNullOrEmpty(PlayerPrefs.GetString(PrefsKeys.PasswordHash));

        // ── 이벤트 ─────────────────────────────────────────────
        public event Action<int> OnPasswordFailed;
        public event Action OnUnlocked;
        public event Action<bool> OnPasswordSet;

        // ── 모드 진입 ──────────────────────────────────────────
        public void OpenForFirstSetup() => OpenMode(LockScreenMode.FirstSetup);
        public void OpenForNormal()     => OpenMode(LockScreenMode.Normal);
        public void OpenForReset()      => OpenMode(LockScreenMode.Reset);

        void OpenMode(LockScreenMode mode)
        {
            CurrentMode = mode;
            FailCount = 0;
            EventBus.Publish(new LockScreenOpenedEvent(mode));
        }

        // ── 비번 처리 ──────────────────────────────────────────
        public bool SetPassword(string pin4)
        {
            if (!PasswordHasher.IsValidPin4(pin4))
            {
                Debug.LogWarning("[LockScreen] SetPassword: invalid PIN format (require 4 digits)");
                return false;
            }

            string salt = PasswordHasher.GenerateSalt();
            string hash = PasswordHasher.Hash(pin4, salt);

            PlayerPrefs.SetString(PrefsKeys.PasswordSalt, salt);
            PlayerPrefs.SetString(PrefsKeys.PasswordHash, hash);
            PlayerPrefs.Save();

            bool isFirstTime = CurrentMode == LockScreenMode.FirstSetup;
            OnPasswordSet?.Invoke(isFirstTime);
            EventBus.Publish(new PasswordSetEvent(isFirstTime));
            return true;
        }

        public bool VerifyPassword(string pin4)
        {
            if (!PasswordHasher.IsValidPin4(pin4)) return RegisterFail();
            if (!IsPasswordSet) return RegisterFail();

            string salt = PlayerPrefs.GetString(PrefsKeys.PasswordSalt, "");
            string stored = PlayerPrefs.GetString(PrefsKeys.PasswordHash, "");
            string input = PasswordHasher.Hash(pin4, salt);

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

        // ── ToDo / 콘텐츠 ──────────────────────────────────────
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
    }
}
