#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using LoveAlgo.NarrativeEditor.Mappings;
using LoveAlgo.Story;
using UnityEditor;
using UnityEngine;

namespace LoveAlgo.NarrativeEditor
{
    public class StoryConvertResult
    {
        public List<string[]> Rows = new();          // 변환된 5컬럼 라인들 (헤더/주석 포함)
        public int NewLines, ChangedLines, RemovedLines, OrphanPatches;
        public List<string> MissingEmote = new();
        public List<string> MissingBg = new();
        public List<string> MissingCg = new();
        public List<string> MissingSd = new();
        public List<string> MissingCharacter = new();
        public List<string> Warnings = new();
    }

    public class StoryConvertOptions
    {
        public string SourceCsvPath;
        public string TargetCsvPath;
        public string PatchCsvPath;
        public string LineIdPrefix = "pro_";
        public bool AssignLineIdsInPlace = true;
        public EmoteMap Emote;
        public CharacterMap Character;
        public BgMap Bg;
        public CgMap Cg;
        public SdMap Sd;
        public SoundMap Sound;
    }

    public static class StoryCsvConverter
    {
        [MenuItem("Tools/LoveAlgo/Story/Convert 기획 CSV (Default Paths)")]
        public static void ConvertWithDefaults()
        {
            const string MAP = "Assets/_Project/Modules/Narrative/Editor/Mappings";
            var opt = new StoryConvertOptions
            {
                SourceCsvPath = "Assets/_Project/Modules/Narrative/Art/Story/프롤로그(기획).csv",
                TargetCsvPath = "Assets/Resources/Story/Prologue.csv",
                PatchCsvPath  = "Assets/Resources/Story/Prologue.patch.csv",
                AssignLineIdsInPlace = true,
                Emote     = AssetDatabase.LoadAssetAtPath<EmoteMap>($"{MAP}/EmoteMap.asset"),
                Character = AssetDatabase.LoadAssetAtPath<CharacterMap>($"{MAP}/CharacterMap.asset"),
                Bg        = AssetDatabase.LoadAssetAtPath<BgMap>($"{MAP}/BgMap.asset"),
                Cg        = AssetDatabase.LoadAssetAtPath<CgMap>($"{MAP}/CgMap.asset"),
                Sd        = AssetDatabase.LoadAssetAtPath<SdMap>($"{MAP}/SdMap.asset"),
                Sound     = AssetDatabase.LoadAssetAtPath<SoundMap>($"{MAP}/SoundMap.asset"),
            };
            var r = Convert(opt);
            AssetDatabase.Refresh();
            Debug.Log($"[StoryConvert] {r.Rows.Count} rows | missing emote={r.MissingEmote.Count}, bg={r.MissingBg.Count}, cg={r.MissingCg.Count}, sd={r.MissingSd.Count}, orphan patch={r.OrphanPatches}");
            foreach (var s in r.MissingEmote) Debug.LogWarning($"missing emote: {s}");
            foreach (var s in r.MissingBg) Debug.LogWarning($"missing bg: {s}");
            foreach (var s in r.MissingCg) Debug.LogWarning($"missing cg: {s}");
            foreach (var s in r.MissingSd) Debug.LogWarning($"missing sd: {s}");
            foreach (var s in r.Warnings) Debug.LogWarning($"warn: {s}");
        }


        // Regex 캐시
        static readonly Regex EmoteRx = new(@"<emote=([^/>]+)/>", RegexOptions.Compiled);

