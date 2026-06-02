using NUnit.Framework;
using LoveAlgo.Story; // StageInterpreter
using LoveAlgo.Events; // BgTransition, CharSlot, CharAction

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="StageInterpreter"/>. BG/Char Value 문법을 인텐트로 분해하는지
    /// (슬롯 생략→C, 전환 생략→Cross, duration 미지정→-1, 케이스 무시, 형식오류→IsValid=false).
    /// </summary>
    [TestFixture]
    public class StageInterpreterTests
    {
        // ── BG ──

        [Test]
        public void Bg_Full_Splits_Name_Transition_Duration()
        {
            var bg = StageInterpreter.ParseBackground("bg_10_01:Cross:0.7");
            Assert.IsTrue(bg.IsValid);
            Assert.AreEqual("bg_10_01", bg.Name);
            Assert.AreEqual(BgTransition.Cross, bg.Transition);
            Assert.AreEqual(0.7f, bg.Duration, 1e-4f);
        }

        [Test]
        public void Bg_NameOnly_Defaults_Cross_And_Unspecified_Duration()
        {
            var bg = StageInterpreter.ParseBackground("bg_10_01");
            Assert.AreEqual("bg_10_01", bg.Name);
            Assert.AreEqual(BgTransition.Cross, bg.Transition);
            Assert.Less(bg.Duration, 0f); // -1 = 기본값 위임
        }

        [Test]
        public void Bg_Cut_And_Fade_Parsed_CaseInsensitive()
        {
            Assert.AreEqual(BgTransition.Cut, StageInterpreter.ParseBackground("bg:cut").Transition);
            Assert.AreEqual(BgTransition.Fade, StageInterpreter.ParseBackground("bg:FADE").Transition);
        }

        [Test]
        public void Bg_Empty_Is_Invalid()
        {
            Assert.IsFalse(StageInterpreter.ParseBackground("").IsValid);
            Assert.IsFalse(StageInterpreter.ParseBackground(null).IsValid);
        }

        // ── Char ──

        [Test]
        public void Char_Enter_NoSlot_Defaults_Center()
        {
            var c = StageInterpreter.ParseCharacter("Enter:c01:00");
            Assert.IsTrue(c.IsValid);
            Assert.AreEqual(CharSlot.C, c.Slot);
            Assert.AreEqual(CharAction.Enter, c.Action);
            Assert.AreEqual("c01", c.Character);
            Assert.AreEqual("00", c.Emote);
        }

        [Test]
        public void Char_Enter_WithSlot()
        {
            var c = StageInterpreter.ParseCharacter("L:Enter:c02:11");
            Assert.AreEqual(CharSlot.L, c.Slot);
            Assert.AreEqual("c02", c.Character);
            Assert.AreEqual("11", c.Emote);
        }

        [Test]
        public void Char_Enter_NoEmote_Yields_Empty_Emote()
        {
            var c = StageInterpreter.ParseCharacter("Enter:c03");
            Assert.AreEqual("c03", c.Character);
            Assert.AreEqual("", c.Emote);
        }

        [Test]
        public void Char_Emote_Sets_Emote_Only()
        {
            var c = StageInterpreter.ParseCharacter("C:Emote:21");
            Assert.AreEqual(CharAction.Emote, c.Action);
            Assert.AreEqual("21", c.Emote);
            Assert.IsNull(c.Character);
        }

        [Test]
        public void Char_Exit_And_Clear_Parse_Slot_And_Action()
        {
            var exit = StageInterpreter.ParseCharacter("R:Exit");
            Assert.AreEqual(CharSlot.R, exit.Slot);
            Assert.AreEqual(CharAction.Exit, exit.Action);

            var clear = StageInterpreter.ParseCharacter("Clear");
            Assert.AreEqual(CharSlot.C, clear.Slot);
            Assert.AreEqual(CharAction.Clear, clear.Action);
        }

        [Test]
        public void Char_CaseInsensitive_Slot_And_Action()
        {
            var c = StageInterpreter.ParseCharacter("left:enter:c01:00");
            Assert.AreEqual(CharSlot.L, c.Slot);
            Assert.AreEqual(CharAction.Enter, c.Action);
        }

        [Test]
        public void Char_Empty_Or_SlotOnly_Is_Invalid()
        {
            Assert.IsFalse(StageInterpreter.ParseCharacter("").IsValid);
            Assert.IsFalse(StageInterpreter.ParseCharacter("L").IsValid);        // 슬롯만, 액션 없음
            Assert.IsFalse(StageInterpreter.ParseCharacter("Enter").IsValid);    // Enter인데 캐릭터 없음
        }
    }
}
