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

        [Test]
        public void Parse_CGLine_ParsesCorrectly()
        {
            var csv = ",CG,,CG/Roa_FirstMeet:Fade:1.0,await";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.CG, result[0].Type);
            Assert.AreEqual("CG/Roa_FirstMeet:Fade:1.0", result[0].Value);
            Assert.AreEqual(NextType.Await, result[0].NextType);
        }

        [Test]
        public void Parse_CGExit_ParsesCorrectly()
        {
            var csv = ",CG,,Exit,>";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.CG, result[0].Type);
            Assert.AreEqual("Exit", result[0].Value);
        }

        [Test]
        public void Parse_OverlayLine_ParsesCorrectly()
        {
            var csv = ",Overlay,,Roa_Theme:FadeIn:0.5,await";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.Overlay, result[0].Type);
            Assert.AreEqual("Roa_Theme:FadeIn:0.5", result[0].Value);
        }

        [Test]
        public void Parse_OverlayFadeOut_ParsesCorrectly()
        {
            var csv = ",Overlay,,FadeOut,await";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.Overlay, result[0].Type);
            Assert.AreEqual("FadeOut", result[0].Value);
        }

        [Test]
        public void Parse_FX_CamShake_ParsesCorrectly()
        {
            var csv = ",FX,,CamShake:0.5,await";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.FX, result[0].Type);
            Assert.AreEqual("CamShake:0.5", result[0].Value);
            Assert.AreEqual(NextType.Await, result[0].NextType);
        }

        [Test]
        public void Parse_ComplexScene_AllTypesParsed()
        {
            var csv = @",BG,,School_Day:Fade:1.5,await
,Sound,,BGM:Morning,>
,Char,,C:Enter:Roa:Happy,await
,CG,,CG/Roa_FirstMeet:Fade:1.0,await
,Text,로아,안녕!,click
,Overlay,,Roa_Theme:FadeIn:0.5,await
,FX,,FadeOut:1.0,await
,Flow,,End,>";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(8, result.Count);
            Assert.AreEqual(LineType.BG, result[0].Type);
            Assert.AreEqual(LineType.Sound, result[1].Type);
            Assert.AreEqual(LineType.Char, result[2].Type);
            Assert.AreEqual(LineType.CG, result[3].Type);
            Assert.AreEqual(LineType.Text, result[4].Type);
            Assert.AreEqual(LineType.Overlay, result[5].Type);
            Assert.AreEqual(LineType.FX, result[6].Type);
            Assert.AreEqual(LineType.Flow, result[7].Type);
        }

        #region Strict Mode Tests

        [Test]
        public void Parse_MissingNextColumn_ReturnsNull()
        {
            // 4컬럼만 있으면 (Next 컬럼 누락) 파싱 실패
            var csv = ",Text,로아,안녕!";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(0, result.Count);
        }

        [Test]
        public void Parse_EmptyNext_TextLine_FallsBackToImmediate()
        {
            // 빈 Next는 Immediate로 대체 (LogError 발생하지만 파싱은 됨)
            var csv = ",Text,로아,안녕!,";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(NextType.Immediate, result[0].NextType);
        }

        [Test]
        public void Parse_EmptyNext_OptionLine_AllowedSilently()
        {
            // Option은 빈 Next 허용 (Choice에서 수집하므로)
            var csv = ",Option,,선택|target,";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual(LineType.Option, result[0].Type);
        }

        [Test]
        public void Parse_ExplicitNext_AlwaysRespected()
        {
            // 모든 타입에서 명시적 Next가 정확히 반영되는지 확인
            var csv = @",Text,로아,안녕!,click
,BG,,School:Cut,>
,Char,,C:Enter:Roa,await
,Sound,,BGM:Morning,1.5
,FX,,FadeOut,await";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(5, result.Count);
            Assert.AreEqual(NextType.Click, result[0].NextType);
            Assert.AreEqual(NextType.Immediate, result[1].NextType);
            Assert.AreEqual(NextType.Await, result[2].NextType);
            Assert.AreEqual(NextType.Delay, result[3].NextType);
            Assert.AreEqual(1.5f, result[3].DelaySeconds, 0.01f);
            Assert.AreEqual(NextType.Await, result[4].NextType);
        }

        [Test]
        public void Parse_BGWithoutTransitionType_StillParses()
        {
            // BG 전환 타입 생략 시에도 파싱은 됨 (LogWarning 발생)
            var csv = ",BG,,BG_MyRoom,await";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("BG_MyRoom", result[0].Value);
        }

        [Test]
        public void Parse_BGWithTransitionType_ParsesCorrectly()
        {
            var csv = ",BG,,BG_MyRoom:Fade:1.5,await";
            var result = ScriptParser.Parse(csv);

            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("BG_MyRoom:Fade:1.5", result[0].Value);
            Assert.AreEqual(NextType.Await, result[0].NextType);
        }

        #endregion
    }
}
