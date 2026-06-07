using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo;                   // EventScriptCatalogSO
using LoveAlgo.Story;             // ScriptLine, LineType
using LoveAlgo.Story.StoryEngine; // ScriptParser, ScriptValidator

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 실 트리거 회귀 가드: EventScriptCatalog.asset의 모든 엔트리가 (1) 실제 StreamingAssets/Story 파일을
    /// 가리키고 (2) 파싱되어 (3) 검증 Error 0 + 점프 대상(Jump/If/선택지)이 전부 존재하는지. 타임라인의 이벤트
    /// 태그(Day6~30) 전수가 카탈로그에 매핑됐는지도 못박는다 — CSV/카탈로그가 어긋나면 여기서 먼저 깨진다.
    /// </summary>
    public class EventCsvIntegrityTests
    {
        static string StoryDir => Path.Combine(Application.streamingAssetsPath, "Story");

        static EventScriptCatalogSO LoadCatalog()
        {
            var so = Resources.Load<EventScriptCatalogSO>("Data/EventScriptCatalog");
            Assert.IsNotNull(so, "Resources/Data/EventScriptCatalog.asset 존재");
            return so;
        }

        // 타임라인(GameTimeline) 이벤트일 전수 — 기획서 Day6·10~12·16·20~22·26·30.
        static readonly string[] TimelineTags =
        {
            "Event1", "Festival_Day1", "Festival_Day2", "Festival_Day3", "Event2",
            "MT_Day1", "MT_Day2", "MT_Day3", "Event3", "Confession",
        };

        [Test]
        public void All_Timeline_EventTags_Are_Mapped()
        {
            var catalog = LoadCatalog();
            foreach (string tag in TimelineTags)
                Assert.IsNotNull(catalog.Resolve(tag), $"타임라인 태그 '{tag}' 카탈로그 매핑");
        }

        [Test]
        public void All_Catalog_Csvs_Exist_Parse_And_Validate_Clean()
        {
            var catalog = LoadCatalog();
            Assert.IsTrue(catalog.Entries.Count > 0, "카탈로그 엔트리 존재");

            foreach (var entry in catalog.Entries)
            {
                string path = Path.Combine(StoryDir, entry.csvPath);
                Assert.IsTrue(File.Exists(path), $"{entry.eventTag}: 파일 존재 — {entry.csvPath}");

                var lines = ScriptParser.Parse(File.ReadAllText(path));
                Assert.IsTrue(lines.Count > 0, $"{entry.eventTag}: 파싱 결과 비어있지 않음");

                var errors = ScriptValidator.Validate(lines)
                    .Where(v => v.Severity == "Error").Select(v => v.ToString()).ToList();
                Assert.IsEmpty(errors, $"{entry.eventTag}: 검증 Error 0\n{string.Join("\n", errors)}");

                AssertJumpTargetsExist(entry.eventTag, lines);
            }
        }

        // Jump/If 점프대상·선택지 점프대상이 라벨(LineID)로 존재하는지 — 런타임 '점프 대상 없음' 에러 선제 차단.
        static void AssertJumpTargetsExist(string tag, List<ScriptLine> lines)
        {
            var labels = new HashSet<string>(lines.Where(l => !string.IsNullOrEmpty(l.LineID)).Select(l => l.LineID));

            foreach (var line in lines)
            {
                if (line.Type == LineType.Flow && line.Value != null)
                {
                    if (line.Value.StartsWith("Jump:"))
                        AssertLabel(tag, labels, line.Value.Substring(5).Trim(), line.Value);
                    else if (line.Value.StartsWith("If:"))
                    {
                        int last = line.Value.LastIndexOf(':');
                        if (last > 2) AssertLabel(tag, labels, line.Value.Substring(last + 1).Trim(), line.Value);
                    }
                }
                else if (line.Type == LineType.Option && line.Value != null)
                {
                    // "라벨|점프[|효과...]" — 2번째 토큰이 점프 대상(있을 때만).
                    var parts = line.Value.Split('|');
                    if (parts.Length >= 2 && !string.IsNullOrWhiteSpace(parts[1]) && !parts[1].Contains(":"))
                        AssertLabel(tag, labels, parts[1].Trim(), line.Value);
                }
            }
        }

        static void AssertLabel(string tag, HashSet<string> labels, string target, string raw)
            => Assert.IsTrue(labels.Contains(target), $"{tag}: 점프 대상 '{target}' 존재 — \"{raw}\"");
    }
}
