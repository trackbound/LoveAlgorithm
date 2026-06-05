namespace LoveAlgo.Events
{
    // ── 위치 배너 명령(Place) ──
    // 순수 PlaceParser가 Place Value("제목 | 장소")를 분해 → 엔진(NarrativeController)이 동결 수치(PlaceTuningSO)로
    // 등장/유지/퇴장 지속을 해석 → 명령에 실어 발행 → 뷰(PlaceCardView)가 배너를 페이드 인→유지→아웃한다(ADR-007).
    // 구 PlaceNotification 배너의 재작성. 비블로킹(스크립트는 등장만 기다리거나 즉시 진행) — 배너가 자체 수명 관리.

    /// <summary>
    /// 위치 배너 표시 명령. <see cref="Title"/>=시간/사건 라벨(예 "[새 학기 첫날]"), <see cref="Place"/>=장소명.
    /// 지속들은 엔진이 동결 수치로 해석한 최종값(초). 핸들은 <b>등장 완료</b>(배너가 떠오른 시점)에 풀린다.
    /// </summary>
    public readonly struct ShowPlaceCommand
    {
        public readonly string Title;
        public readonly string Place;
        public readonly float EnterDuration;
        public readonly float HoldDuration;
        public readonly float ExitDuration;
        public readonly CompletionHandle Handle;

        public ShowPlaceCommand(string title, string place, float enterDuration, float holdDuration, float exitDuration, CompletionHandle handle)
        {
            Title = title;
            Place = place;
            EnterDuration = enterDuration;
            HoldDuration = holdDuration;
            ExitDuration = exitDuration;
            Handle = handle;
        }
    }
}
