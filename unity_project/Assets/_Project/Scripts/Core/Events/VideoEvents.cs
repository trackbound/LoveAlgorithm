namespace LoveAlgo.Events
{
    // ── 영상 재생 명령(Video) ──
    // FX Video가 파일명/Loop/Skippable을 해석해 발행 → VideoView가 Resources/Animation/{Name}을 풀스크린으로 재생.
    // 비-Loop는 종료까지 await(핸들 완료=loopPointReached/스킵), Loop는 비블로킹(핸들 즉시 완료, Reset까지 배경 유지).
    // CSV(VideoParser→NarrativeController)와 코드(직접 Publish)의 단일 진입점 — 둘 다 이 명령 하나로 영상을 튼다.

    /// <summary>풀스크린 영상 재생 명령. <see cref="Name"/>=Resources/Animation 하위 클립명(별칭 없이 코드명).</summary>
    public readonly struct PlayVideoCommand
    {
        public readonly string Name;
        public readonly bool Loop;
        public readonly bool Skippable;
        public readonly CompletionHandle Handle;

        public PlayVideoCommand(string name, bool loop, bool skippable, CompletionHandle handle)
        {
            Name = name;
            Loop = loop;
            Skippable = skippable;
            Handle = handle;
        }
    }
}
