using System;
using System.Collections.Generic;
using LoveAlgo.LockScreen.Data;

namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// PC잠금 모듈 외부 계약.
    /// 구현: <see cref="LockScreenModule"/>.
    /// </summary>
    public interface ILockScreen
    {
        // ── 상태 ──────────────────────────────────────────────
        LockScreenMode CurrentMode { get; }

        /// <summary>비번이 저장돼 있는지 (PrefsKeys에 해시 존재).</summary>
        bool IsPasswordSet { get; }

        /// <summary>현재 세션 실패 횟수.</summary>
        int FailCount { get; }

        /// <summary>3회 이상 실패 시 열쇠 아이콘 노출.</summary>
        bool ShowKeyIcon { get; }

        // ── 모드 진입 ──────────────────────────────────────────
        void OpenForFirstSetup();
        void OpenForNormal();
        void OpenForReset();

        // ── 비번 처리 ──────────────────────────────────────────
        /// <summary>4자리 PIN 신규/재설정 저장. 유효성 실패 시 false.</summary>
        bool SetPassword(string pin4);

        /// <summary>4자리 PIN 검증. 성공 시 OnUnlocked 발행.</summary>
        bool VerifyPassword(string pin4);

        /// <summary>저장된 비번 삭제 (디버그/초기화용).</summary>
        void ClearPassword();

        // ── ToDo ───────────────────────────────────────────────
        IReadOnlyList<ToDoItemSO> GetRandomToDos(int count = 3);

        /// <summary>로아 메시지 (인덱스 0~3).</summary>
        string GetRoaMessage(int index);

        // ── 이벤트 ─────────────────────────────────────────────
        event Action<int> OnPasswordFailed; // (failCount)
        event Action OnUnlocked;
        event Action<bool> OnPasswordSet;   // (isFirstTime)
    }
}
