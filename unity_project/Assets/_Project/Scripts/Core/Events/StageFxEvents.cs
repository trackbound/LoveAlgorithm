namespace LoveAlgo.Events
{
    // ── 스테이지 투명 오버레이 FX 명령 ──
    // 캐릭터 위·대사 UI 아래에 투명 영상 효과를 얹는다(qtrle 소스는 VideoPlayer 재생 불가라
    // VP8 알파 webm으로 변환해 사용). 논블로킹 — 엔진(NarrativeController)이 이 명령을 발행한 뒤
    // 곧장 다음 줄로 진행하므로 완료 핸들이 없다(효과가 도는 동안 Char Emote가 인터리브).
    // 풀스크린 불투명 PlayVideoCommand/VideoView와 별개 경로(레이어·블로킹·탭처리 모두 다름).

    /// <summary>스테이지 투명 오버레이 FX 재생 명령. <see cref="Name"/>=Resources/Animation/{Name}. Loop=무한 유지(기본 1회).</summary>
    public readonly struct PlayStageFxCommand
    {
        public readonly string Name;
        public readonly bool Loop;

        public PlayStageFxCommand(string name, bool loop)
        {
            Name = name;
            Loop = loop;
        }
    }
}
