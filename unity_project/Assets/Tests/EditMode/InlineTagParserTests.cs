using NUnit.Framework;
using LoveAlgo.Story; // InlineTagParser

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M3 슬라이스2 검증: 순수 <see cref="InlineTagParser"/>. 대사 본문의 <c>&lt;wait:sec&gt;</c>를 표시 텍스트
    /// (태그 제거)와 멈춤 지점(글자 인덱스)으로 분해하는지. 기타 태그 제거·무효 인자 무시·미완성 태그 리터럴 처리.
    /// </summary>
    [TestFixture]
    public class InlineTagParserTests
    {
        [Test]
        public void NoTags_Returns_Raw_NoPauses()
        {
            var p = InlineTagParser.Parse("안녕하세요");
            Assert.AreEqual("안녕하세요", p.Text);
            Assert.IsNull(p.Pauses);
        }

        [Test]
        public void Wait_Strips_Tag_And_Records_Pause_At_CharIndex()
        {
            var p = InlineTagParser.Parse("안녕<wait:0.5>반가워");
            Assert.AreEqual("안녕반가워", p.Text);
            Assert.AreEqual(1, p.Pauses.Count);
            Assert.AreEqual(2, p.Pauses[0].CharIndex); // "안녕" 2글자 직후
            Assert.AreEqual(0.5f, p.Pauses[0].Seconds, 1e-4f);
        }

        [Test]
        public void Multiple_Waits_Ordered()
        {
            var p = InlineTagParser.Parse("a<wait:1>b<wait:2>c");
            Assert.AreEqual("abc", p.Text);
            Assert.AreEqual(2, p.Pauses.Count);
            Assert.AreEqual(1, p.Pauses[0].CharIndex);
            Assert.AreEqual(1f, p.Pauses[0].Seconds, 1e-4f);
            Assert.AreEqual(2, p.Pauses[1].CharIndex);
            Assert.AreEqual(2f, p.Pauses[1].Seconds, 1e-4f);
        }

        [Test]
        public void Wait_At_Start()
        {
            var p = InlineTagParser.Parse("<wait:0.3>안녕");
            Assert.AreEqual("안녕", p.Text);
            Assert.AreEqual(0, p.Pauses[0].CharIndex);
        }

        [Test]
        public void Colon_Form_Emote_Is_Not_Recognized_Stripped()
        {
            // 비정규 콜론형 <emote:x>는 emote로 보지 않고 제거만(정규형은 '=' + 꼬리 '/').
            var p = InlineTagParser.Parse("어<emote:happy>이");
            Assert.AreEqual("어이", p.Text);
            Assert.IsNull(p.Pauses);
            Assert.IsNull(p.Emotes);
        }

        [Test]
        public void Emote_Strips_Tag_And_Records_At_CharIndex()
        {
            var p = InlineTagParser.Parse("로아<emote=활짝웃음/>안녕");
            Assert.AreEqual("로아안녕", p.Text);
            Assert.IsNotNull(p.Emotes);
            Assert.AreEqual(1, p.Emotes.Count);
            Assert.AreEqual(2, p.Emotes[0].CharIndex); // "로아" 2글자 직후
            Assert.AreEqual("활짝웃음", p.Emotes[0].Emote);
            Assert.IsNull(p.Pauses);
        }

        [Test]
        public void Emote_At_Start()
        {
            var p = InlineTagParser.Parse("<emote=BrightSmile/>나야?");
            Assert.AreEqual("나야?", p.Text);
            Assert.AreEqual(0, p.Emotes[0].CharIndex);
            Assert.AreEqual("BrightSmile", p.Emotes[0].Emote);
        }

        [Test]
        public void Emote_Without_Trailing_Slash_Also_Works()
        {
            var p = InlineTagParser.Parse("<emote=Happy>야");
            Assert.AreEqual("야", p.Text);
            Assert.AreEqual("Happy", p.Emotes[0].Emote);
        }

        [Test]
        public void Emote_Empty_Value_Ignored()
        {
            var p = InlineTagParser.Parse("a<emote=/>b");
            Assert.AreEqual("ab", p.Text);
            Assert.IsNull(p.Emotes);
        }

        [Test]
        public void Wait_And_Emote_Mixed_Independently()
        {
            var p = InlineTagParser.Parse("a<emote=X/>b<wait:0.5>c");
            Assert.AreEqual("abc", p.Text);
            Assert.AreEqual(1, p.Emotes.Count);
            Assert.AreEqual(1, p.Emotes[0].CharIndex);
            Assert.AreEqual("X", p.Emotes[0].Emote);
            Assert.AreEqual(1, p.Pauses.Count);
            Assert.AreEqual(2, p.Pauses[0].CharIndex);
        }

        [Test]
        public void Wait_Without_Or_Invalid_Arg_Ignored()
        {
            Assert.IsNull(InlineTagParser.Parse("a<wait>b").Pauses);
            Assert.AreEqual("ab", InlineTagParser.Parse("a<wait>b").Text);
            Assert.IsNull(InlineTagParser.Parse("a<wait:0>b").Pauses);
            Assert.IsNull(InlineTagParser.Parse("a<wait:-1>b").Pauses);
        }

        [Test]
        public void Unclosed_Bracket_Is_Literal()
        {
            var p = InlineTagParser.Parse("a<b");
            Assert.AreEqual("a<b", p.Text);
            Assert.IsNull(p.Pauses);
        }

        [Test]
        public void Empty_And_Null()
        {
            Assert.AreEqual("", InlineTagParser.Parse("").Text);
            Assert.IsNull(InlineTagParser.Parse("").Pauses);
            Assert.AreEqual("", InlineTagParser.Parse(null).Text);
        }

        // --- Task 5: 단일토큰 <웃음> → emote ---

        [Test]
        public void Bare_Token_Is_Emote()
        {
            var p = InlineTagParser.Parse("<웃음>안녕");
            Assert.AreEqual("안녕", p.Text);
            Assert.IsNotNull(p.Emotes);
            Assert.AreEqual(1, p.Emotes.Count);
            Assert.AreEqual(0, p.Emotes[0].CharIndex);
            Assert.AreEqual("웃음", p.Emotes[0].Emote);
        }

        [Test]
        public void Emote_Equals_Form_Still_Works()
        {
            var p = InlineTagParser.Parse("<emote=활짝/>야");
            Assert.AreEqual("야", p.Text);
            Assert.AreEqual("활짝", p.Emotes[0].Emote);
        }

        [Test]
        public void Wait_Tag_Not_Treated_As_Emote()
        {
            var p = InlineTagParser.Parse("<wait:1>음");
            Assert.AreEqual("음", p.Text);
            Assert.IsNull(p.Emotes);     // wait는 emote 아님
            Assert.IsNotNull(p.Pauses);
        }

        // ── 통일 문법(<이름=값/>) + sfx + 지정 표정 ──

        [Test]
        public void Wait_Equals_Form_With_Slash_Records_Pause()
        {
            // 통일 문법: <wait=초/> (콜론형과 동등하게 멈춤 기록).
            var p = InlineTagParser.Parse("a<wait=2.5/>b");
            Assert.AreEqual("ab", p.Text);
            Assert.IsNotNull(p.Pauses);
            Assert.AreEqual(1, p.Pauses[0].CharIndex);
            Assert.AreEqual(2.5f, p.Pauses[0].Seconds, 1e-4f);
            Assert.IsNull(p.Emotes); // wait는 emote 아님
        }

        [Test]
        public void Sfx_Records_At_CharIndex_With_And_Without_Slash()
        {
            var p = InlineTagParser.Parse("야<sfx=060_message/>호<sfx=200_pen>");
            Assert.AreEqual("야호", p.Text);
            Assert.IsNotNull(p.Sfx);
            Assert.AreEqual(2, p.Sfx.Count);
            Assert.AreEqual(1, p.Sfx[0].CharIndex);
            Assert.AreEqual("060_message", p.Sfx[0].Name);
            Assert.AreEqual(2, p.Sfx[1].CharIndex);
            Assert.AreEqual("200_pen", p.Sfx[1].Name);
        }

        [Test]
        public void Emote_Plain_Has_Null_Target()
        {
            var p = InlineTagParser.Parse("<emote=기본/>야");
            Assert.AreEqual("기본", p.Emotes[0].Emote);
            Assert.IsNull(p.Emotes[0].Target); // 대상 미지정 → 그 줄 화자
        }

        [Test]
        public void Emote_Targeted_Splits_Target_And_Emote()
        {
            // 지정형 <emote=대상:표정/> — 내레이션 줄에서도 특정 캐릭터 표정 변경.
            var p = InlineTagParser.Parse("그녀는 <emote=서다은:눈웃음/>웃었다");
            Assert.AreEqual("그녀는 웃었다", p.Text);
            Assert.AreEqual(1, p.Emotes.Count);
            Assert.AreEqual("서다은", p.Emotes[0].Target);
            Assert.AreEqual("눈웃음", p.Emotes[0].Emote);
        }

        [Test]
        public void Unknown_Equals_Tag_Is_Stripped()
        {
            // 미지원 이름(예: TMP <color=..>)은 표시 텍스트에서 제거만, 작용 없음.
            var p = InlineTagParser.Parse("a<color=red/>b");
            Assert.AreEqual("ab", p.Text);
            Assert.IsNull(p.Emotes);
            Assert.IsNull(p.Sfx);
            Assert.IsNull(p.Pauses);
        }
    }
}
