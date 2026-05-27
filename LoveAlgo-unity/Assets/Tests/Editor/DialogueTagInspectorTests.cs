using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D18 대사 태그 inspector — D9/D13 태그의 균형/intensity/color name 검증.
    /// inspector는 관대 파서와 분리된 "엄격" 시점 — 작가에게 의도 어긋남을 surface.
    /// </summary>
    [TestFixture]
    public class DialogueTagInspectorTests
    {
        static Dictionary<string, Color> Palette(params string[] names)
        {
            var d = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase);
            foreach (var n in names) d[n] = Color.white;
            return d;
        }

        // ── 깨끗한 케이스 (issue 0건) ────────────────────────────

        [Test]
        public void Inspect_PlainText_NoIssues()
        {
            Assert.AreEqual(0, DialogueTagInspector.Inspect("안녕!", null).Count);
        }

        [Test]
        public void Inspect_Empty_NoIssues()
        {
            Assert.AreEqual(0, DialogueTagInspector.Inspect("", null).Count);
            Assert.AreEqual(0, DialogueTagInspector.Inspect(null, null).Count);
        }

        [Test]
        public void Inspect_BalancedShake_NoIssues()
        {
            Assert.AreEqual(0, DialogueTagInspector.Inspect("<shake>X</shake>", null).Count);
        }

        [Test]
        public void Inspect_BalancedNested_NoIssues()
        {
            // <wave><shake>X</shake></wave> — 모두 균형
            Assert.AreEqual(0, DialogueTagInspector.Inspect("<wave><shake>X</shake></wave>", null).Count);
        }

        [Test]
        public void Inspect_IntensityValid_NoIssues()
        {
            Assert.AreEqual(0, DialogueTagInspector.Inspect("<shake=2>X</shake>", null).Count);
            Assert.AreEqual(0, DialogueTagInspector.Inspect("<wave=1.5>X</wave>", null).Count);
            Assert.AreEqual(0, DialogueTagInspector.Inspect("<emph>X</emph>", null).Count);
        }

        [Test]
        public void Inspect_HexColor_AlwaysOK()
        {
            // palette 없어도 hex는 통과
            Assert.AreEqual(0, DialogueTagInspector.Inspect("<color=#ff00ff>X</color>", null).Count);
        }

        [Test]
        public void Inspect_NamedColorInPalette_NoIssues()
        {
            var p = Palette("roa");
            Assert.AreEqual(0, DialogueTagInspector.Inspect("<color=roa>X</color>", p).Count);
        }

        [Test]
        public void Inspect_TmpNativeTags_PassThrough()
        {
            // <b>, <i> 등은 우리가 모름 → 통과 (균형 stack 안 추적)
            Assert.AreEqual(0, DialogueTagInspector.Inspect("<b>bold</b> <i>italic</i>", null).Count);
        }

        // ── 미스매칭 open (open > close) ─────────────────────────

        [Test]
        public void Inspect_UnclosedShake_OneOpenIssue()
        {
            var r = DialogueTagInspector.Inspect("<shake>Hi", null);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(DialogueTagInspector.IssueKind.UnbalancedOpen, r[0].Kind);
            Assert.AreEqual("shake", r[0].TagName);
        }

        [Test]
        public void Inspect_TwoUnclosed_OneIssueWithCount()
        {
            // <shake>A<shake>B (둘 다 안 닫음) — 한 issue, "2개" 메시지
            var r = DialogueTagInspector.Inspect("<shake>A<shake>B", null);
            Assert.AreEqual(1, r.Count);
            StringAssert.Contains("2개", r[0].Detail);
        }

        // ── 미스매칭 close (close > open) ────────────────────────

        [Test]
        public void Inspect_OrphanClose_CloseIssue()
        {
            var r = DialogueTagInspector.Inspect("Hi</shake>", null);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(DialogueTagInspector.IssueKind.UnbalancedClose, r[0].Kind);
            Assert.AreEqual("shake", r[0].TagName);
        }

        [Test]
        public void Inspect_MismatchedKind_TwoIssues()
        {
            // <wave>X</shake> — orphan close(shake) + 닫지 않은 open(wave)
            var r = DialogueTagInspector.Inspect("<wave>X</shake>", null);
            Assert.AreEqual(2, r.Count);
            bool hasClose = false, hasOpen = false;
            foreach (var iss in r)
            {
                if (iss.Kind == DialogueTagInspector.IssueKind.UnbalancedClose && iss.TagName == "shake") hasClose = true;
                if (iss.Kind == DialogueTagInspector.IssueKind.UnbalancedOpen && iss.TagName == "wave") hasOpen = true;
            }
            Assert.IsTrue(hasClose);
            Assert.IsTrue(hasOpen);
        }

        // ── intensity ────────────────────────────────────────────

        [Test]
        public void Inspect_BadIntensity_NotANumber_FlagBadIntensity()
        {
            var r = DialogueTagInspector.Inspect("<shake=foo>X</shake>", null);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(DialogueTagInspector.IssueKind.BadIntensity, r[0].Kind);
            Assert.AreEqual("shake", r[0].TagName);
        }

        [Test]
        public void Inspect_NegativeIntensity_FlagBadIntensity()
        {
            var r = DialogueTagInspector.Inspect("<shake=-1>X</shake>", null);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(DialogueTagInspector.IssueKind.BadIntensity, r[0].Kind);
        }

        // ── color ────────────────────────────────────────────────

        [Test]
        public void Inspect_UnknownColorName_WithPalette_FlagUnknown()
        {
            var p = Palette("roa");
            var r = DialogueTagInspector.Inspect("<color=mystery>X</color>", p);
            Assert.AreEqual(1, r.Count);
            Assert.AreEqual(DialogueTagInspector.IssueKind.UnknownColor, r[0].Kind);
            StringAssert.Contains("mystery", r[0].Detail);
        }

        [Test]
        public void Inspect_UnknownColorName_NoPalette_NoIssue()
        {
            // palette null → color name 검증 스킵 (역호환 — SO 없는 프로젝트)
            var r = DialogueTagInspector.Inspect("<color=mystery>X</color>", null);
            Assert.AreEqual(0, r.Count);
        }

        [Test]
        public void Inspect_QuotedNamedColor_StripsBeforeLookup()
        {
            // <color="roa"> 도 인식
            var p = Palette("roa");
            var r = DialogueTagInspector.Inspect("<color=\"roa\">X</color>", p);
            Assert.AreEqual(0, r.Count);
        }

        // ── 복합 시나리오 ────────────────────────────────────────

        [Test]
        public void Inspect_MultipleIssues_AllReported()
        {
            var p = Palette("roa");
            var r = DialogueTagInspector.Inspect(
                "<shake=foo>A</shake><color=mystery>B</color><wave>C", p);
            // BadIntensity(shake) + UnknownColor(mystery) + UnbalancedOpen(wave)
            Assert.AreEqual(3, r.Count);
        }

        [Test]
        public void Inspect_AdjacentSameKind_BalanceCorrectly()
        {
            // <shake>A</shake><shake>B</shake> — 둘 다 균형
            var r = DialogueTagInspector.Inspect("<shake>A</shake><shake>B</shake>", null);
            Assert.AreEqual(0, r.Count);
        }
    }
}
