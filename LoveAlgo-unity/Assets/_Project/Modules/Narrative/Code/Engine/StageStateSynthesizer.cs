using System;
using LoveAlgo.Contracts;
using System.Collections.Generic;
using UnityEngine;
using LoveAlgo.Story.SaveSystem;

namespace LoveAlgo.Story
{
    /// <summary>
    /// CSV 라인 0..targetIndex 범위를 순회하며 무대 변경 명령을 누적해서
    /// "그 시점의 최종 무대 상태"를 SaveData 형태로 합성.
    ///
    /// **사이드이펙트 자동 처리** (런타임 executor 모방):
    ///   - CG show → 모든 슬롯 비움 + CurrentOverlay=null (CGLineExecutor가 ExitAllAsync 호출하므로)
    ///   - BG Fade 전환 → 모든 슬롯 비움 + CurrentOverlay=null (BGLineExecutor가 ClearAll 호출)
    ///   - Char Enter overlay character → CurrentOverlay = computed name
    ///   - Char Emote/Mode overlay character → CurrentOverlay 재계산
    ///   - Char Exit overlay character (다른 슬롯에 없으면) → CurrentOverlay=null
    ///
    /// 디버그 점프(F4 편집기, F5 다음 날, F6 다음 선택지)에서 호출.
    /// 합성 후 StageRestorer.RestoreAsync()에 넘기면 즉시(Cut/0초) 무대 복원.
    ///
    /// verbose 모드 (PlayerPrefs "StageSync.Verbose"): 라인별 변경 추적 로그 활성화.
    /// </summary>
    public static class StageStateSynthesizer
    {
        const string Tag = "StageSync";

        public static SaveData Synthesize(IReadOnlyList<ScriptLine> lines, int upToInclusive)
        {
            var data = new SaveData();
            if (lines == null || lines.Count == 0) return data;

            int max = Mathf.Min(upToInclusive, lines.Count - 1);
            if (max < 0) return data;

            // Mark backward search — 가장 가까운 Mark 라인부터만 합성.
            int startFrom = FindStartAfterNearestMark(lines, max);

            StageSyncLog.Section(Tag, $"Synthesize scan [{startFrom}..{max}] ({max - startFrom + 1} lines)");

            // 합성 상태 ──
            // 슬롯별 (character id, emote). null이면 빈 슬롯.
            var slots = new (string ch, string em)[3]; // [L=0, C=1, R=2]
            // 캐릭터별 mode 추적 — Char Mode 명령 또는 Enter 시 모드 지정으로 갱신
            var charModes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = startFrom; i <= max; i++)
            {
                var line = lines[i];
                if (line == null || string.IsNullOrEmpty(line.Value)) continue;

                switch (line.Type)
                {
                    case LineType.BG:      ApplyBG(line.Value, data, slots, charModes, i);            break;
                    case LineType.Char:    ApplyChar(line.Value, slots, charModes, data, i);          break;
                    case LineType.CG:      ApplyCG(line.Value, data, slots, charModes, i);            break;
                    case LineType.SD:      ApplySD(line.Value, data, i);                              break;
                    case LineType.Overlay: ApplyOverlay(line.Value, data, i);                         break;
                    case LineType.Sound:   ApplySound(line.Value, data, i);                           break;
                    case LineType.FX:      ApplyFX(line.Value, data, slots, charModes, i);            break;
                    // 나머지 (Text/Choice/Option/Place/Flow non-Mark)는 상태 영향 없음
                }
            }

            // 슬롯 → Characters 리스트
            data.Characters.Clear();
            AppendSlot(data, "L", slots[0]);
            AppendSlot(data, "C", slots[1]);
            AppendSlot(data, "R", slots[2]);

            StageSyncLog.Info(Tag,
                $"Synthesized: BG={data.CurrentBG ?? "-"}, BGM={data.CurrentBGM ?? "-"}, " +
                $"L={Fmt(slots[0])}, C={Fmt(slots[1])}, R={Fmt(slots[2])}, " +
                $"CG={data.CurrentCG ?? "-"}, SD={data.CurrentSD ?? "-"}, Overlay={data.CurrentOverlay ?? "-"}");