        public static StoryConvertResult Convert(StoryConvertOptions opt)
        {
            var result = new StoryConvertResult();
            if (!File.Exists(opt.SourceCsvPath))
            {
                result.Warnings.Add($"Source CSV 없음: {opt.SourceCsvPath}");
                return result;
            }

            // 1) 읽기
            string sourceText = File.ReadAllText(opt.SourceCsvPath);
            var records = CsvUtility.SplitRecords(sourceText);
            Debug.Log($"[StoryCsvConverter] 입력: {opt.SourceCsvPath} ({records.Count} records)");

            // 2) LineID 자동 발급 (필요 시 in-place 저장)
            var sourceRows = new List<string[]>();
            int nextLineId = 1;
            bool sourceModified = false;
            foreach (var rec in records)
            {
                var cols = CsvUtility.SplitCsv(rec.Text);
                sourceRows.Add(cols);
                if (cols.Length > 0)
                {
                    var lineId = cols[0].Trim();
                    if (lineId.StartsWith(opt.LineIdPrefix))
                    {
                        var rest = lineId.Substring(opt.LineIdPrefix.Length);
                        if (int.TryParse(rest, out int n) && n >= nextLineId) nextLineId = n + 1;
                    }
                }
            }

            for (int i = 0; i < sourceRows.Count; i++)
            {
                var cols = sourceRows[i];
                if (cols.Length < 5) continue;
                string lineId = cols[0].Trim();
                string typeStr = cols[1].Trim();
                string value = cols[3].Trim();
                bool isHeader = i == 0 && (cols[0].Equals("LineID", StringComparison.OrdinalIgnoreCase));
                bool isComment = cols[0].TrimStart().StartsWith("#");
                bool isBlank = string.IsNullOrWhiteSpace(string.Join("", cols));
                if (isHeader || isComment || isBlank) continue;
                if (string.IsNullOrEmpty(typeStr)) continue; // 메모 행 (Type 빈 칸)
                if (!string.IsNullOrEmpty(lineId)) continue; // 이미 LineID 있음

                cols[0] = $"{opt.LineIdPrefix}{nextLineId:D3}";
                nextLineId++;
                sourceModified = true;
            }

            if (opt.AssignLineIdsInPlace && sourceModified)
            {
                WriteCsvInPlace(opt.SourceCsvPath, sourceRows);
                Debug.Log($"[StoryCsvConverter] LineID 자동 발급 → 원본 갱신: {opt.SourceCsvPath}");
            }

            // 3) 변환
            for (int i = 0; i < sourceRows.Count; i++)
            {
                var src = sourceRows[i];
                if (src.Length == 0) { result.Rows.Add(new[] { "" }); continue; }

                // 원본 컬럼 확보 (6 가정, 부족하면 빈칸 패딩)
                string lineId = Col(src, 0);
                string typeStr = Col(src, 1);
                string speaker = Col(src, 2);
                string value = Col(src, 3);
                string next = Col(src, 4);
                // string notes = Col(src, 5);  // 출력 시 제거

                bool isHeader = i == 0 && lineId.Equals("LineID", StringComparison.OrdinalIgnoreCase);
                bool isComment = lineId.TrimStart().StartsWith("#");
                bool isBlank = string.IsNullOrWhiteSpace(string.Join("", src));

                if (isHeader)
                {
                    result.Rows.Add(new[] { "LineID", "Type", "Speaker", "Value", "Next" });
                    continue;
                }
                if (isComment) { result.Rows.Add(new[] { lineId, "", "", "", "" }); continue; }
                if (isBlank) { result.Rows.Add(new[] { "", "", "", "", "" }); continue; }
                if (string.IsNullOrEmpty(typeStr)) continue; // 작가 메모 행 스킵

                // Type alias 변환 (SFX → Sound)
                string normalizedTypeStr = typeStr.Equals("SFX", StringComparison.OrdinalIgnoreCase) ? "Sound" : typeStr;

                // Type별 정규화
                if (Enum.TryParse<LineType>(normalizedTypeStr, true, out var type))
                {
                    // SFX type이었으면 Value에 SFX: prefix 보장
                    if (type == LineType.Sound && typeStr.Equals("SFX", StringComparison.OrdinalIgnoreCase)
                        && !value.StartsWith("SFX:") && !value.StartsWith("BGM:"))
                    {
                        value = "SFX:" + value;
                    }
                    value = NormalizeByType(type, speaker, value, opt, result, lineId);
                    next = NormalizeNext(type, next);
                }
                else
                {
                    result.Warnings.Add($"[{lineId}] 알 수 없는 Type: {typeStr}");
                }

                result.Rows.Add(new[] { lineId, normalizedTypeStr, speaker, value, next });
            }

            // 4) 패치 머지
            if (!string.IsNullOrEmpty(opt.PatchCsvPath) && File.Exists(opt.PatchCsvPath))
            {
                ApplyPatch(opt.PatchCsvPath, result);
            }

            // 5) 백업 + 쓰기
            EnsureDir(Path.GetDirectoryName(opt.TargetCsvPath));
            try
            {
                if (File.Exists(opt.TargetCsvPath))
                {
                    var bakPath = Path.ChangeExtension(opt.TargetCsvPath, ".bak.csv");
                    File.Copy(opt.TargetCsvPath, bakPath, overwrite: true);
                    Debug.Log($"[StoryCsvConverter] 백업: {bakPath}");
                }
                WriteCsv(opt.TargetCsvPath, result.Rows);
                Debug.Log($"[StoryCsvConverter] 출력: {opt.TargetCsvPath} ({result.Rows.Count} rows)");
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[StoryCsvConverter] 쓰기 실패: {opt.TargetCsvPath} — {ex.Message}");
                result.Warnings.Add($"write failed: {ex.Message}");
            }

            return result;
        }

