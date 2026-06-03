namespace LoveAlgo.Events
{
    /// <summary>
    /// 연출 명령 1건의 완료 신호(공용). 명령에 참조형으로 실려, 뷰(DialogueView/StageView/ScreenFadeView)가 표시/
    /// 전환을 마치면 <see cref="Complete"/>를 호출하고, 엔진(NarrativeController) 코루틴은 Next=await/click일 때
    /// <see cref="IsComplete"/>가 참이 될 때까지 대기한다(ADR-007의 "완료-핸들 실은 이벤트").
    ///
    /// 대사/스테이지/스크린FX가 동일한 "완료 통지"만 필요하므로 도메인별 핸들(구 DialogueRequest/StageRequest/
    /// FxRequest)을 이 하나로 통합한다 — 기능마다 동일 구조를 복제하지 않기 위함. 선택 결과처럼 값을 회수해야 하는
    /// 경우는 별도 핸들(<see cref="ChoiceRequest"/>)을 쓴다.
    /// </summary>
    public sealed class CompletionHandle
    {
        public bool IsComplete { get; private set; }
        public void Complete() => IsComplete = true;
    }
}
