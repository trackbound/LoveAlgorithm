using System.Text.RegularExpressions;
using LoveAlgo.Contracts;
using NUnit.Framework;
using LoveAlgo.Story;
using UnityEngine;
using UnityEngine.TestTools;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// ScriptParser EditMode 테스트 — 기본 파싱, 빈 줄/주석 처리, 컬럼 부족 fallback,
    /// L2에서 도입한 Strict 토글이 LogWarning을 LogError로 격상시키는지 검증.
    /// </summary>
    [TestFixture]
    public class ScriptParserTests
    {
        [TearDown]
        public void TearDown()
        {
            ScriptParser.Strict = false; // 토글이 다음 테스트로 새지 않도록
        }

        [Test]
        public void Parse_BasicLines_ReturnsExpectedFields()
        {
            string csv = "LineID,Type,Speaker,Value,Next\n" +  // 헤더 (스킵 대상)
                         "L1,Text,Roa,안녕하세요,>\n" +
                         "L2,Text,Roa,잘 지내?,click";
            // CSV 진행 완료 로그 기대 (무해, 콘솔에 남는 정상 로그)
            LogAssert.Expect(LogType.Log, new Regex(@"\[ScriptParser\] \d+개 라인 파싱 완료"));

            var lines = ScriptParser.Parse(csv);

            Assert.AreEqual(2, lines.Count, "헤더는 제외, 2개 데이터 라인");
            Assert.AreEqual("L1", lines[0].LineID);
            Assert.AreEqual(LineType.Text, lines[0].Type);
            Assert.AreEqual("Roa", lines[0].Speaker);
            Assert.AreEqual("안녕하세요", lines[0].Value);
            Assert.AreEqual(NextType.Immediate, lines[0].NextType);
            Assert.AreEqual(NextType.Click, lines[1].NextType);
        }

        [Test]
        public void Parse_BlankLinesAndComments_AreSkipped()
        {
            string csv = "L1,Text,Roa,A,>\n" +
                         "# 이 줄은 주석\n" +
                         "\n" +
                         "L2,Text,Roa,B,>";
            LogAssert.Expect(LogType.Log, new Regex(@"\[ScriptParser\] 2개 라인 파싱 완료"));
            var lines = ScriptParser.Parse(csv);
            Assert.AreEqual(2, lines.Count);
        }

        [Test]
        public void Parse_InsufficientColumns_LogsWarning_And_Skips()
        {
            // 5컬럼 미만 — 옛 동작은 LogWarning + null skip
            string csv = "L1,Text,Roa"; // 3컬럼
            LogAssert.Expect(LogType.Warning, new Regex(@"\[ScriptParser\] Line \d+: 컬럼 부족"));
            LogAssert.Expect(LogType.Log, new Regex(@"\[ScriptParser\] 0개 라인 파싱 완료"));

            var lines = ScriptParser.Parse(csv);

            Assert.AreEqual(0, lines.Count, "잘못된 라인은 skip되어 0개 반환");
        }

        [Test]
        public void Parse_InsufficientColumns_WithStrict_LogsError()
        {
            // L2 Strict 모드: 같은 위반이 LogError로 격상되어 CI/preflight 게이트가 차단할 수 있음
            ScriptParser.Strict = true;

            string csv = "L1,Text,Roa";
            LogAssert.Expect(LogType.Error, new Regex(@"\[ScriptParser\] Line \d+: 컬럼 부족"));
            LogAssert.Expect(LogType.Log, new Regex(@"\[ScriptParser\] 0개 라인 파싱 완료"));

            var lines = ScriptParser.Parse(csv);

            Assert.AreEqual(0, lines.Count);
        }

        [Test]
        public void Parse_UnknownType_LogsWarning_And_Skips()
        {
            string csv = "L1,Bogus,Roa,A,>"; // Bogus는 LineType에 없음
            LogAssert.Expect(LogType.Warning, new Regex(@"\[ScriptParser\] Line \d+: 알 수 없는 Type"));
            LogAssert.Expect(LogType.Log, new Regex(@"\[ScriptParser\] 0개 라인 파싱 완료"));

            var lines = ScriptParser.Parse(csv);

            Assert.AreEqual(0, lines.Count);
        }

        [Test]
        public void Parse_LiteralBackslashN_IsConvertedToNewline()
        {
            // CSV에 \n 리터럴이 들어오면 ScriptParser가 실제 개행으로 치환 (타이핑 효과에서 \이 보이는 버그 방지)
            string csv = "L1,Text,Roa,첫줄\\n둘째줄,>";
            LogAssert.Expect(LogType.Log, new Regex(@"\[ScriptParser\] 1개 라인 파싱 완료"));

            var lines = ScriptParser.Parse(csv);

            Assert.AreEqual(1, lines.Count);
            StringAssert.Contains("\n", lines[0].Value, "리터럴 \\n이 실제 개행으로 치환");
            Assert.IsFalse(lines[0].Value.Contains("\\n"), "리터럴 \\n 문자는 남아있지 않아야 함");
        }
    }
}
