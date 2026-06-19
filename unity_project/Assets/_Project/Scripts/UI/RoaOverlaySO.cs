using System;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Events; // RoaDevice

namespace LoveAlgo.UI
{
    /// <summary>
    /// 로아 오버레이 규칙 정의 SO. 표정 코드→감정 카테고리(긍정/부정만 나열, 나머지=기본 폴백)와
    /// {device}_{category} 파일명(pc_긍정 등)을 데이터로 보유한다. 컨트롤러(RoaOverlayController)만 소비.
    /// 표정은 런타임에 해석된 코드(00/41…)로 흐르므로 코드 기준으로 매칭한다.
    /// </summary>
    [CreateAssetMenu(fileName = "RoaOverlay", menuName = "LoveAlgo/Roa Overlay")]
    public class RoaOverlaySO : ScriptableObject
    {
        public enum Category { Default, Positive, Negative }

        [Tooltip("이 캐릭터 코드 ID가 등장할 때만 오버레이를 결합한다.")]
        [SerializeField] string roaCharId = "roa";
        [Tooltip("긍정 카테고리 표정 코드(해석된 코드, 예: 41 42). 미나열 표정은 전부 기본.")]
        [SerializeField] string[] positiveEmotes;
        [Tooltip("부정 카테고리 표정 코드.")]
        [SerializeField] string[] negativeEmotes;

        [Header("파일명 규칙 {prefix}_{suffix} — Resources/Overlay/ 파일명과 일치해야 함")]
        [SerializeField] string pcPrefix = "pc";
        [SerializeField] string mobilePrefix = "모바일";
        [SerializeField] string defaultSuffix = "기본";
        [SerializeField] string positiveSuffix = "긍정";
        [SerializeField] string negativeSuffix = "부정";
        [Tooltip("등장 시 디바이스 토큰이 없거나 아직 미설정일 때의 기본 디바이스.")]
        [SerializeField] RoaDevice defaultDevice = RoaDevice.Pc;

        public string RoaCharId => roaCharId;
        public RoaDevice DefaultDevice => defaultDevice;

        public Category ResolveCategory(string emoteCode) => ResolveCategory(positiveEmotes, negativeEmotes, emoteCode);

        public string OverlayName(RoaDevice device, Category category) =>
            $"{(device == RoaDevice.Mobile ? mobilePrefix : pcPrefix)}_{SuffixFor(category)}";

        string SuffixFor(Category c) =>
            c == Category.Positive ? positiveSuffix : c == Category.Negative ? negativeSuffix : defaultSuffix;

        /// <summary>순수 카테고리 룩업: 긍정/부정 코드 일치(대소문자 무시), 미등록/공백=기본.</summary>
        public static Category ResolveCategory(IReadOnlyList<string> positive, IReadOnlyList<string> negative, string emoteCode)
        {
            if (Contains(positive, emoteCode)) return Category.Positive;
            if (Contains(negative, emoteCode)) return Category.Negative;
            return Category.Default;
        }

        static bool Contains(IReadOnlyList<string> list, string code)
        {
            if (list == null || string.IsNullOrWhiteSpace(code)) return false;
            string k = code.Trim();
            for (int i = 0; i < list.Count; i++)
                if (!string.IsNullOrEmpty(list[i]) && string.Equals(list[i].Trim(), k, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }

        /// <summary>테스트/에디터 셋업용 — 직렬화 필드를 코드로 채운다(런타임 미사용).</summary>
        public void Configure(string roaId, string[] positive, string[] negative, RoaDevice defaultDevice)
        {
            roaCharId = roaId;
            positiveEmotes = positive;
            negativeEmotes = negative;
            this.defaultDevice = defaultDevice;
        }
    }
}
