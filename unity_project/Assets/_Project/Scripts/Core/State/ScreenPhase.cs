namespace LoveAlgo.Core
{
    /// <summary>
    /// 화면 페이즈(상호배타 *목적지*) — 현재 활성 화면. State SO 단일 진실원(ADR-013). 값 하나라 "두 화면 동시 활성"이
    /// 구조적으로 불가. 부팅 리셋값 = <see cref="Schedule"/>(인게임 자유행동). 런타임 전용(부팅 리셋, 세이브 비직렬화).
    ///
    /// 명칭: ADR-013은 'GamePhase'였으나 구 게임흐름 enum <c>LoveAlgo.Core.GamePhase</c>
    /// (Title/Username/Prologue/DayLoop/Ending, Assembly-CSharp)와 동일 풀네임 충돌(CS0433) 회피 + "화면 페이즈" 의미
    /// 정합을 위해 ScreenPhase로 확정(ADR이 허용한 "구현 시 확정"). 구 enum은 더 넓은 게임흐름, 본 enum은 화면 한정.
    /// Title(별도 씬=씬로드, ADR-003)·Overlay(완료핸들 인터스티셜)는 페이즈가 아니라 별도 축 — 범위 밖(과설계 게이트).
    /// </summary>
    public enum ScreenPhase
    {
        Story,     // 내러티브(대사/선택지) 화면
        Schedule,  // 자유행동 스케줄/시뮬레이션 화면
        Ending     // 30일 종료 엔딩 화면(인게임 종착)
    }
}
