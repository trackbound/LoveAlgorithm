using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Tests
{
    /// <summary>
    /// GameState.EvaluateCondition 테스트 — 실제 GameState MonoBehaviour 사용
    /// </summary>
    public class GameStateConditionTests
    {
        private GameObject go;
        private GameState state;

        [SetUp]
        public void Setup()
        {
            go = new GameObject("TestGameState");
            state = go.AddComponent<GameState>();

            // 스탯 세팅
            state.SetStat("Int", 25);
            state.SetStat("Str", 15);
            state.SetStat("Fatigue", 30);

            // 호감도 세팅
            state.SetLove("Roa", 35);
            state.SetLove("Daeun", 10);

            // 플래그 세팅
            state.SetFlag("Met_Roa", true);
            state.SetFlag("Confessed", false);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        #region 플래그 테스트

        [Test]
        public void EvaluateCondition_FlagTrue_ReturnsTrue()
        {
            Assert.IsTrue(state.EvaluateCondition("Flag:Met_Roa"));
        }

        [Test]
        public void EvaluateCondition_FlagFalse_ReturnsFalse()
        {
            Assert.IsFalse(state.EvaluateCondition("Flag:Confessed"));
        }

        [Test]
        public void EvaluateCondition_NotFlag_ReturnsOpposite()
        {
            Assert.IsFalse(state.EvaluateCondition("!Flag:Met_Roa"));
            Assert.IsTrue(state.EvaluateCondition("!Flag:Confessed"));
        }

        [Test]
        public void EvaluateCondition_UndefinedFlag_ReturnsFalse()
        {
            Assert.IsFalse(state.EvaluateCondition("Flag:NeverDefined"));
        }

        #endregion

        #region 호감도 테스트

        [Test]
        public void EvaluateCondition_LoveGreaterOrEqual_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Love:Roa>=30"));
            Assert.IsTrue(state.EvaluateCondition("Love:Roa>=35"));
        }

        [Test]
        public void EvaluateCondition_LoveGreaterOrEqual_Fail()
        {
            Assert.IsFalse(state.EvaluateCondition("Love:Roa>=40"));
        }

        [Test]
        public void EvaluateCondition_LoveLessThan_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Love:Daeun<20"));
        }

        [Test]
        public void EvaluateCondition_LoveEquals_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Love:Roa==35"));
        }

        [Test]
        public void EvaluateCondition_UnknownCharacter_ReturnsZero()
        {
            Assert.IsTrue(state.EvaluateCondition("Love:Unknown>=0"));
            Assert.IsFalse(state.EvaluateCondition("Love:Unknown>0"));
        }

        #endregion

        #region 스탯 테스트

        [Test]
        public void EvaluateCondition_StatWithPrefix_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Stat:Int>=20"));
            Assert.IsTrue(state.EvaluateCondition("Stat:Int>=25"));
        }

        [Test]
        public void EvaluateCondition_StatDirect_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Int>=20"));
            Assert.IsTrue(state.EvaluateCondition("Fatigue>=30"));
        }

        [Test]
        public void EvaluateCondition_FatigueLessThan_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Fatigue<50"));
        }

        [Test]
        public void EvaluateCondition_StatLessOrEqual_Pass()
        {
            Assert.IsTrue(state.EvaluateCondition("Str<=15"));
            Assert.IsTrue(state.EvaluateCondition("Str<=20"));
        }

        #endregion

        #region 언더스코어 형식 테스트

        [Test]
        public void EvaluateCondition_UnderscoreFormat_NormalizedCorrectly()
        {
            // Love_Roa>5 → Love:Roa>5 로 정규화
            Assert.IsTrue(state.EvaluateCondition("Love_Roa>=30"));
            Assert.IsTrue(state.EvaluateCondition("Stat_Int>=20"));
        }

        #endregion

        #region 엣지 케이스

        [Test]
        public void EvaluateCondition_EmptyString_ReturnsTrue()
        {
            Assert.IsTrue(state.EvaluateCondition(""));
            Assert.IsTrue(state.EvaluateCondition(null));
        }

        [Test]
        public void EvaluateCondition_InvalidFormat_ReturnsFalse()
        {
            Assert.IsFalse(state.EvaluateCondition("InvalidCondition"));
            Assert.IsFalse(state.EvaluateCondition("Love:Roa"));
        }

        #endregion
    }
}
