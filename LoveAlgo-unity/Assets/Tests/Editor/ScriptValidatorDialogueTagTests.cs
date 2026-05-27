using System.Collections.Generic;
using NUnit.Framework;
using LoveAlgo.Story;
using LoveAlgo.Story.StoryEngine;
using UnityEngine;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D18 ScriptValidator 통합 — Text/Option/Place 라인의 D9/D13 태그를
    /// inspector에 위임해 Violation으로 변환하는지 확인. Strict 모드 격상 포함.
    /// </summary>
    [TestFixture]
    public class ScriptValidatorDialogueTagTests
    {
        [SetUp]
        public void SetUp()
        {
            ScriptValidator.Strict = false;
            ScriptValidator.ColorPalette = null;
        }

        [TearDown]
        public void TearDown()
        {
            ScriptValidator.Strict = false;
            ScriptValidator.ColorPalette = null;
        }

        static ScriptLine Line(LineType t, string value)
            => new ScriptLine("", t, "", value, NextType.Click);

        [Test]
        public void Text_BalancedTags_NoViolations()
        {
            var lines = new List<ScriptLine> { Line(LineType.Text, "<shake>안녕</shake>") };
            var v = ScriptValidator.Validate(lines);
            Assert.AreEqual(0, v.Count);
        }

        [Test]
        public void Text_UnclosedTag_WarningInNonStrict()
        {
            var lines = new List<ScriptLine> { Line(LineType.Text, "<shake>안녕") };
            var v = ScriptValidator.Validate(lines);
            Assert.AreEqual(1, v.Count);
            Assert.AreEqual("Warning", v[0].Severity);
            StringAssert.Contains("shake", v[0].Message);
        }

        [Test]
        public void Text_UnclosedTag_ErrorInStrict()
        {
            ScriptValidator.Strict = true;
            var lines = new List<ScriptLine> { Line(LineType.Text, "<shake>안녕") };
            var v = ScriptValidator.Validate(lines);
            Assert.AreEqual(1, v.Count);
            Assert.AreEqual("Error", v[0].Severity, "Strict 모드면 Warning이 Error로 격상");
        }

        [Test]
        public void Option_ButtonTextOnly_Checked()
        {
            // Option Value 형식: "텍스트|점프대상|효과|if:조건"
            // 텍스트(|앞)만 태그 검증 대상 — 점프/효과/조건엔 inspector 안 돔
            var lines = new List<ScriptLine>
            {
                Line(LineType.Option, "<wave>중요한 선택|next|Love:Roa:5"),
            };
            var v = ScriptValidator.Validate(lines);
            // <wave> 안 닫음 → 1건
            Assert.AreEqual(1, v.Count);
            Assert.AreEqual("Warning", v[0].Severity);
            StringAssert.Contains("wave", v[0].Message);
        }

        [Test]
        public void Place_BadIntensity_Warning()
        {
            var lines = new List<ScriptLine> { Line(LineType.Place, "<shake=abc>장소</shake>") };
            var v = ScriptValidator.Validate(lines);
            Assert.AreEqual(1, v.Count);
            StringAssert.Contains("BadIntensity", v[0].Message);
        }

        [Test]
        public void ColorPalette_KnownName_NoViolation()
        {
            ScriptValidator.ColorPalette = new Dictionary<string, Color>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "roa", Color.red }
            };
            var lines = new List<ScriptLine> { Line(LineType.Text, "<color=roa>대사</color>") };
            var v = ScriptValidator.Validate(lines);
            Assert.AreEqual(0, v.Count);
        }

        [Test]
        public void ColorPalette_UnknownName_Warning()
        {
            ScriptValidator.ColorPalette = new Dictionary<string, Color>
            {
                { "roa", Color.red }
            };
            var lines = new List<ScriptLine> { Line(LineType.Text, "<color=mystery>대사</color>") };
            var v = ScriptValidator.Validate(lines);
            Assert.AreEqual(1, v.Count);
            StringAssert.Contains("UnknownColor", v[0].Message);
        }

        [Test]
        public void ColorPalette_HexAlwaysOK()
        {
            // palette 있어도 hex는 검증 안 함
            ScriptValidator.ColorPalette = new Dictionary<string, Color> { { "roa", Color.red } };
            var lines = new List<ScriptLine> { Line(LineType.Text, "<color=#abcdef>X</color>") };
            var v = ScriptValidator.Validate(lines);
            Assert.AreEqual(0, v.Count);
        }

        [Test]
        public void FX_RegressionCheck_StillValidates()
        {
            // D18 추가가 기존 FX 검증을 깨지 않았는지 확인
            var lines = new List<ScriptLine> { Line(LineType.FX, "UnknownFX:abc") };
            var v = ScriptValidator.Validate(lines);
            Assert.AreEqual(1, v.Count);
            Assert.AreEqual("Error", v[0].Severity);
        }

        [Test]
        public void NonDialogueLine_NoTagInspection()
        {
            // BG/Sound/CG 등은 태그 inspector 대상 아님 (Value에 '<' 있어도)
            var lines = new List<ScriptLine> { Line(LineType.Sound, "<some>weird</wrong>") };
            var v = ScriptValidator.Validate(lines);
            // 태그 inspector 위반 0건 (Sound는 다른 검증도 없음)
            Assert.AreEqual(0, v.Count);
        }
    }
}
