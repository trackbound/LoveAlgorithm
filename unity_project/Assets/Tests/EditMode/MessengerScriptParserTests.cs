using NUnit.Framework;
using LoveAlgo.Messenger;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 메신저 시퀀스 CSV 순수 파서 검증 — 행 3종(Msg/Me/Option), 연속 Option 그룹핑,
    /// 스토리 CSV와 공유하는 관례(#주석/빈 줄/헤더 스킵, 따옴표 쉼표, \n 치환), 오류 수집.
    /// </summary>
    public class MessengerScriptParserTests
    {
        [Test]
        public void Parses_Msg_Me_And_Skips_Header_Comment_Empty()
        {
            const string csv =
                "Type,Speaker,Value\n" +
                "# 데모 시퀀스\n" +
                "\n" +
                "Msg,로아,안녕! 내일 시간 있어?\n" +
                "Me,,그럼 내일 봐!";

            var result = MessengerScriptParser.Parse(csv);

            Assert.IsFalse(result.HasErrors, string.Join(" / ", result.Errors));
            Assert.AreEqual(2, result.Lines.Count);
            Assert.AreEqual(MessengerLineKind.Message, result.Lines[0].Kind);
            Assert.AreEqual("로아", result.Lines[0].SenderId);
            Assert.AreEqual("안녕! 내일 시간 있어?", result.Lines[0].Text);
            Assert.AreEqual(MessengerLineKind.MyMessage, result.Lines[1].Kind);
            Assert.AreEqual("그럼 내일 봐!", result.Lines[1].Text);
        }

        [Test]
        public void Consecutive_Options_Group_Into_One_Choice()
        {
            const string csv =
                "Msg,로아,같이 갈래?\n" +
                "Option,,있지! 같이 가자|Love:로아:1\n" +
                "Option,,글쎄… 생각해 볼게\n" +
                "Me,,이따 연락할게";

            var result = MessengerScriptParser.Parse(csv);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual(3, result.Lines.Count);
            var choice = result.Lines[1];
            Assert.AreEqual(MessengerLineKind.Choice, choice.Kind);
            Assert.AreEqual(2, choice.Options.Count);
            Assert.AreEqual("있지! 같이 가자", choice.Options[0].Text);
            CollectionAssert.AreEqual(new[] { "Love:로아:1" }, choice.Options[0].Effects);
            Assert.AreEqual(0, choice.Options[1].Effects.Count);
        }

        [Test]
        public void Options_Separated_By_Message_Form_Two_Groups()
        {
            const string csv =
                "Option,,첫 그룹\n" +
                "Msg,로아,중간 메시지\n" +
                "Option,,둘째 그룹";

            var result = MessengerScriptParser.Parse(csv);

            Assert.AreEqual(3, result.Lines.Count);
            Assert.AreEqual(MessengerLineKind.Choice, result.Lines[0].Kind);
            Assert.AreEqual(MessengerLineKind.Choice, result.Lines[2].Kind);
            Assert.AreEqual(1, result.Lines[0].Options.Count);
            Assert.AreEqual(1, result.Lines[2].Options.Count);
        }

        [Test]
        public void Quoted_Comma_Text_And_Newline_Literal()
        {
            const string csv =
                "Msg,로아,\"안녕, 반가워!\\n잘 지냈어?\"";

            var result = MessengerScriptParser.Parse(csv);

            Assert.IsFalse(result.HasErrors);
            Assert.AreEqual("안녕, 반가워!\n잘 지냈어?", result.Lines[0].Text);
        }

        [Test]
        public void Option_Cell_Parses_Effects_And_Condition()
        {
            var option = MessengerScriptParser.ParseOption("보러 가자|Love:로아:1|Flag:Promised|if:Flag:MetRoa");

            Assert.AreEqual("보러 가자", option.Text);
            CollectionAssert.AreEqual(new[] { "Love:로아:1", "Flag:Promised" }, option.Effects);
            Assert.AreEqual("Flag:MetRoa", option.Condition);
        }

        [Test]
        public void Collects_Errors_For_Invalid_Rows()
        {
            const string csv =
                "Msg,,발신자 없음\n" +      // Msg 발신자 누락
                "Me,,\n" +                  // Me 텍스트 누락
                "Option,,\n" +              // Option 텍스트 누락
                "Jump,,어디로\n" +          // 알 수 없는 Type
                "한칸뿐";                   // 컬럼 부족

            var result = MessengerScriptParser.Parse(csv);

            Assert.AreEqual(0, result.Lines.Count);
            Assert.AreEqual(5, result.Errors.Count);
        }

        [Test]
        public void Empty_Csv_Returns_Empty_Result()
        {
            Assert.AreEqual(0, MessengerScriptParser.Parse("").Lines.Count);
            Assert.AreEqual(0, MessengerScriptParser.Parse(null).Lines.Count);
        }
    }
}
