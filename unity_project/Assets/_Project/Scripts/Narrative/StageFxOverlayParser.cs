using System;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스테이지 투명 오버레이 FX Value 순수 파서. EventBus·UnityEngine 비의존(EditMode 테스트). 형식
    /// <c>StageFx:파일명[:Loop]</c> — 파일명 뒤 토큰은 순서무관·케이스무시 플래그. 기본 Loop=false(1회 재생 후
    /// 자동 종료). 비-StageFx이거나 파일명 없으면 IsValid=false(PlayFx 캐스케이드의 다른 파서처럼 자기 head를
    /// 스스로 검사해 스킵 위임). 엔진(NarrativeController)이 결과를 <c>PlayStageFxCommand</c>로 발행하고,
    /// StageFxOverlayView가 Resources/Animation/{파일명} 투명 클립을 캐릭터 위·대사 아래에 재생한다.
    /// </summary>
    public static class StageFxOverlayParser
    {
        public static StageFxIntent Parse(string value)
        {
            var r = new StageFxIntent();
            if (string.IsNullOrEmpty(value)) return r;

            string[] parts = value.Split(':');
            if (parts.Length < 2) return r;
            if (!string.Equals(parts[0].Trim(), "StageFx", StringComparison.OrdinalIgnoreCase)) return r;

            r.Name = parts[1].Trim();
            if (string.IsNullOrEmpty(r.Name)) return r;

            for (int i = 2; i < parts.Length; i++)
            {
                string tok = parts[i].Trim();
                if (string.Equals(tok, "Loop", StringComparison.OrdinalIgnoreCase)) r.Loop = true;
            }

            r.IsValid = true;
            return r;
        }
    }

    /// <summary>StageFx 분해 결과. Name(Resources/Animation/{Name})·Loop(기본 false).</summary>
    public struct StageFxIntent
    {
        public bool IsValid;
        public string Name;
        public bool Loop;
    }
}