        // ─── Type별 정규화 ───────────────────────────────
        static string NormalizeByType(LineType type, string speaker, string value, StoryConvertOptions opt, StoryConvertResult result, string lineId)
        {
            switch (type)
            {
                case LineType.Text: return NormalizeText(value, opt, result, lineId);
                case LineType.Char: return NormalizeChar(value, opt, result, lineId);
                case LineType.BG: return NormalizeBg(value, opt, result, lineId);
                case LineType.CG: return NormalizeCg(value, opt, result, lineId);
                case LineType.SD: return NormalizeSd(value, opt, result, lineId);
                case LineType.Sound: return NormalizeSound(value, opt, result, lineId);
                default: return value;
            }
        }

        static string NormalizeText(string value, StoryConvertOptions opt, StoryConvertResult r, string lineId)
        {
            value = NormalizeLineBreaks(value);
            if (opt.Emote == null) return value;
            return EmoteRx.Replace(value, m =>
            {
                var ko = m.Groups[1].Value;
                if (opt.Emote.TryResolve(ko, out var id))
                    return $"<emote={id}/>";
                r.MissingEmote.Add($"{ko} ({lineId})");
                return m.Value;
            });
        }

        /// <summary>
        /// 줄바꿈 정규화:
        ///   1) CRLF/CR → LF
        ///   2) literal `\n` + 실제 LF → literal `\n` (시각상 개행 제거)
        ///   3) 남은 실제 LF → literal `\n`
        /// 결과: CSV Value 안에 실제 개행이 남지 않음 (한 줄)
        /// </summary>
        static string NormalizeLineBreaks(string value)
        {
            if (string.IsNullOrEmpty(value)) return value;
            value = value.Replace("\r\n", "\n").Replace("\r", "\n");
            value = Regex.Replace(value, @"\\n\n+", @"\n");
            value = value.Replace("\n", @"\n");
            return value;
        }

        // 알려진 VirtualOverlay 모드 토큰 (작가가 5번째 segment에 명시)
        static readonly HashSet<string> OverlayModeTokens = new(StringComparer.OrdinalIgnoreCase) { "Mob", "PC" };

