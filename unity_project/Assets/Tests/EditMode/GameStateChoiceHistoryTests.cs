using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core; // GameStateSO, GameStateData

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 선택지 이력(🔴 세이브 스키마): RecordChoice 중복 방지 + HasChosen 조회 + JSON 왕복 + 구세이브 기본값(빈).
    /// </summary>
    [TestFixture]
    public class GameStateChoiceHistoryTests
    {
        [Test]
        public void RecordChoice_DedupsAndHasChosenReads()
        {
            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            gs.ResetRuntime();
            try
            {
                Assert.IsFalse(gs.HasChosen("met_roa"), "초기엔 없음");
                gs.RecordChoice("met_roa");
                gs.RecordChoice("met_roa"); // 중복 — 무시
                gs.RecordChoice("");          // 빈 — 무시
                Assert.IsTrue(gs.HasChosen("met_roa"));
                Assert.AreEqual(1, gs.Data.choiceHistory.Count, "중복/빈 미기록");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void ChoiceHistory_JsonRoundTrip()
        {
            var d = new GameStateData();
            d.choiceHistory.Add("a");
            d.choiceHistory.Add("b");

            var back = JsonUtility.FromJson<GameStateData>(JsonUtility.ToJson(d));

            Assert.AreEqual(2, back.choiceHistory.Count);
            Assert.AreEqual("a", back.choiceHistory[0]);
            Assert.AreEqual("b", back.choiceHistory[1]);
        }

        [Test]
        public void OldSave_WithoutChoiceHistory_LoadsAsEmpty()
        {
            const string oldJson = "{\"playerName\":\"철수\",\"day\":3}";
            var d = JsonUtility.FromJson<GameStateData>(oldJson);
            Assert.IsNotNull(d.choiceHistory, "부재 필드 → 빈 리스트");
            Assert.AreEqual(0, d.choiceHistory.Count);
        }
    }
}
