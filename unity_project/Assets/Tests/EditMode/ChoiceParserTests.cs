using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;    // ScriptableObject
using LoveAlgo.Core;  // GameStateSO
using LoveAlgo.Story; // ChoiceParser, ChoiceOption

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 slice1 검증: 순수 <see cref="ChoiceParser"/>. Option Value 문법
    /// (<c>버튼텍스트|점프대상|효과…|if:조건</c>)을 구 OptionData.Parse와 동일하게 분해하는지.
    /// </summary>
    [TestFixture]
    public class ChoiceParserTests
    {
        [Test]
        public void Parse_Full_Splits_Text_Jump_Effects_Condition()
        {
            var o = ChoiceParser.ParseOption("같이 가자|roa_yes|Love:HaYeEun:5|Money:100|if:Flag:met");

            Assert.AreEqual("같이 가자", o.ButtonText);
            Assert.AreEqual("roa_yes", o.JumpTarget);
            CollectionAssert.AreEqual(new[] { "Love:HaYeEun:5", "Money:100" }, o.Effects);
            Assert.AreEqual("Flag:met", o.Condition);
        }

        [Test]
        public void Parse_Mark_Token_Into_Mark_OrderIndependent()
        {
            // mark:는 if:/효과와 순서 무관하게 3번째+ 토큰 어디에 와도 Mark로 분리.
            var o = ChoiceParser.ParseOption("로아 선택|met|mark:met_roa|Love:Roa:3|if:Flag:intro");

            Assert.AreEqual("로아 선택", o.ButtonText);
            Assert.AreEqual("met", o.JumpTarget);
            Assert.AreEqual("met_roa", o.Mark);
            Assert.AreEqual("Flag:intro", o.Condition);
            CollectionAssert.AreEqual(new[] { "Love:Roa:3" }, o.Effects);
        }

        [Test]
        public void Parse_NoMark_LeavesMarkNull()
        {
            var o = ChoiceParser.ParseOption("선택|target|Stat:Int:2");
            Assert.IsNull(o.Mark);
        }

        [Test]
        public void Parse_TextOnly_Has_No_Jump_Or_Effects()
        {
            var o = ChoiceParser.ParseOption("아무 말 없이 지나간다");

            Assert.AreEqual("아무 말 없이 지나간다", o.ButtonText);
            Assert.IsNull(o.JumpTarget);
            Assert.IsEmpty(o.Effects);
            Assert.IsNull(o.Condition);
        }

        [Test]
        public void Parse_Empty_Yields_Empty_Option()
        {
            var o = ChoiceParser.ParseOption("");
            Assert.IsNull(o.ButtonText);
            Assert.IsNull(o.JumpTarget);
            Assert.IsEmpty(o.Effects);
        }

        [Test]
        public void Parse_Skips_Blank_Effect_Tokens()
        {
            // 연속 파이프(||)로 생기는 빈 토큰은 효과로 추가되지 않는다.
            var o = ChoiceParser.ParseOption("선택|target||Stat:Int:2");
            CollectionAssert.AreEqual(new[] { "Stat:Int:2" }, o.Effects);
        }

        [Test]
        public void ParseOptions_Batch_Preserves_Order()
        {
            var list = ChoiceParser.ParseOptions(new List<string> { "A|a", "B|b", "C|c" });
            Assert.AreEqual(3, list.Count);
            Assert.AreEqual("A", list[0].ButtonText);
            Assert.AreEqual("b", list[1].JumpTarget);
            Assert.AreEqual("C", list[2].ButtonText);
        }

        [Test]
        public void VisibleOptions_Filters_By_Condition()
        {
            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            gs.ResetRuntime();
            gs.SetFlag("met", true);
            gs.SetStat("Int", 10);
            try
            {
                var opts = ChoiceParser.ParseOptions(new[]
                {
                    "무조건|a",                    // 조건 없음 → 표시
                    "플래그있음|b|if:Flag:met",      // met=true → 표시
                    "플래그없음|c|if:Flag:none",     // 미설정 → 숨김
                    "스탯게이트|d|if:Stat:Int>=20",  // Int 10<20 → 숨김
                });
                var visible = ChoiceParser.VisibleOptions(opts, gs);
                CollectionAssert.AreEqual(new[] { "무조건", "플래그있음" }, visible.ConvertAll(o => o.ButtonText));
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void VisibleOptions_NullState_Keeps_Unconditioned_Only()
        {
            var opts = ChoiceParser.ParseOptions(new[] { "무조건|a", "조건부|b|if:Flag:x" });
            var visible = ChoiceParser.VisibleOptions(opts, null);
            CollectionAssert.AreEqual(new[] { "무조건" }, visible.ConvertAll(o => o.ButtonText));
        }
    }
}
