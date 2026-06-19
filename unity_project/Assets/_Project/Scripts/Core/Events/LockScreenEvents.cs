namespace LoveAlgo.Events
{
    // ── 잠금화면 Overlay 명령(LockScreen, ADR-013 첫 Overlay 구현) ──
    // Flow LockScreen이 mode/옵션을 해석해 발행 → LockScreenController가 잠금화면을 띄우고(현 화면 위 인터스티셜,
    // GamePhase 무변), 입력 완료 시 핸들을 푼다(ADR-007 완료-핸들 = await). 이번 슬라이스는 FirstSetup만 구현.

    /// <summary>잠금화면 모드(STORY_COMMANDS). 이번 슬라이스는 FirstSetup만 구현, 나머지는 후속.</summary>
    public enum LockMode
    {
        FirstSetup, // 비번 첫 설정(평문 입력, 마스킹 없음)
        Normal,     // 평상 잠금(저장 비번 검증, * 마스킹)
        Reset,      // 재설정
        Auto,       // 설정 여부 자동 판별(있으면 Normal, 없으면 FirstSetup)
        GameStart   // 게임 첫 시작 sugar(5s 페이드인 + Auto)
    }

    /// <summary>
    /// 잠금화면 표시 명령. <see cref="Mode"/>별 동작, <see cref="FadeOut"/>=true면 종료 시 검은 화면을 노출까지
    /// 페이드(black→0), <see cref="TimeOverride"/>=시계 1회 오버라이드(없으면 null). 입력 완료 시 핸들을 푼다.
    /// </summary>
    public readonly struct ShowLockScreenCommand
    {
        public readonly LockMode Mode;
        public readonly bool FadeOut;
        public readonly string TimeOverride;
        public readonly CompletionHandle Handle;

        public ShowLockScreenCommand(LockMode mode, bool fadeOut, string timeOverride, CompletionHandle handle)
        {
            Mode = mode;
            FadeOut = fadeOut;
            TimeOverride = timeOverride;
            Handle = handle;
        }
    }

    /// <summary>비밀번호 입력 확정 명령. LockScreenView(입력 UI) → LockScreenController(저장·핸들 완료)로 전달(ADR-007).</summary>
    public readonly struct SubmitPasswordCommand
    {
        public readonly string Password;
        public SubmitPasswordCommand(string password) { Password = password; }
    }

    /// <summary>비밀번호 검증 실패(Normal 불일치) 통지 — Controller→View(진동 + 누적 오류 횟수). ErrorCount는 1부터.</summary>
    public readonly struct PasswordVerifyFailedEvent
    {
        public readonly int ErrorCount;
        public PasswordVerifyFailedEvent(int errorCount) { ErrorCount = errorCount; }
    }

    /// <summary>비밀번호 수락(Normal 일치) 통지 — Controller→View(잠금화면 닫기). 저장/핸들 완료는 Controller가 함께 수행한다.</summary>
    public readonly struct PasswordAcceptedEvent { }

    /// <summary>비밀번호 재설정 요청(분실 모달 '예') — KeyResetButton→Controller(_mode=Reset)·View(Reset UI 재구성). 핸들은 유지(현 잠금 세션 그대로).</summary>
    public readonly struct RequestPasswordResetCommand { }
}
