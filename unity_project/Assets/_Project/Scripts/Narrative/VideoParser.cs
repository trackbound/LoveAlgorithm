using System;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 영상(Video) Value 순수 파서. EventBus·UnityEngine 비의존(EditMode 테스트). 형식
    /// <c>Video:파일명[:Loop][:Skippable|:NoSkip]</c> — 파일명 뒤 토큰은 순서무관·케이스무시 플래그.
    /// 기본값 Loop=false·Skippable=true(동결: 구 VideoLayer). 비-Video이거나 파일명 없으면 IsValid=false
    /// (PlayFx 캐스케이드의 다른 파서처럼 자기 head를 스스로 검사해 스킵 위임).
    /// 엔진(NarrativeController)이 결과를 <c>PlayVideoCommand</c>로 발행하고, VideoView가 Resources/Animation/{파일명}을 재생한다.
    /// </summary>
    public static class VideoParser
    {
        public static VideoIntent Parse(string value)
        {
            var r = new VideoIntent { Skippable = true }; // 동결 기본: 스킵 가능
            if (string.IsNullOrEmpty(value)) return r;

            // 콜론 분해: [0]=명령(Video) · [1]=파일명 · [2..]=플래그
            string[] parts = value.Split(':');
            if (parts.Length < 2) return r;
            if (!string.Equals(parts[0].Trim(), "Video", StringComparison.OrdinalIgnoreCase)) return r;

            r.Name = parts[1].Trim();
            if (string.IsNullOrEmpty(r.Name)) return r;

            for (int i = 2; i < parts.Length; i++)
            {
                string tok = parts[i].Trim();
                if (string.Equals(tok, "Loop", StringComparison.OrdinalIgnoreCase)) r.Loop = true;
                else if (string.Equals(tok, "NoSkip", StringComparison.OrdinalIgnoreCase)) r.Skippable = false;
                else if (string.Equals(tok, "Skippable", StringComparison.OrdinalIgnoreCase)) r.Skippable = true;
            }

            r.IsValid = true;
            return r;
        }
    }

    /// <summary>Video 분해 결과. Name(Resources/Animation/{Name})·Loop·Skippable(기본 true).</summary>
    public struct VideoIntent
    {
        public bool IsValid;
        public string Name;
        public bool Loop;
        public bool Skippable;
    }
}
