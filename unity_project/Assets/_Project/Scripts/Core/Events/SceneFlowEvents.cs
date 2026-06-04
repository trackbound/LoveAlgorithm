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
}
