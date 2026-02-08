using NUnit.Framework;
using LoveAlgo.Story;

namespace LoveAlgo.Tests
{
    /// <summary>
    /// ScriptParser 단위 테스트
    /// </summary>
    public class ScriptParserTests
    {
        [Test]
        public void Parse_EmptyString_ReturnsEmptyList()
        {
            var result = ScriptParser.Parse("");
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Parse_CommentLine_IsSkipped()
        {
            var csv = "# 이것은 주석입니다\n,Text,로아,안녕!,click";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.Text, result[0].Type);
        }

        [Test]
        public void Parse_HeaderLine_IsSkipped()
        {
            var csv = "LineID,Type,Speaker,Value,Next\n,Text,로아,안녕!,click";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
        }

        [Test]
        public void Parse_TextLine_ParsesCorrectly()
        {
            var csv = ",Text,로아,안녕하세요!,click";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.Text, result[0].Type);
            Assert.AreEqual("로아", result[0].Speaker);
            Assert.AreEqual("안녕하세요!", result[0].Value);
            Assert.AreEqual(NextType.Click, result[0].NextType);
        }

        [Test]
        public void Parse_CharLine_ParsesCorrectly()
        {
            var csv = ",Char,,C:Enter:Roa:Happy,>";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.Char, result[0].Type);
            Assert.AreEqual("C:Enter:Roa:Happy", result[0].Value);
            Assert.AreEqual(NextType.Immediate, result[0].NextType);
        }

        [Test]
        public void Parse_BGLine_ParsesCorrectly()
        {
            var csv = ",BG,,School_Day:Fade:1.5,await";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.BG, result[0].Type);
            Assert.AreEqual("School_Day:Fade:1.5", result[0].Value);
            Assert.AreEqual(NextType.Await, result[0].NextType);
        }

        [Test]
        public void Parse_FlowJump_ParsesCorrectly()
        {
            var csv = ",Flow,,Jump:Morning_End,>";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.Flow, result[0].Type);
            Assert.AreEqual("Jump:Morning_End", result[0].Value);
        }

        [Test]
        public void Parse_ChoiceWithOptions_ParsesCorrectly()
        {
            var csv = @",Choice,,,click
,Option,,공부하자|Study|Stat:Int:1,
,Option,,놀러가자|Play|Love:Roa:2,";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(3, result.Count);
            Assert.AreEqual(LineType.Choice, result[0].Type);
            Assert.AreEqual(LineType.Option, result[1].Type);
            Assert.AreEqual(LineType.Option, result[2].Type);
        }

        [Test]
        public void Parse_LineIdAnchor_ParsesCorrectly()
        {
            var csv = "Morning_Start,BG,,MyRoom_Day,>";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("Morning_Start", result[0].LineID);
        }

        [Test]
        public void Parse_DelayNext_ParsesCorrectly()
        {
            var csv = ",Text,,잠시 후...,1.5";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(NextType.Delay, result[0].NextType);
            Assert.AreEqual(1.5f, result[0].DelaySeconds, 0.01f);
        }

        [Test]
        public void Parse_MultipleLines_ParsesAll()
        {
            var csv = @",BG,,School_Day,>
,Char,,C:Enter:Roa,await
,Text,로아,안녕!,click
,Text,로아,오늘 뭐해?,click
,Flow,,End,>";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(5, result.Count);
        }

        [Test]
        public void Parse_SoundLine_ParsesCorrectly()
        {
            var csv = ",Sound,,BGM:Morning,>";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.Sound, result[0].Type);
            Assert.AreEqual("BGM:Morning", result[0].Value);
        }

        [Test]
        public void Parse_FXLine_ParsesCorrectly()
        {
            var csv = ",FX,,FadeOut:1.0,await";
            var result = ScriptParser.Parse(csv);
            
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.FX, result[0].Type);
            Assert.AreEqual("FadeOut:1.0", result[0].Value);
        }
    }
}