        static string NormalizeChar(string value, StoryConvertOptions opt, StoryConvertResult r, string lineId)
        {
            // 신규: C:Enter:로아:기본:Mob → C:Enter:Roa:_00:Mob (모드 토큰 보존)
            // 구버전: C:Enter:로아:활짝:Roa_Mob_1:Fade:4.0 → C:Enter:Roa:_13:Mob (모드만 추출)
            var parts = value.Split(':');
            if (parts.Length < 2 || parts[0] != "C") return value;
            var output = new List<string> { parts[0], parts[1] };
            for (int i = 2; i < parts.Length; i++)
            {
                var p = parts[i];

                // 모드 토큰 단독 (Mob/PC) → 유지
                if (OverlayModeTokens.Contains(p)) { output.Add(p); continue; }

                // legacy overlay 토큰 (`<prefix>_<Mode>_<idx>`) → 모드만 추출
                var modeMatch = Regex.Match(p, @"^[A-Za-z0-9]+_(Mob|PC)(?:_\w+)?$");
                if (modeMatch.Success) { output.Add(modeMatch.Groups[1].Value); continue; }

                // 미구현 파라미터 strip
                if (p.StartsWith("Fade") || Regex.IsMatch(p, @"^\d+(\.\d+)?$"))
                    continue;

                // 한글 캐릭터명 → engineId
                if (opt.Character != null && opt.Character.TryResolve(p, out var ce))
                {
                    output.Add(!string.IsNullOrEmpty(ce.engineId) ? ce.engineId : ce.code);
                    continue;
                }
                // 한글 emote → _NN
                if (opt.Emote != null && opt.Emote.TryResolve(p, out var emoteId))
                {
                    output.Add(emoteId);
                    continue;
                }
                output.Add(p);
            }
            return string.Join(":", output);
        }

        static string NormalizeBg(string value, StoryConvertOptions opt, StoryConvertResult r, string lineId)
        {
            // 작가 표기 BG: "자취방 책상:Cut" 또는 "BG_자취방_책상" 또는 "BG_Black"
            var parts = value.Split(':');
            string head = parts[0];
            string tail = parts.Length > 1 ? ":" + string.Join(":", parts, 1, parts.Length - 1) : "";

            // 이미 엔진 ID 형식이면 (bg_ 또는 BG_ prefix) 그대로
            if (head.StartsWith("bg_") || head.StartsWith("BG_"))
            {
                // transition 없으면 :Cut 부여
                if (string.IsNullOrEmpty(tail)) tail = ":Cut";
                return head + tail;
            }

            // 한글 BG 룩업
            if (opt.Bg != null && opt.Bg.TryResolve(head, out var engineId))
            {
                if (string.IsNullOrEmpty(tail)) tail = ":Cut";
                return engineId + tail;
            }

            r.MissingBg.Add($"{head} ({lineId})");
            return value;
        }

        // CG/SD 액션 키워드 — 리소스 ID가 아니므로 매핑 시도 안 함
        static readonly HashSet<string> ActionKeywords = new(StringComparer.OrdinalIgnoreCase)
        { "Close", "Hide", "Exit", "Off", "End" };

        static string NormalizeCg(string value, StoryConvertOptions opt, StoryConvertResult r, string lineId)
        {
            // 'CG_Roa_01:Fade:4.0' or '로아 첫만남' or 'Close'
            if (ActionKeywords.Contains(value.Trim())) return value.Trim();
            var parts = value.Split(':');
            string head = parts[0];
            // duration만 남기고 Fade 같은 키워드 strip (작가가 :Fade:4.0이라 쓰면 :4.0만 유지하면 엔진이 default duration 사용)
            // 단순화: head만 매핑, tail은 첫 숫자만 남김
            if (opt.Cg != null && opt.Cg.TryResolve(head, out var engineId))
            {
                head = engineId;
            }
            else if (!head.StartsWith("cg_") && !head.StartsWith("CG_"))
            {
                r.MissingCg.Add($"{head} ({lineId})");
            }
            // tail의 숫자만 유지
            string tail = "";
            for (int i = 1; i < parts.Length; i++)
            {
                if (Regex.IsMatch(parts[i], @"^\d+(\.\d+)?$"))
                { tail = ":" + parts[i]; break; }
            }
            return head + tail;
        }