            return data;
        }

        static void AppendSlot(SaveData data, string slot, (string ch, string em) state)
        {
            if (string.IsNullOrEmpty(state.ch)) return;
            data.Characters.Add(new CharacterSaveInfo
            {
                Slot = slot,
                Character = state.ch,
                Emote = string.IsNullOrEmpty(state.em) ? "Default" : state.em
            });
        }

        static string Fmt((string ch, string em) s)
            => string.IsNullOrEmpty(s.ch) ? "-" : $"{s.ch}/{s.em ?? "Default"}";

        // ══════════════════════════════════════════════
        //  BG
        // ══════════════════════════════════════════════
        static void ApplyBG(string value, SaveData data, (string ch, string em)[] slots,
                            Dictionary<string,string> charModes, int lineIdx)
        {
            // 형식: bgName[:Transition[:duration]]
            var parts = value.Split(':');
            string name = parts[0];
            string transition = parts.Length > 1 ? parts[1] : "CrossFade";
            string prev = data.CurrentBG;

            if (!string.IsNullOrEmpty(name)) data.CurrentBG = name;

            // Fade 전환 → 런타임에서 Character.ClearAll() + DialogueUI.Hide
            // 합성기는 슬롯/Overlay 비우기 모방
            bool isFade = transition.Equals("Fade", StringComparison.OrdinalIgnoreCase);
            if (isFade)
            {
                int cleared = ClearAllSlots(slots);
                charModes.Clear();
                string prevOverlay = data.CurrentOverlay;
                data.CurrentOverlay = null;
                StageSyncLog.Detail(Tag,
                    $"L{lineIdx} BG[Fade]: {prev ?? "-"} → {name}  (side: cleared {cleared} slots, overlay {prevOverlay ?? "-"} → null)");
            }
            else
            {
                StageSyncLog.Detail(Tag, $"L{lineIdx} BG[{transition}]: {prev ?? "-"} → {name}");
            }
        }

        // ══════════════════════════════════════════════
        //  Char
        // ══════════════════════════════════════════════
        static void ApplyChar(string rawValue, (string ch, string em)[] slots,
                              Dictionary<string,string> charModes, SaveData data, int lineIdx)
        {
            // 단축 문법 정규화
            string value = NormalizeCharShortForm(rawValue);
            var parts = value.Split(':');
            if (parts.Length < 2) return;

            int idx = SlotIndex(parts[0]);
            if (idx < 0) return;
            string slotName = new[] { "L", "C", "R" }[idx];
            string action = parts[1].ToLowerInvariant();
            var prev = slots[idx];

            switch (action)
            {
                case "enter":
                case "enterup":
                {
                    if (parts.Length < 3) return;
                    string character = ResolveCharId(parts[2]);
                    string emote = parts.Length >= 4 ? parts[3] : "Default";
                    string fifth = parts.Length >= 5 ? parts[4] : null;

                    slots[idx] = (character, emote);

                    // Mode 추출 — 5번째가 Mode면 등록
                    if (!string.IsNullOrEmpty(fifth) && IsOverlayCharacter(character))
                    {
                        var entry = StoryMappings.GetOverlay(character);
                        if (entry != null && entry.IsValidMode(fifth))
                            charModes[character] = fifth;
                    }

                    // Overlay 자동 표시 (runtime: ShowOverlayAsync)
                    UpdateOverlayAfterCharChange(data, slots, charModes, lineIdx,
                        $"Char {slotName}:Enter: {Fmt(prev)} → {character}/{emote}");
                    return;
                }

                case "emote":
                {
                    if (parts.Length < 3) return;
                    string emote = parts[2];
                    string fourth = parts.Length >= 4 ? parts[3] : null;

                    if (string.IsNullOrEmpty(slots[idx].ch))
                    {
                        StageSyncLog.Detail(Tag, $"L{lineIdx} Char {slotName}:Emote 무시 — 슬롯 비어있음");
                        return;
                    }
                    slots[idx] = (slots[idx].ch, emote);

                    // 4번째가 Mode면 갱신
                    if (!string.IsNullOrEmpty(fourth) && IsOverlayCharacter(slots[idx].ch))
                    {
                        var entry = StoryMappings.GetOverlay(slots[idx].ch);
                        if (entry != null && entry.IsValidMode(fourth))
                            charModes[slots[idx].ch] = fourth;
                    }

                    UpdateOverlayAfterCharChange(data, slots, charModes, lineIdx,
                        $"Char {slotName}:Emote: {Fmt(prev)} → {slots[idx].ch}/{emote}");
                    return;
                }

                case "mode":
                {
                    if (parts.Length < 3) return;
                    string newMode = parts[2];
                    if (string.IsNullOrEmpty(slots[idx].ch)) return;
                    string charId = slots[idx].ch;
                    if (IsOverlayCharacter(charId))
                    {
                        var entry = StoryMappings.GetOverlay(charId);
                        if (entry != null && entry.IsValidMode(newMode))
                        {
                            charModes[charId] = newMode;
                            UpdateOverlayAfterCharChange(data, slots, charModes, lineIdx,
                                $"Char {slotName}:Mode: {charId} mode → {newMode}");
                        }
                    }
                    return;
                }

                case "exit":
                case "exitdown":
                {
                    slots[idx] = (null, null);
                    UpdateOverlayAfterCharChange(data, slots, charModes, lineIdx,
                        $"Char {slotName}:Exit: {Fmt(prev)} → empty");
                    return;
                }
            }
        }

