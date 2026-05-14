using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// VirtualOverlay 데이터 — 가상 캐릭터(예: 로아)의 화면-점유 오버레이 정의.
    /// Stage 모듈의 VirtualBGOverlay와 1:1 대응.
    /// 일반 캐릭터(단일 스프라이트)와 분리되어 SRP 충족.
    /// </summary>
    [CreateAssetMenu(fileName = "VirtualOverlayDatabase", menuName = "LoveAlgo/Virtual Overlay Database")]
    public class VirtualOverlayDatabase : ScriptableObject
    {
        static VirtualOverlayDatabase _instance;

        static bool _loaded;

        /// <summary>
        /// Resources/Data/VirtualOverlayDatabase 로드. 없으면 null 반환 (옵셔널).
        /// 가상 캐릭터(로아 등)가 없는 시나리오에서는 자산 자체가 불필요하므로 무로그.
        /// </summary>
        public static VirtualOverlayDatabase Instance
        {
            get
            {
                if (!_loaded)
                {
                    _instance = Resources.Load<VirtualOverlayDatabase>("Data/VirtualOverlayDatabase");
                    _loaded = true;
                }
                return _instance;
            }
        }

        [Header("가상 캐릭터 오버레이 목록")]
        public List<VirtualOverlayEntry> entries = new();

        public VirtualOverlayEntry GetById(string characterId) =>
            entries.Find(e => e.characterId.Equals(characterId, StringComparison.OrdinalIgnoreCase));
    }

    [Serializable]
    public class VirtualOverlayEntry
    {
        [Tooltip("캐릭터 ID (CharacterMetaDatabase와 동일, 예: c01)")]
        public string characterId = "";

        [Tooltip("오버레이 prefix (예: Roa). 모드와 variant는 런타임에 합성")]
        public string overlayPrefix = "";

        [Tooltip("유효 모드 목록 (예: Mob, PC)")]
        public List<string> overlayModes = new();

        [Tooltip("Enter 시 모드 미지정 시 fallback (예: Mob)")]
        public string defaultOverlayMode = "";

        [Tooltip("긍정 무드 표정 ID 목록 (예: BrightSmile, _11)")]
        public List<string> positiveEmotes = new();

        [Tooltip("부정 무드 표정 ID 목록 (예: Glare, _21)")]
        public List<string> negativeEmotes = new();

        /// <summary>오버레이 리소스 이름 합성: {prefix}_{mode}_{variant} (예: Roa_Mob_Negative)</summary>
        public string GetOverlayName(string emote, string mode = null)
        {
            if (string.IsNullOrEmpty(overlayPrefix)) return null;

            string variant = ResolveVariant(emote);
            string effectiveMode = !string.IsNullOrEmpty(mode) ? mode :
                                   !string.IsNullOrEmpty(defaultOverlayMode) ? defaultOverlayMode : null;

            return string.IsNullOrEmpty(effectiveMode)
                ? $"{overlayPrefix}_{variant}"
                : $"{overlayPrefix}_{effectiveMode}_{variant}";
        }

        public string ResolveVariant(string emote)
        {
            if (string.IsNullOrEmpty(emote)) return "Default";
            foreach (var e in positiveEmotes)
                if (e.Equals(emote, StringComparison.OrdinalIgnoreCase)) return "Positive";
            foreach (var e in negativeEmotes)
                if (e.Equals(emote, StringComparison.OrdinalIgnoreCase)) return "Negative";
            return "Default";
        }

        public bool IsValidMode(string mode)
        {
            if (overlayModes == null || overlayModes.Count == 0) return true;
            foreach (var m in overlayModes)
                if (m.Equals(mode, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
