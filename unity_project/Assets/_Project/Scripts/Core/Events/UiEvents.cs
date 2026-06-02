namespace LoveAlgo.Events
{
    /// <summary>
    /// UI 인스턴스 부모 그룹(모듈명 1:1) — 모듈이 자기 UI를 spawn하는 루트 + 그룹 단위 show/hide 대상.
    /// 구 <c>LoveAlgo.UI.UIGroup</c>의 의미 이식(네임스페이스만 Core로 — 명령 이벤트가 Core에 있어야
    /// 발행자/구독자가 공통 참조 가능). 구 enum과 다른 네임스페이스라 공존 충돌 없음.
    /// </summary>
    public enum UIGroup
    {
        Narrative,    // DialogueUI, ChoicePopup 등
        Simulation,   // ScheduleUI, ShopUI, QuickMenu 등
        Title         // TitlePanel, UsernameUI 등 진입 화면
    }

    /// <summary>
    /// 특정 UI 그룹만 표시(나머지 숨김)를 요청하는 명령 이벤트(EventBus). 발행자(페이즈 흐름/오케스트레이션,
    /// 후속)는 의도만 알리고, UIManager 구독자가 그룹 루트 활성/비활성을 수행한다(ADR-007).
    /// 구 <c>UIManager.ShowOnly</c>의 그룹 전환 의도를 Service Locator 없이 대체.
    /// </summary>
    public readonly struct ShowUiGroupCommand
    {
        public readonly UIGroup Group;
        public ShowUiGroupCommand(UIGroup group) { Group = group; }
    }
}