        /// <summary>슬롯/모드 변경 후 Overlay 재계산 — 다른 슬롯에 overlay 캐릭터 남아있는지 확인.</summary>
        static void UpdateOverlayAfterCharChange(SaveData data, (string ch, string em)[] slots,
                                                  Dictionary<string,string> charModes, int lineIdx,
                                                  string actionDesc)
        {
            string prev = data.CurrentOverlay;
            string newOverlay = ComputeOverlayFromSlots(slots, charModes);
            data.CurrentOverlay = newOverlay;

            if (prev != newOverlay)
                StageSyncLog.Detail(Tag, $"L{lineIdx} {actionDesc}  (overlay {prev ?? "-"} → {newOverlay ?? "-"})");
            else
                StageSyncLog.Detail(Tag, $"L{lineIdx} {actionDesc}");
        }

        /// <summary>현재 슬롯들 중 overlay 캐릭터의 overlay 이름 계산. 없으면 null.</summary>
        static string ComputeOverlayFromSlots((string ch, string em)[] slots, Dictionary<string,string> charModes)
        {
            foreach (var s in slots)
            {
                if (string.IsNullOrEmpty(s.ch)) continue;
                if (!IsOverlayCharacter(s.ch)) continue;
                var entry = StoryMappings.GetOverlay(s.ch);
                if (entry == null) continue;
                string mode = charModes.TryGetValue(s.ch, out var m) ? m : entry.DefaultMode;
                return entry.GetOverlayName(s.em ?? "Default", mode);
            }
            return null;
        }

        static bool IsOverlayCharacter(string charId)
        {
            if (string.IsNullOrEmpty(charId)) return false;
            return StoryMappings.GetOverlay(charId) != null;
        }

        /// <summary>alias/displayName을 표준 c0X로 정규화 (예: "Roa" → "c01").</summary>
        static string ResolveCharId(string nameOrId)
        {
            if (string.IsNullOrEmpty(nameOrId)) return nameOrId;
            return StoryMappings.SpeakerToCharacterId(nameOrId) ?? nameOrId;
        }

        static string NormalizeCharShortForm(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return raw;
            int colon = raw.IndexOf(':');
            string first = colon >= 0 ? raw.Substring(0, colon) : raw;
            if (SlotIndex(first) >= 0) return raw;
            string lower = first.ToLowerInvariant();
            if (lower == "enter" || lower == "enterup" || lower == "emote"
                || lower == "exit" || lower == "exitdown" || lower == "mode")
                return "C:" + raw;
            return raw;
        }

