using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Story;

namespace LoveAlgo.Tests
{
    /// <summary>
    /// GameState 스탯 조작 (GetStat, SetStat, AddStat) 및 클램핑 검증
    /// </summary>
    public class GameStateStatTests
    {
        private GameObject go;
        private GameState state;

        [SetUp]
        public void Setup()
        {
            go = new GameObject("TestGameState");
            state = go.AddComponent<GameState>();
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(go);
        }

        #region GetStat / SetStat 기본

        [Test]
        public void GetStat_Default_ReturnsZero()
        {
            Assert.AreEqual(0, state.GetStat("Str"));
            Assert.AreEqual(0, state.GetStat("Int"));
            Assert.AreEqual(0, state.GetStat("Soc"));
            Assert.AreEqual(0, state.GetStat("Per"));
            Assert.AreEqual(0, state.GetStat("Fatigue"));
        }

        [Test]
        public void SetStat_ThenGetStat_ReturnsValue()
        {
            state.SetStat("Str", 50);
            Assert.AreEqual(50, state.GetStat("Str"));

            state.SetStat("Int", 75);
            Assert.AreEqual(75, state.GetStat("Int"));
        }

        [Test]
        public void GetStat_CaseInsensitive()
        {
            state.SetStat("str", 30);
            Assert.AreEqual(30, state.GetStat("Str"));
            Assert.AreEqual(30, state.GetStat("STR"));
            Assert.AreEqual(30, state.GetStat("strength"));
            Assert.AreEqual(30, state.GetStat("Strength"));
        }

        [Test]
        public void GetStat_UnknownStat_ReturnsZero()
        {
            Assert.AreEqual(0, state.GetStat("UnknownStat"));
        }

        #endregion

        #region 클램핑

        [Test]
        public void SetStat_AboveMax_ClampedToMax()
        {
            state.SetStat("Str", 999);
            Assert.AreEqual(GameConstants.MaxStat, state.GetStat("Str"));
        }

        [Test]
        public void SetStat_BelowZero_ClampedToZero()
        {
            state.SetStat("Int", -10);
            Assert.AreEqual(0, state.GetStat("Int"));
        }

        [Test]
        public void AddStat_Overflow_ClampedToMax()
        {
            state.SetStat("Soc", 90);
            state.AddStat("Soc", 50);  // 90 + 50 = 140 → MaxStat
            Assert.AreEqual(GameConstants.MaxStat, state.GetStat("Soc"));
        }

        [Test]
        public void AddStat_NegativeOverflow_ClampedToZero()
        {
            state.SetStat("Per", 10);
            state.AddStat("Per", -30); // 10 - 30 = -20 → 0
            Assert.AreEqual(0, state.GetStat("Per"));
        }

        [Test]
        public void AddStat_NormalAddition_Works()
        {
            state.SetStat("Fatigue", 20);
            state.AddStat("Fatigue", 15);
            Assert.AreEqual(35, state.GetStat("Fatigue"));
        }

        #endregion

        #region 호감도 / 플래그

        [Test]
        public void LovePoints_DefaultZero()
        {
            Assert.AreEqual(0, state.GetLove("Roa"));
        }

        [Test]
        public void AddLove_Accumulates()
        {
            state.AddLove("Roa", 10);
            state.AddLove("Roa", 5);
            Assert.AreEqual(15, state.GetLove("Roa"));
        }

        [Test]
        public void SetLove_Overwrites()
        {
            state.AddLove("Daeun", 20);
            state.SetLove("Daeun", 5);
            Assert.AreEqual(5, state.GetLove("Daeun"));
        }

        [Test]
        public void Flag_DefaultFalse()
        {
            Assert.IsFalse(state.GetFlag("SomeFlag"));
        }

        [Test]
        public void SetFlag_ThenGetFlag()
        {
            state.SetFlag("Met_Roa", true);
            Assert.IsTrue(state.GetFlag("Met_Roa"));

            state.SetFlag("Met_Roa", false);
            Assert.IsFalse(state.GetFlag("Met_Roa"));
        }

        #endregion

        #region ResetAll

        [Test]
        public void ResetAll_ClearsEverything()
        {
            state.SetStat("Str", 50);
            state.AddLove("Roa", 30);
            state.SetFlag("Met_Roa", true);
            state.SetMoney(10000);

            state.ResetAll();

            Assert.AreEqual(0, state.GetStat("Str"));
            Assert.AreEqual(0, state.GetLove("Roa"));
            Assert.IsFalse(state.GetFlag("Met_Roa"));
            Assert.AreEqual(0, state.Money);
        }

        #endregion
    }
}
