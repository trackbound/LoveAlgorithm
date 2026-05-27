using NUnit.Framework;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D9 대사 효과 태그 파서 — 엣지케이스 다수.
    /// "돌다리 두드리며" 라운드의 핵심: 정규식/문자열 처리는 회귀 위험 큼.
    /// </summary>
    [TestFixture]
    public class DialogueEffectsParserTests
    {
        [Test]
        public void Parse_PlainText_NoEffects_PassesThrough()
        {
            var r = DialogueEffectsParser.Parse("Hello world!");
            Assert.AreEqual("Hello world!", r.CleanText);
            Assert.AreEqual(0, r.Effects.Count);
        }

        [Test]
        public void Parse_Null_ReturnsEmpty()
        {
            var r = DialogueEffectsParser.Parse(null);
            Assert.AreEqual("", r.CleanText);
            Assert.AreEqual(0, r.Effects.Count);
        }

        [Test]
        public void Parse_Empty_ReturnsEmpty()
        {
            var r = DialogueEffectsParser.Parse("");
            Assert.AreEqual("", r.CleanText);
            Assert.AreEqual(0, r.Effects.Count);
        }

        [Test]
        public void Parse_SimpleShake_StripsTag_ProducesRange()
        {
            var r = DialogueEffectsParser.Parse("<shake>X</shake>");
            Assert.AreEqual("X", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(DialogueEffectKind.Shake, r.Effects[0].Kind);
            Assert.AreEqual(0, r.Effects[0].Start);
            Assert.AreEqual(1, r.Effects[0].End);
            Assert.AreEqual(1f, r.Effects[0].Intensity);
        }

        [Test]
        public void Parse_ShakeWithIntensity_CapturesValue()
        {
            var r = DialogueEffectsParser.Parse("<shake=2.5>X</shake>");
            Assert.AreEqual("X", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(2.5f, r.Effects[0].Intensity);
        }

        [Test]
        public void Parse_WaveTag_Recognized()
        {
            var r = DialogueEffectsParser.Parse("<wave>AB</wave>");
            Assert.AreEqual("AB", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(DialogueEffectKind.Wave, r.Effects[0].Kind);
        }

        [Test]
        public void Parse_EmphTag_Recognized()
        {
            var r = DialogueEffectsParser.Parse("<emph>!!!</emph>");
            Assert.AreEqual("!!!", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(DialogueEffectKind.Emph, r.Effects[0].Kind);
        }

        [Test]
        public void Parse_TagInsideSentence_PositionsCorrect()
        {
            var r = DialogueEffectsParser.Parse("Hi <shake>X</shake>!");
            Assert.AreEqual("Hi X!", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(3, r.Effects[0].Start, "'Hi '의 길이 3에서 시작");
            Assert.AreEqual(4, r.Effects[0].End);
        }

        [Test]
        public void Parse_AdjacentDifferentTags_TwoRanges()
        {
            var r = DialogueEffectsParser.Parse("<shake>A</shake><wave>B</wave>");
            Assert.AreEqual("AB", r.CleanText);
            Assert.AreEqual(2, r.Effects.Count);
        }

        [Test]
        public void Parse_NestedTags_BothRangesRecorded()
        {
            // <wave><shake>X</shake></wave>
            var r = DialogueEffectsParser.Parse("<wave><shake>X</shake></wave>");
            Assert.AreEqual("X", r.CleanText);
            Assert.AreEqual(2, r.Effects.Count);
            // 두 효과 모두 (0, 1) 구간
            foreach (var e in r.Effects)
            {
                Assert.AreEqual(0, e.Start);
                Assert.AreEqual(1, e.End);
            }
        }

        [Test]
        public void Parse_EmptyTag_NoRangeRecorded()
        {
            // <shake></shake> → no inner content → no effect
            var r = DialogueEffectsParser.Parse("Hi<shake></shake>!");
            Assert.AreEqual("Hi!", r.CleanText);
            Assert.AreEqual(0, r.Effects.Count, "빈 태그는 효과 안 만듦");
        }

        [Test]
        public void Parse_UnclosedTag_ExtendsToEnd()
        {
            // <shake>Foo (close 없음) → "Foo"까지 효과 적용 (관대 정책)
            var r = DialogueEffectsParser.Parse("<shake>Foo");
            Assert.AreEqual("Foo", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(0, r.Effects[0].Start);
            Assert.AreEqual(3, r.Effects[0].End);
        }

        [Test]
        public void Parse_OrphanCloseTag_Ignored()
        {
            // </shake>만 있고 매칭 open 없음 → close 무시, 텍스트 그대로
            var r = DialogueEffectsParser.Parse("Hi</shake> there");
            Assert.AreEqual("Hi there", r.CleanText);
            Assert.AreEqual(0, r.Effects.Count);
        }

        [Test]
        public void Parse_MismatchedCloseTag_OpenStaysAndCloseIgnored()
        {
            // <wave>X</shake> — close 안 맞음 → close 무시, wave는 닫지 않은 채로 끝까지 적용
            var r = DialogueEffectsParser.Parse("<wave>X</shake>");
            Assert.AreEqual("X", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(DialogueEffectKind.Wave, r.Effects[0].Kind);
        }

        [Test]
        public void Parse_UnknownTag_PassedThroughAsLiteral()
        {
            // <b>는 TMP native — 파서가 모르므로 그대로 통과
            var r = DialogueEffectsParser.Parse("Hi <b>bold</b>");
            Assert.AreEqual("Hi <b>bold</b>", r.CleanText);
            Assert.AreEqual(0, r.Effects.Count);
        }

        [Test]
        public void Parse_OurTagMixedWithTmpTag_CleanTextKeepsTmp()
        {
            var r = DialogueEffectsParser.Parse("<shake>A<b>B</b></shake>");
            // <shake>는 제거, <b></b>는 그대로
            Assert.AreEqual("A<b>B</b>", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(0, r.Effects[0].Start);
            Assert.AreEqual(9, r.Effects[0].End, "A<b>B</b> 길이 9 전체에 효과");
        }

        [Test]
        public void Parse_CaseInsensitiveTagNames()
        {
            var r = DialogueEffectsParser.Parse("<SHAKE>x</Shake>");
            Assert.AreEqual("x", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(DialogueEffectKind.Shake, r.Effects[0].Kind);
        }

        [Test]
        public void Parse_LessThanLiteral_NotConsumed()
        {
            // 1 < 2 같은 텍스트는 그대로
            var r = DialogueEffectsParser.Parse("1 < 2");
            Assert.AreEqual("1 < 2", r.CleanText);
        }

        [Test]
        public void Parse_MultipleSeparateInstances_EachRecorded()
        {
            var r = DialogueEffectsParser.Parse("<shake>A</shake> <shake>B</shake> <shake>C</shake>");
            Assert.AreEqual("A B C", r.CleanText);
            Assert.AreEqual(3, r.Effects.Count);
        }

        [Test]
        public void Parse_SameKindNestedSameKind_LIFOMatchesProperly()
        {
            // <shake>A<shake>B</shake>C</shake>  — 안쪽 닫기 → 안쪽 (1,2) 효과,
            //                                       바깥 닫기 → 바깥 (0,3) 효과
            var r = DialogueEffectsParser.Parse("<shake>A<shake>B</shake>C</shake>");
            Assert.AreEqual("ABC", r.CleanText);
            Assert.AreEqual(2, r.Effects.Count);

            // 안쪽 효과는 (1, 2), 바깥은 (0, 3)
            DialogueEffectRange? inner = null, outer = null;
            foreach (var e in r.Effects)
            {
                if (e.Start == 1 && e.End == 2) inner = e;
                if (e.Start == 0 && e.End == 3) outer = e;
            }
            Assert.IsTrue(inner.HasValue, "안쪽 (1,2) shake 누락");
            Assert.IsTrue(outer.HasValue, "바깥 (0,3) shake 누락");
        }

        [Test]
        public void Parse_IntensityInvalid_DefaultsToOne()
        {
            // 숫자 파싱 실패 → 1로 폴백 (관대 정책)
            var r = DialogueEffectsParser.Parse("<shake=abc>X</shake>");
            Assert.AreEqual("X", r.CleanText);
            Assert.AreEqual(1, r.Effects.Count);
            Assert.AreEqual(1f, r.Effects[0].Intensity);
        }

        [Test]
        public void Parse_TagAtEnd_NoEffect_NoCrash()
        {
            // 결말 직전 잘린 태그 — 비정상 입력 안전성
            var r = DialogueEffectsParser.Parse("Hi <shake");
            Assert.AreEqual("Hi <shake", r.CleanText, "잘린 태그는 리터럴로 처리");
            Assert.AreEqual(0, r.Effects.Count);
        }
    }
}