        static int SlotIndex(string token)
        {
            if (string.IsNullOrEmpty(token)) return -1;
            switch (token.ToUpperInvariant())
            {
                case "L": case "LEFT":   return 0;
                case "C": case "CENTER": return 1;
                case "R": case "RIGHT":  return 2;
                default: return -1;
            }
        }

        static int ClearAllSlots((string ch, string em)[] slots)
        {
            int n = 0;
            for (int i = 0; i < slots.Length; i++)
            {
                if (!string.IsNullOrEmpty(slots[i].ch)) n++;
                slots[i] = (null, null);
            }
            return n;
        }

        // ══════════════════════════════════════════════
        //  CG
        // ══════════════════════════════════════════════
        static void ApplyCG(string value, SaveData data, (string ch, string em)[] slots,
                            Dictionary<string,string> charModes, int lineIdx)
        {
            var parts = value.Split(':');
            string first = parts[0];

            if (IsHideKeyword(first))
            {
                string prev = data.CurrentCG;
                data.CurrentCG = null;
                StageSyncLog.Detail(Tag, $"L{lineIdx} CG:Exit: {prev ?? "-"} → null");
                return;
            }

            // CG show → 런타임은 ExitAllAsync + Overlay 자동 숨김
            string prevCG = data.CurrentCG;
            data.CurrentCG = first;

            int cleared = ClearAllSlots(slots);
            charModes.Clear();
            string prevOverlay = data.CurrentOverlay;
            data.CurrentOverlay = null;

            StageSyncLog.Detail(Tag,
                $"L{lineIdx} CG:Show: {prevCG ?? "-"} → {first}  (side: cleared {cleared} slots, overlay {prevOverlay ?? "-"} → null)");
        }

        // ══════════════════════════════════════════════
        //  SD
        // ══════════════════════════════════════════════
        static void ApplySD(string value, SaveData data, int lineIdx)
        {
            // SD show/exit은 캐릭터를 visibility만 바꾸고 상태는 보존 → 합성기 상태 무영향
            var parts = value.Split(':');
            string first = parts[0];
            if (IsHideKeyword(first))
            {
                string prev = data.CurrentSD;
                data.CurrentSD = null;
                StageSyncLog.Detail(Tag, $"L{lineIdx} SD:Exit: {prev ?? "-"} → null");
                return;
            }
            string prevSD = data.CurrentSD;
            data.CurrentSD = first;
            StageSyncLog.Detail(Tag, $"L{lineIdx} SD:Show: {prevSD ?? "-"} → {first}");
        }

        // ══════════════════════════════════════════════
        //  Overlay (단독 — Char 사이드이펙트와는 별개 경로)
        // ══════════════════════════════════════════════
        static void ApplyOverlay(string value, SaveData data, int lineIdx)
        {
            var parts = value.Split(':');
            string first = parts[0];

            if (first.Equals("FadeOut", StringComparison.OrdinalIgnoreCase))
            {
                string prev = data.CurrentOverlay;
                data.CurrentOverlay = null;
                StageSyncLog.Detail(Tag, $"L{lineIdx} Overlay:FadeOut: {prev ?? "-"} → null");
                return;
            }
            if (parts.Length >= 2 && parts[1].Equals("FadeOut", StringComparison.OrdinalIgnoreCase))
            {
                string prev = data.CurrentOverlay;
                data.CurrentOverlay = null;
                StageSyncLog.Detail(Tag, $"L{lineIdx} Overlay:FadeOut(name): {prev ?? "-"} → null");
                return;
            }

            string prevOverlay = data.CurrentOverlay;
            data.CurrentOverlay = first;
            StageSyncLog.Detail(Tag, $"L{lineIdx} Overlay:Show: {prevOverlay ?? "-"} → {first}");
        }

