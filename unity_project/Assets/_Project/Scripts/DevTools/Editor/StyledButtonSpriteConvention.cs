using System.Collections.Generic;

namespace LoveAlgo.DevTools.Editor
{
    /// <summary>
    /// UI 버튼 스프라이트 네이밍 규약 → <see cref="LoveAlgo.UI.StyledButton"/> 상태 필드 매핑(순수 문자열 로직,
    /// UnityEditor/GameObject 무관 — EditMode 단위테스트 대상).
    ///
    /// 규약: base <c>btn_{module}_{purpose}</c> + 상태 형제 <c>_hover</c>(highlighted)·<c>_disabled</c>(disabled)·
    /// <c>_on</c>(selected/토글). <c>normal</c>·<c>pressed</c> 는 의도적으로 설정하지 않는다 — normal 은 Image 의
    /// 기본 스프라이트가 노출되고, pressed 는 hover 폴백 위에 네이티브 ColorTint(C8C8C8)가 곱해진다.
    /// 화이트리스트 접미사만 상태로 인식하므로 <c>_kr</c>(로케일) 등은 자동 무시된다.
    /// </summary>
    public static class StyledButtonSpriteConvention
    {
        public const string HoverSuffix = "_hover";
        public const string DisabledSuffix = "_disabled";
        public const string OnSuffix = "_on";

        /// <summary>해석 결과. 각 필드는 형제 스프라이트의 이름(확장자 없음) 또는 null(=해당 StyledButton 필드 비움).</summary>
        public readonly struct Resolution
        {
            public readonly string Highlighted;
            public readonly string Disabled;
            public readonly string Selected;

            public Resolution(string highlighted, string disabled, string selected)
            {
                Highlighted = highlighted;
                Disabled = disabled;
                Selected = selected;
            }

            public bool Any => Highlighted != null || Disabled != null || Selected != null;
        }

        /// <summary>이름이 상태 변형(_hover/_disabled/_on)으로 끝나는가.</summary>
        public static bool IsStateVariant(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            return EndsWith(name, HoverSuffix) || EndsWith(name, DisabledSuffix) || EndsWith(name, OnSuffix);
        }

        /// <summary>끝의 상태 접미사 1개를 제거해 base 이름을 돌려준다(상태형이 아니면 원본 그대로).</summary>
        public static string NormalizeBase(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            if (EndsWith(name, HoverSuffix)) return name.Substring(0, name.Length - HoverSuffix.Length);
            if (EndsWith(name, DisabledSuffix)) return name.Substring(0, name.Length - DisabledSuffix.Length);
            if (EndsWith(name, OnSuffix)) return name.Substring(0, name.Length - OnSuffix.Length);
            return name;
        }

        /// <summary>base 이름과 같은 폴더의 가용 스프라이트 이름 집합에서 상태 형제를 해석한다.</summary>
        public static Resolution Resolve(string baseName, ISet<string> availableNames)
        {
            if (string.IsNullOrEmpty(baseName) || availableNames == null)
                return new Resolution(null, null, null);

            string h = baseName + HoverSuffix;
            string d = baseName + DisabledSuffix;
            string s = baseName + OnSuffix;
            return new Resolution(
                availableNames.Contains(h) ? h : null,
                availableNames.Contains(d) ? d : null,
                availableNames.Contains(s) ? s : null);
        }

        static bool EndsWith(string s, string suffix) => s.EndsWith(suffix, System.StringComparison.Ordinal);
    }
}
