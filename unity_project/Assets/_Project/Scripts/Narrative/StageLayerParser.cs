using System.Globalization;
using LoveAlgo.Events; // LayerTransition

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스테이지 레이어 파싱 결과(순수). CG/SD/Overlay 공통 — 종류(Kind)는 LineType에서 엔진이 정하므로 여기엔 없다.
    /// <see cref="IsClose"/>=true면 종료. <see cref="Duration"/>&lt;0이면 미지정(엔진이 동결값). 표시 시 <see cref="Name"/> 필수.
    /// </summary>
    public readonly struct StageLayerIntent
    {
        public readonly bool IsClose;
        public readonly string Name;
        public readonly LayerTransition Transition;
        public readonly float Duration;
        public readonly bool IsValid;

        public StageLayerIntent(bool isClose, string name, LayerTransition transition, float duration, bool isValid)
        {
            IsClose = isClose;
            Name = name;
            Transition = transition;
            Duration = duration;
            IsValid = isValid;
        }

        public static StageLayerIntent Invalid => new StageLayerIntent(false, null, LayerTransition.Fade, -1f, false);
    }

    /// <summary>
    /// 스테이지 레이어(CG/SD/Overlay) Value 순수 파서(M3 슬라이스2). EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 요구사항(REWRITE_FEATURE_INVENTORY §CG/SD/Overlay)의 기능만 도출 — 이미지 레이어를 페이드로 보이고/닫기.
    /// 동결 fade 시간은 StageLayerTuningSO로(ADR-012). 종류별 z-위치·CG 결합은 뷰/엔진 소관(파서 무관).
    ///
    /// 문법(스토리 데이터 1:1): <c>imageName[:transition[:dur]]</c> 표시 · <c>Close/Exit/Hide/FadeOut[:dur]</c> 종료.
    /// transition: Cut/Fade(기본 Fade). 예: <c>cg_c01_01:Fade:4.0</c> · <c>sd_c04_01</c> · <c>Close</c>.
    /// </summary>
    public static class StageLayerParser
    {
        public static StageLayerIntent Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) return StageLayerIntent.Invalid;

            var parts = value.Split(':');
            string head = parts[0].Trim();
            switch (head.ToLowerInvariant())
            {
                case "close":
                case "exit":
                case "hide":
                case "fadeout":
                {
                    float closeDur = parts.Length >= 2 && TryFloat(parts[1], out float cd) ? cd : -1f;
                    return new StageLayerIntent(true, null, LayerTransition.Fade, closeDur, true);
                }
            }

            // 표시: imageName[:transition[:dur]]
            var transition = LayerTransition.Fade;
            if (parts.Length >= 2 && parts[1].Trim().ToLowerInvariant() == "cut")
                transition = LayerTransition.Cut;
            float dur = parts.Length >= 3 && TryFloat(parts[2], out float d) ? d : -1f;
            return new StageLayerIntent(false, head, transition, dur, true);
        }

        static bool TryFloat(string s, out float value) =>
            float.TryParse(s.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
