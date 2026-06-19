namespace LoveAlgo.Events
{
    // ── Extra(부가 콘텐츠) 팝업 커맨드 ──
    // ExtraView가 ShowExtraCommand로 표시(ADR-013 Overlay). 타이틀 Extra 버튼이 발행(ADR-007: 표시만).

    /// <summary>Extra 팝업 열기 요청. ExtraView가 구독해 표시(단순 show/hide Overlay).</summary>
    public readonly struct ShowExtraCommand { }
}
