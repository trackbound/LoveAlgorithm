namespace LoveAlgo.Events
{
    /// <summary>
    /// 새 게임 시작 *의도*(EventBus). 타이틀의 <c>TitleView</c>가 발행 → <c>SceneFlowController</c>가 구독해
    /// 게임 씬을 로드한다. 씬 전환은 페이즈축(<see cref="RequestPhaseCommand"/>)과 분리된 별도 축이며,
    /// 씬별 자족 + EventBus로 처리한다(persistent 매니저 없음, ADR-013 씬 경계 / ADR-003 씬 전환).
    /// 파라미터 없음 — 새 게임 초기화는 게임 씬의 GameBootstrap이 수행한다.
    /// </summary>
    public readonly struct StartNewGameCommand
    {
    }

    /// <summary>
    /// 이어하기 *의도*(EventBus). 타이틀의 <c>TitleView</c> Continue 버튼이 발행 → <c>SceneFlowController</c>가
    /// 구독해 부팅 모드를 Continue로 설정하고 게임 씬을 로드한다. 게임 씬의 <c>GameBootstrap</c>이 오토세이브를
    /// 복원한다(<see cref="StartNewGameCommand"/> 새 게임 경로와 대칭). 파라미터 없음.
    /// </summary>
    public readonly struct ContinueGameCommand
    {
    }

    /// <summary>
    /// 게임 종료 *의도*(EventBus). 타이틀의 <c>TitleView</c> Exit 버튼이 발행 → <c>SceneFlowController</c>가
    /// 구독해 애플리케이션을 종료한다(에디터에선 PlayMode 정지). 종료도 "표시는 뷰, 동작은 구독자" 분리를
    /// 따른다(ADR-007) — 새 게임/이어하기와 같은 타이틀 메뉴 의도 축. 파라미터 없음.
    /// </summary>
    public readonly struct QuitGameCommand
    {
    }
}
