using NUnit.Framework;
using UnityEngine;
using LoveAlgo;          // GameConstants
using LoveAlgo.Core;     // GameStateSO
using LoveAlgo.Affinity; // AffinityFormula
using LoveAlgo.Game;     // GameBoot

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 부팅 와이어링 검증: 순수 <see cref="GameBoot.NewGame"/> — 상태 리셋 + 호감도 정의표 주입 +
    /// 데이루프 시작(1일차·행동 풀충전). balance=null이면 AffinityFormula 폴백.
    /// </summary>
    [TestFixture]
    public class GameBootTests
    {
        [Test]
        public void NewGame_Resets_State_And_Begins_Day1()
        {
            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            try
            {
                gs.ResetRuntime();
                // 더럽힌 상태
                gs.Day = 7;
                gs.Money = 99999;
                gs.SetStat("Str", 33);

                GameBoot.NewGame(gs, null);

                Assert.AreEqual(1, gs.Day, "1일차");
                Assert.AreEqual(GameConstants.ActionsPerDay, gs.RemainingActions, "행동 풀충전");
                Assert.AreEqual(0, gs.Money, "ResetRuntime으로 소지금 0");
                Assert.AreEqual(0, gs.GetStat("Str"), "ResetRuntime으로 스탯 0");
                Assert.Greater(AffinityFormula.Count, 0, "정의표 구성됨(폴백)");
            }
            finally { Object.DestroyImmediate(gs); }
        }

        [Test]
        public void NewGame_Null_State_Is_NoOp()
        {
            Assert.DoesNotThrow(() => GameBoot.NewGame(null, null));
        }
    }
}
