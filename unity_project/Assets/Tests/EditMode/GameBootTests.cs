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

        [Test]
        public void GameEntry_Consume_Returns_Mode_Then_Resets_To_NewGame()
        {
            GameEntry.PendingMode = BootMode.Continue;
            Assert.AreEqual(BootMode.Continue, GameEntry.Consume(), "설정한 모드 반환");
            Assert.AreEqual(BootMode.NewGame, GameEntry.PendingMode, "소비 후 NewGame으로 리셋");
            Assert.AreEqual(BootMode.NewGame, GameEntry.Consume(), "리셋 후 기본 NewGame");
        }

        [Test]
        public void ContinueGame_Restores_Saved_State_Without_Reset()
        {
            var backup = JsonSaveStore.Load(JsonSaveStore.AutoSaveSlot); // 유저 슬롯0 보호
            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            try
            {
                gs.ResetRuntime();
                gs.Day = 5;
                gs.Money = 1234;
                JsonSaveStore.Save(JsonSaveStore.AutoSaveSlot, new SaveData { state = gs.Data });

                gs.Day = 1; // 더럽힘 — ContinueGame이 리셋이 아니라 복원해야
                gs.Money = 0;

                bool ok = GameBoot.ContinueGame(gs, null);

                Assert.IsTrue(ok, "세이브 로드 성공");
                Assert.AreEqual(5, gs.Day, "저장된 일차 복원(ResetRuntime 안 함)");
                Assert.AreEqual(1234, gs.Money, "저장된 소지금 복원");
                Assert.Greater(AffinityFormula.Count, 0, "공식 정의표 주입됨(폴백)");
            }
            finally
            {
                if (backup != null) JsonSaveStore.Save(JsonSaveStore.AutoSaveSlot, backup);
                else JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot);
                Object.DestroyImmediate(gs);
            }
        }

        [Test]
        public void ContinueGame_No_Save_Returns_False()
        {
            var backup = JsonSaveStore.Load(JsonSaveStore.AutoSaveSlot); // 유저 슬롯0 보호
            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            try
            {
                JsonSaveStore.Delete(JsonSaveStore.AutoSaveSlot); // 세이브 없음 보장
                Assert.IsFalse(GameBoot.ContinueGame(gs, null), "세이브 없으면 false(호출부가 NewGame 폴백)");
            }
            finally
            {
                if (backup != null) JsonSaveStore.Save(JsonSaveStore.AutoSaveSlot, backup);
                Object.DestroyImmediate(gs);
            }
        }

        [Test]
        public void ContinueGame_Loads_From_Specified_Slot()
        {
            const int Slot = 7; // 유저 슬롯(자동저장 슬롯0과 구분 — 지정 슬롯 로드 검증)
            var backup = JsonSaveStore.Load(Slot);
            var gs = ScriptableObject.CreateInstance<GameStateSO>();
            try
            {
                gs.ResetRuntime();
                gs.Day = 12;
                gs.Money = 4321;
                JsonSaveStore.Save(Slot, new SaveData { state = gs.Data });

                gs.Day = 1; gs.Money = 0; // 더럽힘 — 지정 슬롯에서 복원해야

                bool ok = GameBoot.ContinueGame(gs, null, Slot);

                Assert.IsTrue(ok, "지정 슬롯 로드 성공");
                Assert.AreEqual(12, gs.Day, "지정 슬롯의 일차 복원");
                Assert.AreEqual(4321, gs.Money, "지정 슬롯의 소지금 복원");
            }
            finally
            {
                if (backup != null) JsonSaveStore.Save(Slot, backup);
                else JsonSaveStore.Delete(Slot);
                Object.DestroyImmediate(gs);
            }
        }

        [Test]
        public void GameEntry_Consume_Resets_SelectedSlot()
        {
            GameEntry.SelectedSlot = 9;
            GameEntry.Consume();
            Assert.AreEqual(JsonSaveStore.AutoSaveSlot, GameEntry.SelectedSlot, "소비 후 자동저장 슬롯으로 리셋");
        }
    }
}
