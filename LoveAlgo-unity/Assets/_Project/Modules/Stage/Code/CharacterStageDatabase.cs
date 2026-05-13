using System;
using System.Collections.Generic;
using UnityEngine;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 캐릭터 시각 표현 DB (Stage 표현 — 트랜스폼 + VirtualOverlay).
    /// Stage 모듈 소유. 정체성(displayName 등)은 CharacterMetaDatabase 참조.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStageDatabase", menuName = "LoveAlgo/Character Stage Database")]
    public class CharacterStageDatabase : ScriptableObject
    {
        static CharacterStageDatabase _instance;

        public static CharacterStageDatabase Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<CharacterStageDatabase>("Data/CharacterStageDatabase");
                    if (_instance == null)
                        Debug.LogError("[CharacterStageDatabase] Resources/Data/CharacterStageDatabase.asset 을 찾을 수 없습니다");
                }
                return _instance;
            }
        }

        [Header("캐릭터 표현 목록")]
        public List<CharacterStageEntry> entries = new();

        public CharacterStageEntry GetById(string characterId) =>
            entries.Find(e => e.characterId.Equals(characterId, StringComparison.OrdinalIgnoreCase));
    }

    [Serializable]
    public class CharacterStageEntry
    {
        [Tooltip("캐릭터 ID (CharacterMetaDatabase와 동일)")]
        public string characterId = "";

        [Header("스프라이트 트랜스폼")]
        [Tooltip("스케일 배율 (1 = 기본)")]
        public float spriteScale = 1f;

        [Tooltip("X 오프셋")]
        public float offsetX = 0f;

        [Tooltip("Y 오프셋")]
        public float offsetY = 0f;

        [Tooltip("피벗 Y (0=하단, 0.5=중앙, 1=상단)")]
        [Range(0f, 1f)]
        public float pivotY = 0f;

        [Header("VirtualOverlay (가상 캐릭터 BG)")]
        [Tooltip("오버레이 prefix (예: Roa). 비어있으면 오버레이 미사용. 모드(Mob/PC)와 variant(Default/Positive/Negative)는 런타임에 합성")]
        public string overlayPrefix = "";

        [Tooltip("유효 모드 목록 (예: Mob, PC). 작가가 CSV에 명시하는 모드와 일치해야 함")]
        public List<string> overlayModes = new();

        [Tooltip("Enter 시 모드 미지정 시 fallback 모드 (예: Mob)")]
        public string defaultOverlayMode = "";

        [Tooltip("긍정 무드 표정 ID 목록 (예: BrightSmile, EyeSmile, Happy 또는 _11/_12/_14)")]
        public List<string> positiveEmotes = new();

        [Tooltip("부정 무드 표정 ID 목록 (예: Glare, Tearful 또는 _21/_31)")]
        public List<string> negativeEmotes = new();

        public bool UseOverlay => !string.IsNullOrEmpty(overlayPrefix);

        public void GetTransform(out float scale, out float oX, out float oY, out float pY)
        {
            scale = spriteScale;
            oX = offsetX;
            oY = offsetY;
            pY = pivotY;
        }

        /// <summary>
        /// 오버레이 리소스 이름 합성: `{overlayPrefix}_{mode}_{variant}` (예: Roa_Mob_Negative).
        /// overlayPrefix 비었으면 null. mode 비었으면 defaultOverlayMode fallback. 모두 비었으면 mode 생략 (구버전 호환: Roa_Default).
        /// </summary>
        public string GetOverlayName(string emote, string mode = null)
        {
            if (!UseOverlay) return null;

            string variant = ResolveVariant(emote);

            string effectiveMode = !string.IsNullOrEmpty(mode) ? mode :
                                   !string.IsNullOrEmpty(defaultOverlayMode) ? defaultOverlayMode : null;

            return string.IsNullOrEmpty(effectiveMode)
                ? $"{overlayPrefix}_{variant}"
                : $"{overlayPrefix}_{effectiveMode}_{variant}";
        }

        /// <summary>emote ID → variant 분류 (Positive/Negative/Default)</summary>
        public string ResolveVariant(string emote)
        {
            if (string.IsNullOrEmpty(emote)) return "Default";
            foreach (var e in positiveEmotes)
                if (e.Equals(emote, StringComparison.OrdinalIgnoreCase)) return "Positive";
            foreach (var e in negativeEmotes)
                if (e.Equals(emote, StringComparison.OrdinalIgnoreCase)) return "Negative";
            return "Default";
        }

        /// <summary>모드 유효성 검사 (overlayModes 화이트리스트 있을 때만).</summary>
        public bool IsValidMode(string mode)
        {
            if (overlayModes == null || overlayModes.Count == 0) return true;
            foreach (var m in overlayModes)
                if (m.Equals(mode, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
