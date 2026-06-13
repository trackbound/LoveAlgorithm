namespace LoveAlgo.Events
{
    /// <summary>
    /// 튜토리얼 시작 명령(EventBus). 발행: 스케줄 화면 첫 진입 감지(컨트롤러 자체) 또는 디버그.
    /// 수신한 TutorialController가 미완료(PlayerPrefs)일 때만 오버레이를 연다.
    /// </summary>
    public readonly struct StartTutorialCommand { }

    /// <summary>튜토리얼 종료 통지(완료 기록 후 발행) — 입력 잠금 해제 등 구독자 반응용.</summary>
    public readonly struct TutorialFinishedEvent { }
}
