using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using LoveAlgo.Story;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// ScriptValidator EditMode 테스트 — L6에서 보강한 Char 라인 검증과
    /// 기존 FX/BG 검증이 올바르게 위반을 보고하는지 확인.
    /// </summary>
    [TestFixture]
    public class ScriptValidatorTests
    {
        [Test]
        public void Validate_ValidCharLine_NoViolation()
        {
            var lines = new List<ScriptLine>
            {
                new ScriptLine("L1", LineType.Char, "", "C:Enter:로아:Default", NextType.Immediate)
            };

            var result = ScriptValidator.Validate(lines);

            Assert.IsEmpty(result, "정상 Char 라인은 위반 없음");
        }

        [Test]
        public void Validate_CharLine_EnterWithoutCharacterName_ReportsError()
        {
            // L6: Enter는 캐릭터 이름이 최소 1개 필요
            var lines = new List<ScriptLine>
            {
                new ScriptLine("L1", LineType.Char, "", "C:Enter", NextType.Immediate)
            };

            var result = ScriptValidator.Validate(lines);

            Assert.IsNotEmpty(result, "Enter에 캐릭터 이름 없음 → 위반 보고");
            Assert.AreEqual("Error", result[0].Severity);
            StringAssert.Contains("Enter는 캐릭터 이름이 필요", result[0].Message);
        }

        [Test]
        public void Validate_CharLine_EmoteWithoutValue_ReportsError()
        {
            // L6: Emote는 표정 인자가 최소 1개 필요
            var lines = new List<ScriptLine>
            {
                new ScriptLine("L1", LineType.Char, "", "C:Emote", NextType.Immediate)
            };

            var result = ScriptValidator.Validate(lines);

            Assert.IsNotEmpty(result);
            Assert.AreEqual("Error", result[0].Severity);
            StringAssert.Contains("Emote는 표정 인자가 필요", result[0].Message);
        }

        [Test]
        public void Validate_CharLine_UnknownFirstToken_ReportsError()
        {
            // 슬롯도 액션도 아닌 첫 토큰
            var lines = new List<ScriptLine>
            {
                new ScriptLine("L1", LineType.Char, "", "Bogus:foo", NextType.Immediate)
            };

            var result = ScriptValidator.Validate(lines);

            Assert.IsNotEmpty(result);
            Assert.AreEqual("Error", result[0].Severity);
            StringAssert.Contains("첫 토큰이 슬롯", result[0].Message);
        }

        [Test]
        public void Validate_CharLine_SlotOnlyNoAction_ReportsError()
        {
            // 슬롯만 있고 액션이 없는 케이스 — L6에서 추가 검증
            var lines = new List<ScriptLine>
            {
                new ScriptLine("L1", LineType.Char, "", "L", NextType.Immediate)
            };

            var result = ScriptValidator.Validate(lines);

            Assert.IsNotEmpty(result);
            Assert.AreEqual("Error", result[0].Severity);
            StringAssert.Contains("액션이 없음", result[0].Message);
        }

        [Test]
        public void Validate_CharLine_ExitWithoutArgs_OK()
        {
            // Exit/ExitDown/Clear는 인자 없어도 OK (L6 규약)
            var lines = new List<ScriptLine>
            {
                new ScriptLine("L1", LineType.Char, "", "C:Exit", NextType.Immediate),
                new ScriptLine("L2", LineType.Char, "", "L:ExitDown", NextType.Immediate),
                new ScriptLine("L3", LineType.Char, "", "R:Clear", NextType.Immediate),
            };

            var result = ScriptValidator.Validate(lines);

            Assert.IsEmpty(result, "Exit/ExitDown/Clear는 인자 없어도 통과");
        }

        [Test]
        public void Validate_FXLine_UnknownCommand_ReportsError()
        {
            var lines = new List<ScriptLine>
            {
                new ScriptLine("L1", LineType.FX, "", "BogusEffect:1.0", NextType.Immediate)
            };

            var result = ScriptValidator.Validate(lines);

            Assert.IsNotEmpty(result);
            Assert.AreEqual("Error", result[0].Severity);
            StringAssert.Contains("알 수 없는 FX/매크로", result[0].Message);
        }

        [Test]
        public void FormatReport_EmptyViolations_ReturnsOk()
        {
            string report = ScriptValidator.FormatReport(new List<ScriptValidator.Violation>());
            StringAssert.Contains("OK", report);
        }

        [Test]
        public void FormatReport_WithViolations_ContainsCount()
        {
            var lines = new List<ScriptLine>
            {
                new ScriptLine("L1", LineType.Char, "", "C:Enter", NextType.Immediate),
                new ScriptLine("L2", LineType.Char, "", "C:Emote", NextType.Immediate),
            };
            var violations = ScriptValidator.Validate(lines);

            string report = ScriptValidator.FormatReport(violations);

            // 위반 수 + 카테고리 카운트가 보고서에 들어감
            StringAssert.Contains("총 " + violations.Count, report);
            StringAssert.Contains("Error", report);
        }
    }
}
