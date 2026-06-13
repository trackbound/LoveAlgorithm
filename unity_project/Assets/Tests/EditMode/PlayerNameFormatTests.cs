using NUnit.Framework;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>{{Player}} 치환 순수 유틸 — 화자 판별/본문 다중 치환/대소문자/폴백 가드.</summary>
    public class PlayerNameFormatTests
    {
        [Test]
        public void IsPlayerSpeaker_TokenOnly_CaseAndSpaceInsensitive()
        {
            Assert.IsTrue(PlayerNameFormat.IsPlayerSpeaker("{{Player}}"));
            Assert.IsTrue(PlayerNameFormat.IsPlayerSpeaker(" {{player}} "));
            Assert.IsFalse(PlayerNameFormat.IsPlayerSpeaker("로아"));
            Assert.IsFalse(PlayerNameFormat.IsPlayerSpeaker(""));
            Assert.IsFalse(PlayerNameFormat.IsPlayerSpeaker(null));
        }

        [Test]
        public void Apply_ReplacesAllOccurrences_CaseInsensitive()
        {
            Assert.AreEqual("철수... 좋아! 철수라고 불러도 되지?",
                PlayerNameFormat.Apply("{{Player}}... 좋아! {{player}}라고 불러도 되지?", "철수"));
        }

        [Test]
        public void Apply_EmptyName_UsesFallback()
        {
            Assert.AreEqual($"안녕, {PlayerNameFormat.FallbackName}!",
                PlayerNameFormat.Apply("안녕, {{Player}}!", ""));
            Assert.AreEqual($"안녕, {PlayerNameFormat.FallbackName}!",
                PlayerNameFormat.Apply("안녕, {{Player}}!", null));
        }

        [Test]
        public void Apply_NoToken_Unchanged_NullSafe()
        {
            Assert.AreEqual("그냥 대사", PlayerNameFormat.Apply("그냥 대사", "철수"));
            Assert.IsNull(PlayerNameFormat.Apply(null, "철수"));
            Assert.AreEqual("", PlayerNameFormat.Apply("", "철수"));
        }

        [Test]
        public void Apply_TrimsName()
        {
            Assert.AreEqual("안녕, 철수!", PlayerNameFormat.Apply("안녕, {{Player}}!", "  철수  "));
        }
    }
}
