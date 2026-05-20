using System;
using System.Collections.Generic;
using System.Linq;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 스토리 매핑·캐릭터·오버레이 단일 정전.
    /// 기획용 한글 키 → 엔진 ID, 캐릭터 메타, 가상 오버레이 데이터를 한 파일에서 관리.
    /// 신규 항목 추가 시 이 파일만 수정하면 됨 (xlsx/SO 워크플로 폐기).
    /// </summary>
    public static class StoryMappings
    {
        static readonly StringComparer KO = StringComparer.OrdinalIgnoreCase;

        // ─── BG (배경) ────────────────────────────────────────────
        public static readonly Dictionary<string, string> BG = new(KO)
        {
            { "검은 화면",                "bg_00_00" },
            { "자취방 전경 낮",           "bg_10_01" },
            { "자취방 전경 밤",           "bg_10_02" },
            { "자취방 전경 밤 불켜기",    "bg_10_03" },
            { "자취방 침대위 아침",       "bg_10_04" },
            { "자취방 침대위 밤",         "bg_10_05" },
            { "자취방 책상",              "bg_10_06" },
            { "공대 앞 낮",               "bg_20_01" },
            { "공대 앞 밤",               "bg_20_02" },
            { "공대 강의실복도",          "bg_20_03" },
            { "공대 학생복지실",          "bg_20_04" },
            { "공대 강의실 낮",           "bg_20_05" },
            { "공대 강의실 낮 벚꽃",      "bg_20_06" },
            { "캠퍼스거리1 맑음",         "bg_30_01" },
            { "캠퍼스거리2 맑음",         "bg_30_02" },
            { "학생회관_앞_낮",           "bg_40_01" },
            { "학생회관_앞_밤",           "bg_40_02" },
            { "학생회관_행정실",          "bg_40_03" },
            { "학생회관_복도",            "bg_40_04" },
            { "학생회관_게시판",          "bg_40_05" },
            { "학생회관_동방_낮_나무",    "bg_40_06" },
            { "학생회관_동방_낮_벚꽃",    "bg_40_07" },
            { "편의점 앞 낮",             "bg_60_01" },
            { "편의점 앞 밤",             "bg_60_02" },
        };

        // ─── CG ───────────────────────────────────────────────────
        // 값은 Resources/CG/{value}.png 실제 파일명과 일치. 형식: cg_{cNN}_{NN}
        public static readonly Dictionary<string, string> CG = new(KO)
        {
            { "로아 첫만남",              "cg_c01_01" },
            { "예은 입부신청서 작성",     "cg_c03_01" },
        };

        // ─── SD ───────────────────────────────────────────────────
        // 값은 Resources/SD/{value}.png 실제 파일명과 일치. 형식: sd_{cNN}_{NN}
        public static readonly Dictionary<string, string> SD = new(KO)
        {
            { "다은 첫만남",              "sd_c02_01" },
            { "희원 첫만남",              "sd_c04_01" },
            { "봄 첫만남",                "sd_c05_01" },
        };

        // ─── BGM / SFX ────────────────────────────────────────────
        // 값은 Resources/Audio/BGM/{value}.mp3 실제 파일명과 일치해야 함.
        public static readonly Dictionary<string, string> BGM = new(KO)
        {
            { "백색소음1",   "white_noise" },
            { "일상2",       "Daily2" },
            { "SeoDaEun",    "Daeun" },
            { "HaYeEun",     "Yeun" },
            { "DoHeewon",    "Heewon" },
            { "LeeBom",      "Bom" },
        };
        public static readonly Dictionary<string, string> SFX = new(KO) { };

        // ─── Emote (표정) ─────────────────────────────────────────
        public static readonly Dictionary<string, string> Emote = new(KO)
        {
            { "기본",            "_00" },
            { "눈웃음",          "_11" },
            { "밝게웃음",        "_12" },
            { "활짝",            "_13" },
            { "행복",            "_14" },
            { "찌릿",            "_21" },
            { "쌔짐",            "_22" },
            { "머쓱",            "_23" },
            { "어질어질",        "_24" },
            { "울먹",            "_31" },
            { "눈물 주르륵",     "_32" },
            { "와아앙 울기",     "_33" },
            { "부끄러워",        "_34" },
            { "피곤/졸려",       "_35" },
            { "깜짝",            "_41" },
            { "반짝빈짝",        "_42" },
            { "궁금",            "_43" },
            { "윙크",            "_44" },
            { "자신만만",        "_45" },

            // 영문 별칭 — Prologue.csv 등 코드 전역에서 사용
            { "Default",         "_00" },
            { "EyeSmile",        "_11" },
            { "BrightSmile",     "_12" },
            { "Happy",           "_14" },
            { "Glare",           "_21" },
            { "Tearful",         "_31" },
            { "Surprise",        "_41" },
        };

        // ─── Character ────────────────────────────────────────────
        public sealed class Character
        {
            public string Id;
            public string DisplayName;
            public string[] Aliases;
        }

        // Aliases는 코드 전역(GameConstants.HeroineConfig 등)에서 쓰이는 영문 ID와 일치.
        public static readonly Character[] Characters =
        {
            new() { Id = "c01", DisplayName = "로아",    Aliases = new[] { "Roa" } },
            new() { Id = "c02", DisplayName = "서다은",  Aliases = new[] { "SeoDaEun" } },
            new() { Id = "c03", DisplayName = "하예은",  Aliases = new[] { "HaYeEun" } },
            new() { Id = "c04", DisplayName = "도희원",  Aliases = new[] { "DoHeewon" } },
            new() { Id = "c05", DisplayName = "이봄",    Aliases = new[] { "LeeBom" } },
        };

        // ─── Overlay (가상 캐릭터 — VirtualBGOverlay 1:1) ─────────
        public sealed class Overlay
        {
            public string CharacterId;
            public string Prefix;
            public string[] Modes;
            public string DefaultMode;
            public string[] PositiveEmotes;
            public string[] NegativeEmotes;

            /// <summary>오버레이 리소스 이름 합성: {Prefix}_{Mode}_{Variant} — 모두 소문자로 정규화.</summary>
            public string GetOverlayName(string emote, string mode = null)
            {
                if (string.IsNullOrEmpty(Prefix)) return null;
                string variant = ResolveVariant(emote);
                string effective = !string.IsNullOrEmpty(mode) ? mode.ToLowerInvariant()
                                 : !string.IsNullOrEmpty(DefaultMode) ? DefaultMode.ToLowerInvariant() : null;
                return string.IsNullOrEmpty(effective)
                    ? $"{Prefix}_{variant}"
                    : $"{Prefix}_{effective}_{variant}";
            }

            public string ResolveVariant(string emote)
            {
                if (string.IsNullOrEmpty(emote)) return "default";
                if (PositiveEmotes != null)
                    foreach (var e in PositiveEmotes)
                        if (!string.IsNullOrEmpty(e) && e.Equals(emote, StringComparison.OrdinalIgnoreCase)) return "positive";
                if (NegativeEmotes != null)
                    foreach (var e in NegativeEmotes)
                        if (!string.IsNullOrEmpty(e) && e.Equals(emote, StringComparison.OrdinalIgnoreCase)) return "negative";
                return "default";
            }

            public bool IsValidMode(string mode)
            {
                if (Modes == null || Modes.Length == 0) return true;
                foreach (var m in Modes)
                    if (m.Equals(mode, StringComparison.OrdinalIgnoreCase)) return true;
                return false;
            }
        }

        public static readonly Overlay[] Overlays =
        {
            new()
            {
                CharacterId    = "c01",
                Prefix         = "overlay_c01",
                Modes          = new[] { "mob", "pc" },
                DefaultMode    = "pc",
                // 표정별 Overlay Variant 매핑 (한글 EmoteMap 키 + 영문 별칭 + 내부 ID 모두 커버)
                PositiveEmotes = new[]
                {
                    "눈웃음", "밝게웃음", "활짝", "행복", "반짝빠짝", "윙크", "자신만만",
                    "EyeSmile", "BrightSmile", "Happy",
                    "_11", "_12", "_13", "_14", "_42", "_44", "_45",
                },
                NegativeEmotes = new[]
                {
                    "찌릿", "쌔짐", "어질어질", "울먹", "눈물 주르륵", "와아앙 울기",
                    "Glare", "Tearful",
                    "_21", "_22", "_24", "_31", "_32", "_33",
                },
                // 위 두 배열에 없는 표정(기본, 머쓱, 부끄러워, 깜짝 등)은 Default로 폴백
            },
        };

        // ─── Lookup API ───────────────────────────────────────────
        public static string ResolveEmote(string ko) =>
            !string.IsNullOrEmpty(ko) && Emote.TryGetValue(ko, out var v) ? v : ko;

        public static bool TryResolveBg(string ko, out string id)  => BG.TryGetValue(ko ?? "", out id);
        public static bool TryResolveCg(string ko, out string id)  => CG.TryGetValue(ko ?? "", out id);
        public static bool TryResolveSd(string ko, out string id)  => SD.TryGetValue(ko ?? "", out id);
        public static bool TryResolveBgm(string ko, out string id) => BGM.TryGetValue(ko ?? "", out id);
        public static bool TryResolveSfx(string ko, out string id) => SFX.TryGetValue(ko ?? "", out id);

        public static Character GetCharacterById(string id) =>
            Characters.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));

        public static Character GetCharacterByDisplayName(string name) =>
            Characters.FirstOrDefault(c => string.Equals(c.DisplayName, name, StringComparison.OrdinalIgnoreCase));

        /// <summary>Speaker(displayName 또는 alias) → characterId. 미스 시 null.</summary>
        public static string SpeakerToCharacterId(string speaker)
        {
            if (string.IsNullOrEmpty(speaker)) return null;
            foreach (var c in Characters)
            {
                if (string.Equals(c.DisplayName, speaker, StringComparison.OrdinalIgnoreCase))
                    return c.Id;
                if (c.Aliases != null)
                    foreach (var a in c.Aliases)
                        if (string.Equals(a, speaker, StringComparison.OrdinalIgnoreCase))
                            return c.Id;
            }
            return null;
        }

        public static string CharacterIdToDisplayName(string id) =>
            GetCharacterById(id)?.DisplayName ?? id;

        public static Overlay GetOverlay(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;
            // alias/displayName으로 들어와도 c01로 정규화 후 매칭
            var resolved = SpeakerToCharacterId(characterId) ?? characterId;
            return Overlays.FirstOrDefault(o => string.Equals(o.CharacterId, resolved, StringComparison.OrdinalIgnoreCase));
        }
    }
}