        // ══════════════════════════════════════════════
        //  Sound (BGM만)
        // ══════════════════════════════════════════════
        static void ApplySound(string value, SaveData data, int lineIdx)
        {
            var parts = value.Split(':');
            if (parts.Length < 2) return;
            string category = parts[0];
            if (!category.Equals("BGM", StringComparison.OrdinalIgnoreCase)) return;

            string name = parts[1];
            if (name.Equals("Stop", StringComparison.OrdinalIgnoreCase))
            {
                string prev = data.CurrentBGM;
                data.CurrentBGM = "";
                StageSyncLog.Detail(Tag, $"L{lineIdx} BGM:Stop: {prev ?? "-"} → -");
                return;
            }

            if (StoryMappings.TryResolveBgm(name, out var resolved)) name = resolved;
            string prevBgm = data.CurrentBGM;
            data.CurrentBGM = name;
            StageSyncLog.Detail(Tag, $"L{lineIdx} BGM:Play: {prevBgm ?? "-"} → {name}");
        }

        static bool IsHideKeyword(string s)
        {
            if (string.IsNullOrEmpty(s)) return false;
            return s.Equals("Exit", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Close", StringComparison.OrdinalIgnoreCase)
                || s.Equals("Hide", StringComparison.OrdinalIgnoreCase);
        }

        // ══════════════════════════════════════════════
        //  FX (매크로 — Setup/SceneStart/SceneEnd/DayStart/DayEnd/NextDay)
        //  매크로가 stage 상태를 바꾸는 경우 합성기가 정확히 추적해야 점프 시 화면 깨짐 없음.
        // ══════════════════════════════════════════════
        static void ApplyFX(string value, SaveData data, (string ch, string em)[] slots,
                            Dictionary<string,string> charModes, int lineIdx)
        {
            if (string.IsNullOrEmpty(value)) return;
            int colonIdx = value.IndexOf(':');
            string cmd  = (colonIdx < 0 ? value : value.Substring(0, colonIdx)).Trim();
            string args = colonIdx < 0 ? "" : value.Substring(colonIdx + 1);

            switch (cmd.ToLowerInvariant())
            {
                case "setup":      ApplyFXSetup(args, data, slots, charModes, lineIdx); break;
                case "scenestart": ApplyFXSceneStart(args, data, slots, charModes, lineIdx); break;
                case "sceneend":   ApplyFXSceneEnd(data, slots, charModes, lineIdx); break;
                case "daystart":   ApplyFXDayStart(args, data, slots, charModes, lineIdx); break;
                case "dayend":     ApplyFXDayEnd(data, slots, charModes, lineIdx); break;
                case "nextday":    ApplyFXNextDay(args, data, slots, charModes, lineIdx); break;
                // 나머지 FX (FadeIn/Out, Shake 등)는 stage 상태 영향 없음 → 무시
            }
        }

        /// <summary>Setup:BG=…|BGM=…|Char=…[:슬롯]|Overlay=… — 다중 상태 즉시 세팅</summary>
        static void ApplyFXSetup(string args, SaveData data, (string ch, string em)[] slots,
                                 Dictionary<string,string> charModes, int lineIdx)
        {
            if (string.IsNullOrEmpty(args)) return;
            var entries = args.Split('|');
            var changes = new List<string>();

            foreach (var entry in entries)
            {
                var kv = entry.Split('=');
                if (kv.Length < 2) continue;
                string key = kv[0].Trim();
                string val = kv[1].Trim();
                if (string.IsNullOrEmpty(val)) continue;

                switch (key.ToLowerInvariant())
                {
                    case "bg":
                    {
                        // 한글/별칭 → 표준 ID
                        if (StoryMappings.TryResolveBg(val, out var resolved)) val = resolved;
                        string prev = data.CurrentBG;
                        data.CurrentBG = val;
                        changes.Add($"BG {prev ?? "-"} → {val}");
                        break;
                    }
                    case "bgm":
                    {
                        if (StoryMappings.TryResolveBgm(val, out var resolved)) val = resolved;
                        string prev = data.CurrentBGM;
                        data.CurrentBGM = val;
                        changes.Add($"BGM {prev ?? "-"} → {val}");
                        break;
                    }
                    case "char":
                    {
                        // val 형식: name[:slot] (예: "로아:C" 또는 "로아")
                        var cp = val.Split(':');
                        string charId = ResolveCharId(cp[0]);
                        int idx = cp.Length >= 2 ? SlotIndex(cp[1]) : 1; // 기본 C
                        if (idx < 0) idx = 1;
                        var prev = slots[idx];
                        slots[idx] = (charId, "Default");
                        changes.Add($"Char [{new[] { "L", "C", "R" }[idx]}] {Fmt(prev)} → {charId}/Default");
                        break;
                    }
                    case "overlay":
                    {
                        string prev = data.CurrentOverlay;
                        data.CurrentOverlay = val;
                        changes.Add($"Overlay {prev ?? "-"} → {val}");
                        break;
                    }
                    case "eye":
                    {
                        // PR-Scene-3에서 추가 — Eye=Close|Open
                        bool close = val.Equals("Close", StringComparison.OrdinalIgnoreCase);
                        bool open  = val.Equals("Open",  StringComparison.OrdinalIgnoreCase);
                        if (close) { data.IsEyeClosed = true;  changes.Add("Eye → Close"); }
                        else if (open) { data.IsEyeClosed = false; changes.Add("Eye → Open"); }
                        break;
                    }
                    default:
                        changes.Add($"<unknown {key}={val}>");
                        break;
                }
            }

            // Setup 후 Overlay 재계산 (Char 항목으로 인해 자동 overlay가 필요할 수 있음)
            string autoOverlay = ComputeOverlayFromSlots(slots, charModes);
            if (!string.IsNullOrEmpty(autoOverlay) && string.IsNullOrEmpty(data.CurrentOverlay))
                data.CurrentOverlay = autoOverlay;

            StageSyncLog.Detail(Tag, $"L{lineIdx} FX:Setup [{string.Join(", ", changes)}]");
        }

        /// <summary>SceneStart[:BG[:EyeClose]] — Character ClearAll + BG 교체 + EyeClose</summary>
        static void ApplyFXSceneStart(string args, SaveData data, (string ch, string em)[] slots,
                                      Dictionary<string,string> charModes, int lineIdx)
        {
            var parts = string.IsNullOrEmpty(args) ? new string[0] : args.Split(':');
            string bg = parts.Length > 0 ? parts[0] : null;
            bool eyeClose = parts.Length > 1 && parts[1].Equals("EyeClose", StringComparison.OrdinalIgnoreCase);

            int cleared = ClearAllSlots(slots);
            charModes.Clear();
            data.CurrentOverlay = null;

            string prevBG = data.CurrentBG;
            if (!string.IsNullOrEmpty(bg))
            {
                if (StoryMappings.TryResolveBg(bg, out var resolved)) bg = resolved;
                data.CurrentBG = bg;
            }
            data.IsEyeClosed = eyeClose;
            data.IsFadeBlack = false;

            StageSyncLog.Detail(Tag,
                $"L{lineIdx} FX:SceneStart  (BG {prevBG ?? "-"} → {data.CurrentBG ?? "-"}, " +
                $"cleared {cleared} slots, eye={(eyeClose ? "Close" : "Open")})");
        }

        /// <summary>SceneEnd — Character/Overlay/BGM 정리 + 검정 페이드</summary>
        static void ApplyFXSceneEnd(SaveData data, (string ch, string em)[] slots,
                                    Dictionary<string,string> charModes, int lineIdx)
        {
            int cleared = ClearAllSlots(slots);
            charModes.Clear();
            data.CurrentOverlay = null;
            data.CurrentBGM = "";
            data.IsFadeBlack = true;
            StageSyncLog.Detail(Tag, $"L{lineIdx} FX:SceneEnd  (cleared {cleared} slots, BGM stop, fade black)");
        }

        /// <summary>DayStart[:BG[:Wake|Cut][:actions]] — BG 교체, mode에 따라 eye/fade</summary>
        static void ApplyFXDayStart(string args, SaveData data, (string ch, string em)[] slots,
                                    Dictionary<string,string> charModes, int lineIdx)
        {
            // 인자 순서 자유 — 숫자=actions, Wake/Cut/Reveal=mode, 그 외=BG
            string bg = null;
            string mode = "Wake";
            if (!string.IsNullOrEmpty(args))
            {
                foreach (var token in args.Split(':'))
                {
                    if (string.IsNullOrEmpty(token)) continue;
                    if (int.TryParse(token, out _)) continue; // actions — 상태 영향 없음
                    if (token.Equals("Wake",   StringComparison.OrdinalIgnoreCase) ||
                        token.Equals("Cut",    StringComparison.OrdinalIgnoreCase) ||
                        token.Equals("Reveal", StringComparison.OrdinalIgnoreCase))
                        mode = token;
                    else if (bg == null)
                        bg = token;
                }
            }

            string prevBG = data.CurrentBG;
            if (!string.IsNullOrEmpty(bg))
            {
                if (StoryMappings.TryResolveBg(bg, out var resolved)) bg = resolved;
                data.CurrentBG = bg;
            }

            if (mode.Equals("Cut", StringComparison.OrdinalIgnoreCase) ||
                mode.Equals("Reveal", StringComparison.OrdinalIgnoreCase))
            {
                data.IsEyeClosed = false;
                data.IsFadeBlack = false;
            }
            else
            {
                // Wake: BG 있으면 EyeClose 유지, 없으면 EyeOpen
                if (!string.IsNullOrEmpty(bg)) data.IsEyeClosed = true;
                else data.IsEyeClosed = false;
            }

            StageSyncLog.Detail(Tag,
                $"L{lineIdx} FX:DayStart[{mode}]  (BG {prevBG ?? "-"} → {data.CurrentBG ?? "-"}, " +
                $"eye={(data.IsEyeClosed ? "Close" : "Open")})");
        }

        /// <summary>DayEnd[:duration[:Wake|Cut]] — Character/Overlay 정리, BG=Black, mode별 eye</summary>
        static void ApplyFXDayEnd(SaveData data, (string ch, string em)[] slots,
                                  Dictionary<string,string> charModes, int lineIdx)
        {
            int cleared = ClearAllSlots(slots);
            charModes.Clear();
            data.CurrentOverlay = null;
            data.CurrentBGM = "";
            data.CurrentBG = "BG_BlackCut";
            // Wake/Cut 모두 화면은 검정. 차이는 EyeClose 여부지만 점프 시 IsFadeBlack=true가 안전.
            data.IsFadeBlack = true;
            data.IsEyeClosed = false; // mode 모호 — 다음 라인이 EyeClose라면 별도 처리
            StageSyncLog.Detail(Tag,
                $"L{lineIdx} FX:DayEnd  (cleared {cleared} slots, BG=Black, BGM stop, fade black)");
        }

        /// <summary>NextDay = DayEnd + DayStart 합성 한 줄</summary>
        static void ApplyFXNextDay(string args, SaveData data, (string ch, string em)[] slots,
                                   Dictionary<string,string> charModes, int lineIdx)
        {
            ApplyFXDayEnd(data, slots, charModes, lineIdx);
            ApplyFXDayStart(args, data, slots, charModes, lineIdx);
        }

        // ══════════════════════════════════════════════
        //  Mark 역방향 탐색
        // ══════════════════════════════════════════════
        static int FindStartAfterNearestMark(IReadOnlyList<ScriptLine> lines, int upToInclusive)
        {
            int markIdx = MarkRegistry.FindNearestMarkAtOrBefore(upToInclusive);
            if (markIdx >= 0)
            {
                StageSyncLog.Section(Tag, $"start point = Mark at line {markIdx + 1}");
                return markIdx + 1;
            }
            // Fallback — 인라인 역방향 스캔
            for (int i = upToInclusive; i >= 0; i--)
            {
                var line = lines[i];
                if (line == null || line.Type != LineType.Flow) continue;
                if (!string.IsNullOrEmpty(line.Value)
                    && line.Value.StartsWith("Mark", StringComparison.OrdinalIgnoreCase))
                {
                    StageSyncLog.Section(Tag, $"start point = inline Mark at line {i + 1}");
                    return i + 1;
                }
            }
            StageSyncLog.Section(Tag, "start point = 0 (no Mark found)");
            return 0;
        }
    }
}
