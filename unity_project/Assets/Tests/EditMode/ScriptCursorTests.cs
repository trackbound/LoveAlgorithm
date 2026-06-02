using System.Collections.Generic;
using NUnit.Framework;
using LoveAlgo.Story; // ScriptCursor, ScriptLine, LineType, NextType

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 slice1 검증: 순수 <see cref="ScriptCursor"/>. 순차 진행/LineID 점프/Choice 블록 조회·건너뛰기/
    /// 종료 판정을 결정적으로 확인. 구 ScriptRunner의 인덱스·점프 책임만 추려낸 것.
    /// </summary>
    [TestFixture]
    public class ScriptCursorTests
    {
        static ScriptLine Line(string id, LineType type, string value = "")
            => new ScriptLine(id, type, "", value, NextType.Immediate);

        [Test]
        public void Sequential_Advance_Until_End()
        {
            var lines = new List<ScriptLine> { Line("", LineType.Text, "a"), Line("", LineType.Text, "b") };
            var c = new ScriptCursor(lines);

            Assert.IsTrue(c.HasCurrent);
            Assert.AreEqual("a", c.Current.Value);
            c.MoveNext();
            Assert.AreEqual("b", c.Current.Value);
            c.MoveNext();
            Assert.IsFalse(c.HasCurrent, "범위 밖 → 종료");
            Assert.IsNull(c.Current);
        }

        [Test]
        public void TryJump_Moves_To_Labeled_Line()
        {
            var lines = new List<ScriptLine>
            {
                Line("", LineType.Text, "a"),
                Line("target", LineType.Text, "b"),
                Line("", LineType.Text, "c"),
            };
            var c = new ScriptCursor(lines);

            Assert.IsTrue(c.TryJump("target"));
            Assert.AreEqual("b", c.Current.Value);
            Assert.AreEqual(1, c.Index);
        }

        [Test]
        public void TryJump_Unknown_Returns_False_No_Move()
        {
            var lines = new List<ScriptLine> { Line("", LineType.Text, "a") };
            var c = new ScriptCursor(lines);

            Assert.IsFalse(c.TryJump("nope"));
            Assert.AreEqual(0, c.Index, "실패 시 커서 불변");
        }

        [Test]
        public void PeekOptionValues_Collects_Following_Options_Only()
        {
            var lines = new List<ScriptLine>
            {
                Line("", LineType.Choice),
                Line("", LineType.Option, "A|a"),
                Line("", LineType.Option, "B|b"),
                Line("", LineType.Text, "after"),
            };
            var c = new ScriptCursor(lines); // 커서는 Choice(0)

            var opts = c.PeekOptionValues();
            CollectionAssert.AreEqual(new[] { "A|a", "B|b" }, opts);
            Assert.AreEqual(0, c.Index, "조회는 커서를 옮기지 않음");
        }

        [Test]
        public void SkipChoiceBlock_Advances_Past_Choice_And_Options()
        {
            var lines = new List<ScriptLine>
            {
                Line("", LineType.Choice),
                Line("", LineType.Option, "A|a"),
                Line("", LineType.Option, "B|b"),
                Line("", LineType.Text, "after"),
            };
            var c = new ScriptCursor(lines);

            c.SkipChoiceBlock();
            Assert.AreEqual("after", c.Current.Value, "Choice+Option 블록 다음 라인으로");
        }

        [Test]
        public void Duplicate_Label_Resolves_To_First()
        {
            var lines = new List<ScriptLine>
            {
                Line("dup", LineType.Text, "first"),
                Line("dup", LineType.Text, "second"),
            };
            var c = new ScriptCursor(lines);

            Assert.IsTrue(c.TryJump("dup"));
            Assert.AreEqual("first", c.Current.Value);
        }
    }
}
