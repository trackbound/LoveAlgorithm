namespace LoveAlgo.Events
{
    // ── 로딩 화면 명령(LoadingScene/Loading) ──
    // Flow LoadingScene이 displayTime을 해석해 발행 → LoadingScreenView가 그 시간 동안 풀스크린 오버레이를 띄우고
    // 핸들을 푼다(ADR-007). 씬 전환 사이의 로딩 비트(구 LoadingScene 재작성). 대기형이라 엔진은 await로 기다린다.

    /// <summary>
    /// 로딩 화면 표시 명령. <see cref="Seconds"/> 동안 오버레이 표시 후 숨기고 핸들을 푼다.
    /// <see cref="Key"/>(선택)는 표시할 스플래시 컨텍스트 — 캐릭터 폴더 id(Roa/DoHeewon 등). 주면
    /// 이름에 그 키를 포함한 스플래시 중 무작위, 없으면(빈/매칭 0) 전체 무작위(LoadingScreenView 처리).
    /// </summary>
    public readonly struct ShowLoadingCommand
    {
        public readonly float Seconds;
        public readonly CompletionHandle Handle;
        public readonly string Key;

        public ShowLoadingCommand(float seconds, CompletionHandle handle, string key = null)
        {
            Seconds = seconds;
            Handle = handle;
            Key = key;
        }
    }
}
