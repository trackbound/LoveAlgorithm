using System;
using System.Globalization;

namespace LoveAlgo.Story
{
    /// <summary>
    /// FX 매크로 <c>SceneStart</c>/<c>SceneEnd</c>의 순수 파서. EventBus·UnityEngine 비의존(EditMode 테스트).
    /// 다른 연출 파서와 같은 "순수층" — 엔진(NarrativeController)이 결과를 받아 <b>기존 명령(EyeMask·BG)을 재발행</b>한다.
    ///
    /// 문법(STORY_COMMANDS.md):
    ///   <c>SceneStart[:bgPath[:EyeClose]]</c> — 씬 진입. bg를 즉시(Cut) 깔고, <c>EyeClose</c> 플래그면 눈을 즉시 감아
    ///     <b>유지</b>(암전 모놀로그·Wake 스타일), 없으면 눈을 뜨며 리빌(eyeOpen 0.6s) 후 pause(0.4s).
    ///   <c>SceneEnd[:fadeDuration]</c> — 씬 퇴장. 눈을 감아 암전(기본 0.5s). EyeMask라 대사/캐릭터는 가리지 않음
    ///     (구 Wake 패턴: SceneEnd→다음 씬 캐릭터/대사가 검은 화면 위로 진행 가능 — Prologue의 SceneEnd→Char→Text가 근거).
    ///
    /// head가 SceneStart/SceneEnd가 아니면 <see cref="SceneFxIntent.IsValid"/>=false(형제 파서로 위임).
    /// </summary>
    public static class SceneFxParser
    {
        public static SceneFxIntent Parse(string value)
        {
            var r = new SceneFxIntent { Duration = -1f };
            if (string.IsNullOrEmpty(value)) return r;

            var parts = value.Split(':');
            string head = parts[0].Trim();

            if (string.Equals(head, "SceneStart", StringComparison.OrdinalIgnoreCase))
            {
                r.IsValid = true;
                r.Kind = SceneFxKind.Start;
                // 나머지 토큰: EyeClose 플래그 또는 bgPath(처음 나오는 비-플래그 토큰).
                for (int i = 1; i < parts.Length; i++)
                {
                    string t = parts[i].Trim();
                    if (t.Length == 0) continue;
                    if (string.Equals(t, "EyeClose", StringComparison.OrdinalIgnoreCase)) r.EyeClose = true;
                    else if (r.Bg == null) r.Bg = t;
                }
                return r;
            }

            if (string.Equals(head, "SceneEnd", StringComparison.OrdinalIgnoreCase))
            {
                r.IsValid = true;
                r.Kind = SceneFxKind.End;
                if (parts.Length >= 2 &&
                    float.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float d) && d >= 0f)
                    r.Duration = d;
                return r;
            }

            return r;
        }
    }

    public enum SceneFxKind { Start, End }

    /// <summary>Scene 매크로 분해 결과. Start: Bg(null=미지정)·EyeClose. End: Duration(-1=기본).</summary>
    public struct SceneFxIntent
    {
        public bool IsValid;
        public SceneFxKind Kind;
        public string Bg;       // SceneStart 선택 배경(Cut)
        public bool EyeClose;   // SceneStart: 눈 감고 유지(암전 모놀로그)
        public float Duration;  // SceneEnd 눈감기 지속(-1=기본 0.5s)
    }
}