        static string NormalizeSd(string value, StoryConvertOptions opt, StoryConvertResult r, string lineId)
        {
            if (ActionKeywords.Contains(value.Trim())) return value.Trim();
            var parts = value.Split(':');
            string head = parts[0];
            if (opt.Sd != null && opt.Sd.TryResolve(head, out var engineId))
                head = engineId;
            else if (!head.StartsWith("sd_") && !head.StartsWith("SD_"))
                r.MissingSd.Add($"{head} ({lineId})");
            string tail = parts.Length > 1 ? ":" + string.Join(":", parts, 1, parts.Length - 1) : "";
            return head + tail;
        }

        static string NormalizeSound(string value, StoryConvertOptions opt, StoryConvertResult r, string lineId)
        {
            // BGM:Roa:Fade:4.0 / SFX:Click
            var parts = value.Split(':');
            if (parts.Length < 2) return value;
            string kindStr = parts[0]; // BGM or SFX
            string id = parts[1];
            if (Enum.TryParse<SoundMap.Kind>(kindStr, true, out var kind) && opt.Sound != null
                && opt.Sound.TryResolve(kind, id, out var engineId))
            {
                id = engineId;
            }
            // Fade:N 파라미터 strip
            return $"{kindStr}:{id}";
        }

        static string NormalizeNext(LineType type, string next)
        {
            next = (next ?? "").Trim();
            if (!string.IsNullOrEmpty(next)) return next;
            if (type == LineType.Text || type == LineType.Char) return "click";
            return "";
        }

        // ─── 패치 머지 ───────────────────────────────────
        static void ApplyPatch(string patchPath, StoryConvertResult result)
        {
            var text = File.ReadAllText(patchPath);
            var recs = CsvUtility.SplitRecords(text);
            var overrides = new List<(string lineId, string field, string val)>();
            foreach (var rec in recs)
            {
                var c = CsvUtility.SplitCsv(rec.Text);
                if (c.Length < 3) continue;
                if (c[0].Equals("LineID", StringComparison.OrdinalIgnoreCase)) continue;
                if (string.IsNullOrEmpty(c[0]) || c[0].StartsWith("#")) continue;
                overrides.Add((c[0].Trim(), c[1].Trim(), c[2]));
            }
            // 결과 row 인덱스
            var rowIdx = new Dictionary<string, int>();
            for (int i = 0; i < result.Rows.Count; i++)
            {
                if (result.Rows[i].Length >= 1 && !string.IsNullOrEmpty(result.Rows[i][0]))
                    rowIdx[result.Rows[i][0]] = i;
            }
            foreach (var ov in overrides)
            {
                if (!rowIdx.TryGetValue(ov.lineId, out var idx))
                {
                    result.OrphanPatches++;
                    result.Warnings.Add($"orphan patch: {ov.lineId} ({ov.field})");
                    continue;
                }
                int colIdx = ov.field switch
                {
                    "Type" => 1, "Speaker" => 2, "Value" => 3, "Next" => 4, _ => -1
                };
                if (colIdx < 0) continue;
                result.Rows[idx][colIdx] = ov.val;
            }
        }

        // ─── CSV I/O ────────────────────────────────────
        static void WriteCsv(string path, List<string[]> rows)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(EscapeCsvField(row[i]));
                }
                sb.Append('\n');
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        static void WriteCsvInPlace(string path, List<string[]> rows)
        {
            var sb = new StringBuilder();
            foreach (var row in rows)
            {
                for (int i = 0; i < row.Length; i++)
                {
                    if (i > 0) sb.Append(',');
                    sb.Append(EscapeCsvField(row[i]));
                }
                sb.Append('\n');
            }
            File.WriteAllText(path, sb.ToString(), new UTF8Encoding(true));
        }

        static string EscapeCsvField(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            bool needsQuote = s.IndexOfAny(new[] { ',', '"', '\n', '\r' }) >= 0;
            if (!needsQuote) return s;
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        }

        static string Col(string[] cols, int i) => i < cols.Length ? cols[i] : "";

        static void EnsureDir(string dir)
        {
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);
        }
    }
}
#endif
