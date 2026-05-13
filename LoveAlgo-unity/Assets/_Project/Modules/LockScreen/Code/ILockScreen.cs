using System;
using System.Collections.Generic;
using LoveAlgo.LockScreen.Data;

namespace LoveAlgo.LockScreen
{
    /// <summary>
    /// PC잠금 모듈 외부 계약.
    /// 구현: <see cref="LockScreenController"/> (씬 GameObject), 노출: <see cref="LockScreenModule"/>.
    /// </summary>
    public interface ILockScreen
    {
        // ── 상태 ──
        LockScreenMode CurrentMode { get; }
        bool IsPasswordSet { get; }
        int FailCount { get; }
        bool ShowKeyIcon { get; }

        /// <summary>현재 콘텐츠 SO (안내 문구·시계·메시지 조회).</summary>
        LockScreenContentSO Content { get; }

        // ── 모드 진입 ──
        void OpenForFirstSetup();
        void OpenForNormal();
        /// <summary>재설정. 기획서 §오류/분실: 기존 비번 확인 X — FirstSetup과 동일 흐름.</summary>
        void OpenForReset();

        // ── 비번 처리 ──
        /// <summary>비밀번호(자유 문자, 1~7자) 신규/재설정 저장. 유효성 실패 시 false.</summary>
        bool SetPassword(string pwd);
        /// <summary>비밀번호 검증. 성공 시 OnUnlocked + FailCount 리셋. 실패 시 OnPasswordFailed.</summary>
        bool VerifyPassword(string pwd);
        void ClearPassword();

        // ── ToDo / 콘텐츠 ──
        IReadOnlyList<ToDoItemSO> GetRandomToDos(int count = 3);
        string GetRoaMessage(int index);
        string GetHint(LockScreenHint kind);
        /// <summary>시계 표시값 (Content.fixedClockTime 비어있으면 실시간 OS 시각).</summary>
        string GetClockTime();
        /// <summary>시계 1회 오버라이드 (CSV Time= 인자에 사용). 빈 문자열이면 SO 기본값 복귀.</summary>
        void SetClockOverride(string hhmm);

        // ── 이벤트 ──
        event Action<int> OnPasswordFailed;
        event Action OnUnlocked;
        event Action<bool> OnPasswordSet;
    }
}
