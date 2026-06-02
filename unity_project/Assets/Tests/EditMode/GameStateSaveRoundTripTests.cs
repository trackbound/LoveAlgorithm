using System.IO;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// M1 step2 검증 (ADR-007/012): 신규 단일 상태 컨테이너 GameStateSO ·
    /// 직렬화 모델 GameStateData · JSON I/O JsonSaveStore의 작동 증거.
    /// 모두 정적/플레인 클래스 + SO 인스턴스만 다루므로 EditMode로 충분.
    /// 사용자 실제 세이브와 충돌 방지를 위해 슬롯 93/94 사용(일반 0~29 범위 밖).
    /// </summary>
    [TestFixture]
    public class GameStateSaveRoundTripTests
    {
        const int TestSlot1 = 93;
        const int TestSlot2 = 94;

        [SetUp]
        public void SetUp()
        {
            JsonSaveStore.Delete(TestSlot1);
            JsonSaveStore.Delete(TestSlot2);
        }

        [TearDown]
        public void TearDown()
        {
            JsonSaveStore.Delete(TestSlot1);
            JsonSaveStore.Delete(TestSlot2);
        }

        static GameStateSO MakeState()
        {
            var so = ScriptableObject.CreateInstance<GameStateSO>();
            so.ResetRuntime();
            return so;
        }

        // ── 1. GameStateSO 동기 접근 의미 (엔트리 리스트 ↔ dict) ──────────────
        [Test]
        public void GetLove_Default_Is_Zero_And_Set_Get_RoundTrips()
        {
            var so = MakeState();
            try
            {
                Assert.AreEqual(0, so.GetLove("Roa"), "미설정 호감도 기본값 0");

                so.SetLove("Roa", 12);
                Assert.AreEqual(12, so.GetLove("Roa"), "SetLove 후 GetLove");

                // 같은 키 재설정은 추가가 아니라 갱신
                so.SetLove("Roa", 20);
                Assert.AreEqual(20, so.GetLove("Roa"), "동일 키 갱신");
                Assert.AreEqual(1, so.Data.lovePoints.Count, "동일 키 중복 엔트리 금지");

                so.AddLove("Roa", 5);
                Assert.AreEqual(25, so.GetLove("Roa"), "AddLove 누적");

                so.AddLove("Haeeun", 3);
                Assert.AreEqual(3, so.GetLove("Haeeun"), "신규 키 AddLove");
                Assert.AreEqual(2, so.Data.lovePoints.Count, "서로 다른 키는 별도 엔트리");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Flags_Default_False_And_Set_Get_RoundTrips()
        {
            var so = MakeState();
            try
            {
                Assert.IsFalse(so.GetFlag("MetRoa"), "미설정 플래그 기본값 false");

                so.SetFlag("MetRoa", true);
                Assert.IsTrue(so.GetFlag("MetRoa"), "SetFlag(true) 후 GetFlag");

                so.SetFlag("MetRoa", false);
                Assert.IsFalse(so.GetFlag("MetRoa"), "동일 키 갱신(false)");
                Assert.AreEqual(1, so.Data.flags.Count, "동일 키 중복 엔트리 금지");
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── 2. ResetRuntime: 새 게임 시작 시 상태 초기화 (부팅 리셋 경로) ──────
        [Test]
        public void ResetRuntime_Clears_All_State()
        {
            var so = MakeState();
            try
            {
                so.Data.playerName = "탐험가";
                so.Data.money = 9999;
                so.Data.day = 17;
                so.SetLove("Roa", 40);
                so.SetFlag("MetRoa", true);

                so.ResetRuntime();

                Assert.AreEqual("", so.Data.playerName, "playerName 초기화");
                Assert.AreEqual(0, so.Data.money, "money 초기화");
                Assert.AreEqual(1, so.Data.day, "day 기본값 1로 초기화");
                Assert.AreEqual(0, so.Data.lovePoints.Count, "호감도 엔트리 비움");
                Assert.AreEqual(0, so.Data.flags.Count, "플래그 엔트리 비움");
                Assert.AreEqual(0, so.GetLove("Roa"), "리셋 후 호감도 0");
                Assert.IsFalse(so.GetFlag("MetRoa"), "리셋 후 플래그 false");
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Load_Null_Yields_Fresh_State()
        {
            var so = MakeState();
            try
            {
                so.Data.money = 500;
                so.Load(null);
                Assert.IsNotNull(so.Data, "Load(null) 후에도 Data는 비-null");
                Assert.AreEqual(0, so.Data.money, "Load(null)은 새 상태로 대체");
            }
            finally { Object.DestroyImmediate(so); }
        }

        // ── 3. JsonSaveStore Save→Load 라운드트립 (디스크 직렬화) ─────────────
        [Test]
        public void Save_Then_Load_Returns_Same_State()
        {
            var so = MakeState();
            try
            {
                so.Data.playerName = "Alice";
                so.Data.money = 12345;
                so.Data.day = 8;
                so.Data.str = 30;
                so.Data.intel = 42;
                so.Data.soc = 11;
                so.Data.per = 7;
                so.Data.fatigue = 55;
                so.SetLove("Roa", 30);
                so.SetLove("Haeeun", 16);
                so.SetFlag("MetRoa", true);

                var save = new SaveData
                {
                    savedAtUtc = "2026-06-02T00:00:00Z",
                    chapterLabel = "테스트 챕터",
                    state = so.Data,
                };

                Assert.IsTrue(JsonSaveStore.Save(TestSlot1, save), "Save 성공");
                Assert.IsTrue(JsonSaveStore.Exists(TestSlot1), "저장 후 Exists true");

                var loaded = JsonSaveStore.Load(TestSlot1);
                Assert.IsNotNull(loaded, "Load null 아님");
                Assert.AreEqual(SaveData.CurrentVersion, loaded.version, "저장 시 CurrentVersion 갱신");
                Assert.AreEqual("테스트 챕터", loaded.chapterLabel, "chapterLabel 복원");

                // 복원 상태를 새 SO에 적용 (도메인 리로드 후 새 게임 객체 시뮬레이션)
                var restored = MakeState();
                try
                {
                    restored.Load(loaded.state);
                    Assert.AreEqual("Alice", restored.Data.playerName, "playerName 복원");
                    Assert.AreEqual(12345, restored.Data.money, "money 복원");
                    Assert.AreEqual(8, restored.Data.day, "day 복원");
                    Assert.AreEqual(30, restored.Data.str, "str 복원");
                    Assert.AreEqual(42, restored.Data.intel, "intel 복원");
                    Assert.AreEqual(11, restored.Data.soc, "soc 복원");
                    Assert.AreEqual(7, restored.Data.per, "per 복원");
                    Assert.AreEqual(55, restored.Data.fatigue, "fatigue 복원");
                    Assert.AreEqual(30, restored.GetLove("Roa"), "호감도 Roa 복원");
                    Assert.AreEqual(16, restored.GetLove("Haeeun"), "호감도 Haeeun 복원");
                    Assert.IsTrue(restored.GetFlag("MetRoa"), "플래그 복원");
                }
                finally { Object.DestroyImmediate(restored); }
            }
            finally { Object.DestroyImmediate(so); }
        }

        [Test]
        public void Load_Missing_Slot_Returns_Null()
        {
            Assert.IsFalse(JsonSaveStore.Exists(TestSlot2), "사전조건: 슬롯 없음");
            Assert.IsNull(JsonSaveStore.Load(TestSlot2), "없는 슬롯 로드는 null(호출부 새 게임 처리)");
        }

        [Test]
        public void Delete_Removes_Slot_File()
        {
            var save = new SaveData { state = new GameStateData { playerName = "ToDelete" } };
            JsonSaveStore.Save(TestSlot1, save);
            Assert.IsTrue(JsonSaveStore.Exists(TestSlot1), "사전조건: 저장됨");

            JsonSaveStore.Delete(TestSlot1);
            Assert.IsFalse(JsonSaveStore.Exists(TestSlot1), "Delete 후 슬롯 파일 제거");
        }
    }
}
