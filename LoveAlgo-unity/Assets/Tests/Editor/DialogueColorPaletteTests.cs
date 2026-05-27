using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using System.Text.RegularExpressions;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D13 대사 named color 치환 — 순수 함수 테스트.
    /// Resources 로드는 통합 테스트에 양보, 여긴 dict 주입 형태로만.
    /// </summary>
    [TestFixture]
    public class DialogueColorPaletteTests
    {
        static Dictionary<string, Color> Palette(params (string name, Color c)[] entries)
        {
            var d = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var (n, c) in entries) d[n] = c;
            return d;
        }

        [Test]
        public void Apply_PlainText_NotChanged()
        {
            var p = Palette(("roa", Color.red));
            Assert.AreEqual("안녕!", DialogueColorPalette.ApplyNamedColors("안녕!", p));
        }

        [Test]
        public void Apply_NullText_SafeEmpty()
        {
            Assert.AreEqual("", DialogueColorPalette.ApplyNamedColors(null, null));
        }

        [Test]
        public void Apply_EmptyText_SafeEmpty()
        {
            Assert.AreEqual("", DialogueColorPalette.ApplyNamedColors("", Palette()));
        }

        [Test]
        public void Apply_NoColorTag_PassesThrough()
        {
            // <color=...>가 없으면 fast path로 그대로 반환
            var p = Palette(("roa", Color.red));
            Assert.AreEqual("hello <b>world</b>", DialogueColorPalette.ApplyNamedColors("hello <b>world</b>", p));
        }

        [Test]
        public void Apply_NamedColor_ReplacedWithHex()
        {
            var p = Palette(("roa", new Color(1, 0, 0, 1)));
            var r = DialogueColorPalette.ApplyNamedColors("<color=roa>로아</color>", p);
            // close는 그대로, open만 hex로 치환됨
            StringAssert.Contains("<color=#FF0000FF>", r);
            StringAssert.Contains("</color>", r);
            StringAssert.Contains("로아", r);
        }

        [Test]
        public void Apply_CaseInsensitiveName()
        {
            var p = Palette(("ROA", new Color(1, 0, 0, 1)));
            var r = DialogueColorPalette.ApplyNamedColors("<color=roa>X</color>", p);
            StringAssert.Contains("<color=#FF0000FF>", r);
        }

        [Test]
        public void Apply_CaseInsensitiveOpenTag()
        {
            var p = Palette(("roa", new Color(1, 0, 0, 1)));
            var r = DialogueColorPalette.ApplyNamedColors("<COLOR=roa>X</color>", p);
            StringAssert.Contains("<color=#FF0000FF>", r);
        }

        [Test]
        public void Apply_HexColor_PassThroughUnchanged()
        {
            var p = Palette(("roa", Color.red));
            var r = DialogueColorPalette.ApplyNamedColors("<color=#ff00ff>X</color>", p);
            // 입력 그대로 — hex는 치환 안 함
            StringAssert.Contains("<color=#ff00ff>", r);
        }

        [Test]
        public void Apply_UnknownName_PassesThrough_AndWarns()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[DialogueColorPalette\].*'mystery'"));
            var p = Palette(("roa", Color.red));
            var r = DialogueColorPalette.ApplyNamedColors("<color=mystery>X</color>", p);
            // 원본 그대로
            StringAssert.Contains("<color=mystery>", r);
        }

        [Test]
        public void Apply_QuotedValue_StripsQuotes()
        {
            var p = Palette(("roa", new Color(0, 1, 0, 1)));
            var r = DialogueColorPalette.ApplyNamedColors("<color=\"roa\">X</color>", p);
            StringAssert.Contains("<color=#00FF00FF>", r);
        }

        [Test]
        public void Apply_MultipleColors_AllReplaced()
        {
            var p = Palette(
                ("roa", new Color(1, 0, 0, 1)),
                ("daun", new Color(0, 0, 1, 1)));
            var r = DialogueColorPalette.ApplyNamedColors(
                "<color=roa>A</color> <color=daun>B</color>", p);
            StringAssert.Contains("<color=#FF0000FF>", r);
            StringAssert.Contains("<color=#0000FFFF>", r);
        }

        [Test]
        public void Apply_NestedColors_BothNamesReplaced()
        {
            // TMP가 내부 스택으로 닫기 매칭 — 우리는 open만 치환
            var p = Palette(
                ("roa", new Color(1, 0, 0, 1)),
                ("daun", new Color(0, 0, 1, 1)));
            var r = DialogueColorPalette.ApplyNamedColors(
                "<color=roa>A<color=daun>B</color>C</color>", p);
            StringAssert.Contains("<color=#FF0000FF>", r);
            StringAssert.Contains("<color=#0000FFFF>", r);
            Assert.AreEqual(2, Regex.Matches(r, "</color>").Count);
        }

        [Test]
        public void Apply_NullPalette_UnknownNameWarnsAndPassesThrough()
        {
            LogAssert.Expect(LogType.Warning, new Regex(@"\[DialogueColorPalette\]"));
            var r = DialogueColorPalette.ApplyNamedColors("<color=roa>X</color>", null);
            StringAssert.Contains("<color=roa>", r);
        }

        [Test]
        public void Apply_NullPalette_HexStillWorks()
        {
            // 팔레트 없어도 hex는 그대로 (역호환 보장)
            var r = DialogueColorPalette.ApplyNamedColors("<color=#abcdef>X</color>", null);
            Assert.AreEqual("<color=#abcdef>X</color>", r);
        }

        [Test]
        public void Apply_BrokenTag_NoCrash()
        {
            // 닫는 '>' 없는 망가진 입력 — crash 안 하고 원본 비슷하게 반환
            var p = Palette(("roa", Color.red));
            var r = DialogueColorPalette.ApplyNamedColors("Hi <color=roa", p);
            Assert.IsNotNull(r);
            StringAssert.Contains("Hi", r);
        }

        [Test]
        public void Apply_AlphaPreservedInHex()
        {
            var p = Palette(("ghost", new Color(0.5f, 0.5f, 0.5f, 0.5f)));
            var r = DialogueColorPalette.ApplyNamedColors("<color=ghost>X</color>", p);
            // 알파 0.5 → 0x80
            StringAssert.Contains("80", r, "알파 0.5가 hex 80으로 인코딩");
        }
    }
}
