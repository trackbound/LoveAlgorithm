using NUnit.Framework;
using LoveAlgo.Story; // StageParser
using LoveAlgo.Events; // BgTransition, CharSlot, CharAction

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="StageParser"/>. BG/Char Value 문법을 인텐트로 분해하는지
    /// (슬롯 생략→C, 전환 생략→Cross, duration 미지정→-1, 케이스 무시, 형식오류→IsValid=false).
    /// </summary>
    [TestFixture]
    public class StageParserTests
    {
        // ── BG ──

        [Test]
        public void Bg_Full_Splits_Name_Transition_Duration()
        {
            var bg = StageParser.ParseBackground("bg_10_01:Cross:0.7");
            Assert.IsTrue(bg.IsValid);
            Assert.AreEqual("bg_10_01", bg.Name);
            Assert.AreEqual(BgTransition.Cross, bg.Transition);
            Assert.AreEqual(0.7f, bg.Duration, 1e-4f);
        }

        [Test]
        public void Bg_NameOnly_Defaults_Cross_And_Unspecified_Duration()
        {
            var bg = StageParser.ParseBackground("bg_10_01");
            Assert.AreEqual("bg_10_01", bg.Name);
            Assert.AreEqual(BgTransition.Cross, bg.Transition);
            Assert.Less(bg.Duration, 0f); // -1 = 기본값 위임
        }

        [Test]
        public void Bg_Cut_And_Fade_Parsed_CaseInsensitive()
        {
            Assert.AreEqual(BgTransition.Cut, StageParser.ParseBackground("bg:cut").Transition);
            Assert.AreEqual(BgTransition.Fade, StageParser.ParseBackground("bg:FADE").Transition);
        }

        [Test]
        public void Bg_Empty_Is_Invalid()
        {
            Assert.IsFalse(StageParser.ParseBackground("").IsValid);
            Assert.IsFalse(StageParser.ParseBackground(null).IsValid);
        }

        // ── Char ──

        [Test]
        public void Char_Enter_NoSlot_Defaults_Center()
        {
            var c = StageParser.ParseCharacter("Enter:c01:00");
            Assert.IsTrue(c.IsValid);
            Assert.AreEqual(CharSlot.C, c.Slot);
            Assert.AreEqual(CharAction.Enter, c.Action);
            Assert.AreEqual("c01", c.Character);
            Assert.AreEqual("00", c.Emote);
        }

        [Test]
        public void Char_Enter_WithSlot()
        {
            var c = StageParser.ParseCharacter("L:Enter:c02:11");
            Assert.AreEqual(CharSlot.L, c.Slot);
            Assert.AreEqual("c02", c.Character);
            Assert.AreEqual("11", c.Emote);
        }

        [Test]
        public void Char_Enter_NoEmote_Yields_Empty_Emote()
        {
            var c = StageParser.ParseCharacter("Enter:c03");
            Assert.AreEqual("c03", c.Character);
            Assert.AreEqual("", c.Emote);
        }

        [Test]
        public void Char_Emote_Sets_Emote_Only()
        {
            var c = StageParser.ParseCharacter("C:Emote:21");
            Assert.AreEqual(CharAction.Emote, c.Action);
            Assert.AreEqual("21", c.Emote);
            Assert.IsNull(c.Character);
        }

        [Test]
        public void Char_Exit_And_Clear_Parse_Slot_And_Action()
        {
            var exit = StageParser.ParseCharacter("R:Exit");
            Assert.AreEqual(CharSlot.R, exit.Slot);
            Assert.AreEqual(CharAction.Exit, exit.Action);

            var clear = StageParser.ParseCharacter("Clear");
            Assert.AreEqual(CharSlot.C, clear.Slot);
            Assert.AreEqual(CharAction.Clear, clear.Action);
        }

        [Test]
        public void Char_CaseInsensitive_Slot_And_Action()
        {
            var c = StageParser.ParseCharacter("left:enter:c01:00");
            Assert.AreEqual(CharSlot.L, c.Slot);
            Assert.AreEqual(CharAction.Enter, c.Action);
        }

        [Test]
        public void Char_Empty_Or_SlotOnly_Is_Invalid()
        {
            Assert.IsFalse(StageParser.ParseCharacter("").IsValid);
            Assert.IsFalse(StageParser.ParseCharacter("L").IsValid);        // 슬롯만, 액션 없음
            Assert.IsFalse(StageParser.ParseCharacter("Enter").IsValid);    // Enter인데 캐릭터 없음
        }

        // ── Char 단축 문법(Task 3) ──

        [Test]
        public void Char_Shorthand_CharColonEmote_Is_Identity_Emote()
        {
            var c = StageParser.ParseCharacter("Roa:웃음");
            Assert.IsTrue(c.IsValid);
            Assert.AreEqual(CharAction.Emote, c.Action);
            Assert.AreEqual("Roa", c.Character);
            Assert.AreEqual("웃음", c.Emote);
            Assert.AreEqual(EmoteTarget.Character, c.Target);
        }

        [Test]
        public void Char_Shorthand_BareEmote_Targets_LastSpeaker()
        {
            var c = StageParser.ParseCharacter("웃음");
            Assert.IsTrue(c.IsValid);
            Assert.AreEqual(CharAction.Emote, c.Action);
            Assert.IsNull(c.Character);
            Assert.AreEqual("웃음", c.Emote);
            Assert.AreEqual(EmoteTarget.LastSpeaker, c.Target);
        }

        [Test]
        public void Char_Explicit_Emote_Keeps_Slot_Target()
        {
            var c = StageParser.ParseCharacter("C:Emote:21");
            Assert.AreEqual(CharAction.Emote, c.Action);
            Assert.AreEqual(EmoteTarget.Slot, c.Target);
        }

        // ── Char Enter 디바이스 토큰(Task 3) ──

        [Test]
        public void ParseCharacter_Enter_WithDevice()
        {
            var c = StageParser.ParseCharacter("Enter:로아:기본:pc");
            Assert.AreEqual(CharAction.Enter, c.Action);
            Assert.AreEqual("로아", c.Character);
            Assert.AreEqual("기본", c.Emote);
            Assert.AreEqual("pc", c.Device);
        }

        [Test]
        public void ParseCharacter_Enter_NoDevice_EmptyDevice()
        {
            var c = StageParser.ParseCharacter("Enter:로아:기본");
            Assert.AreEqual("기본", c.Emote);
            Assert.AreEqual("", c.Device);
        }

        [Test]
        public void ParseCharacter_Exit_NoDevice()
        {
            var c = StageParser.ParseCharacter("Exit");
            Assert.AreEqual(CharAction.Exit, c.Action);
            Assert.AreEqual("", c.Device);
        }

        [Test]
        public void ParseCharacter_EmoteShortcut_Unaffected()
        {
            var c = StageParser.ParseCharacter("로아:웃음");
            Assert.AreEqual(CharAction.Emote, c.Action);
            Assert.AreEqual("로아", c.Character);
            Assert.AreEqual("웃음", c.Emote);
            Assert.AreEqual("", c.Device);
        }
    }
}
