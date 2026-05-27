using NUnit.Framework;
using LoveAlgo.Story;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D10 선택지 폴리쉬 — 중요 마커('*' 또는 '[important]') 파싱 검증.
    /// 시각 폴리쉬(stagger/hover/pulse)는 PlayMode 필요 — 마커 추출만 EditMode로.
    /// </summary>
    [TestFixture]
    public class OptionDataImportantMarkerTests
    {
        [Test]
        public void Parse_NoMarker_NotImportant()
        {
            var o = OptionData.Parse("일반 선택지|next");
            Assert.IsFalse(o.IsImportant);
            Assert.AreEqual("일반 선택지", o.ButtonText);
        }

        [Test]
        public void Parse_StarPrefix_MarkedImportant_AndStripped()
        {
            var o = OptionData.Parse("*중요한 결정|next");
            Assert.IsTrue(o.IsImportant);
            Assert.AreEqual("중요한 결정", o.ButtonText);
        }

        [Test]
        public void Parse_StarPrefix_WithLeadingSpace_StillStripped()
        {
            var o = OptionData.Parse(" * 공백 포함|next");
            Assert.IsTrue(o.IsImportant);
            Assert.AreEqual("공백 포함", o.ButtonText);
        }

        [Test]
        public void Parse_ImportantToken_AnywhereInText_Stripped()
        {
            var o = OptionData.Parse("이 선택[important]은 중요|next");
            Assert.IsTrue(o.IsImportant);
            Assert.AreEqual("이 선택은 중요", o.ButtonText);
        }

        [Test]
        public void Parse_ImportantToken_CaseInsensitive()
        {
            var o = OptionData.Parse("선택 [IMPORTANT]|next");
            Assert.IsTrue(o.IsImportant);
            Assert.AreEqual("선택", o.ButtonText.Trim());
        }

        [Test]
        public void Parse_BothMarkers_StillStripsBoth()
        {
            var o = OptionData.Parse("*텍스트[important] 추가|next");
            Assert.IsTrue(o.IsImportant);
            Assert.AreEqual("텍스트 추가", o.ButtonText);
        }

        [Test]
        public void Parse_OnlyStarCharacter_NoText()
        {
            // 마커만 있고 텍스트가 없으면 빈 ButtonText + IsImportant=true
            var o = OptionData.Parse("*|next");
            Assert.IsTrue(o.IsImportant);
            Assert.AreEqual("", o.ButtonText);
        }

        [Test]
        public void Parse_StarInsideText_NotStripped()
        {
            // 'A*B' 같은 가운데 별표는 마커가 아님 — 접두사만 인식
            var o = OptionData.Parse("A*B|next");
            Assert.IsFalse(o.IsImportant);
            Assert.AreEqual("A*B", o.ButtonText);
        }

        [Test]
        public void Parse_PreservesJumpTargetAndEffects()
        {
            var o = OptionData.Parse("*중요|next|Love:Roa:5|if:Flag:Test");
            Assert.IsTrue(o.IsImportant);
            Assert.AreEqual("중요", o.ButtonText);
            Assert.AreEqual("next", o.JumpTarget);
            Assert.AreEqual(1, o.Effects.Count);
            Assert.AreEqual("Love:Roa:5", o.Effects[0]);
            Assert.AreEqual("Flag:Test", o.Condition);
        }

        [Test]
        public void ExtractMarker_NullReturnsEmpty()
        {
            // ExtractImportantMarker는 internal — 같은 어셈블리에서 호출
            string r = OptionData.ExtractImportantMarker(null, out var imp);
            Assert.AreEqual("", r);
            Assert.IsFalse(imp);
        }

        [Test]
        public void ExtractMarker_EmptyReturnsEmpty()
        {
            string r = OptionData.ExtractImportantMarker("", out var imp);
            Assert.AreEqual("", r);
            Assert.IsFalse(imp);
        }
    }
}
