using NUnit.Framework;
using System.Collections.Generic;
using LoveAlgo.Story.StoryEngine; // NarrativeController
using LoveAlgo.Core;              // GameStateData

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// Task4 순수 헬퍼 검증: storyChars에서 id(대소문자 무시)로 슬롯을 찾는다.
    /// 식별자/직전화자 Emote 라우팅이 대상 캐릭터의 현재 슬롯을 정확히 조회하는지(없으면 -1) 보장.
    /// </summary>
    [TestFixture]
    public class StoryCharSlotLookupTests
    {
        static List<GameStateData.StoryCharRecord> Sample() => new()
        {
            new GameStateData.StoryCharRecord { slot = 1, id = "Roa", emote = "기본" },
            new GameStateData.StoryCharRecord { slot = 0, id = "Yeeun", emote = "기본" },
        };

        [Test]
        public void Finds_Slot_By_Id_CaseInsensitive()
        {
            Assert.AreEqual(1, NarrativeController.FindSlotForCharId(Sample(), "roa"));
            Assert.AreEqual(0, NarrativeController.FindSlotForCharId(Sample(), "Yeeun"));
        }

        [Test]
        public void Returns_Minus1_When_Not_On_Stage()
        {
            Assert.AreEqual(-1, NarrativeController.FindSlotForCharId(Sample(), "Bom"));
            Assert.AreEqual(-1, NarrativeController.FindSlotForCharId(Sample(), null));
        }
    }
}
