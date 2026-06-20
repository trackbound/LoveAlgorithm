namespace LoveAlgo.Events
{
    // ── 빠른 진행(시프트 홀드) 상태 변경 알림 ──
    // ADR-007: 발행은 DialogueView(입력 감지), 표시는 FastForwardIndicatorView. 느슨한 연동.

    /// <summary>
    /// 시프트 홀드 빠른 진행의 on/off 상태가 바뀔 때 발행. <see cref="LoveAlgo.UI.FastForwardIndicatorView"/>가
    /// 구독해 화면 우측 상단에 "FAST" 인디케이터를 켜고 끈다. 상태가 실제로 바뀐 프레임에만 발행한다.
    /// (개발용 토스트와 달리 릴리즈 빌드에서도 동작 — 빌드본에서도 빠른 진행 중임을 표시.)
    /// </summary>
    public readonly struct FastForwardChanged
    {
        public readonly bool Active;
        public FastForwardChanged(bool active) => Active = active;
    }
}
