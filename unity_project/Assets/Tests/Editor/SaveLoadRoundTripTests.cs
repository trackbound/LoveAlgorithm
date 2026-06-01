using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using LoveAlgo.Core;
using LoveAlgo.Story.SaveSystem;
using UnityEngine.TestTools;
// 재작성 전환기: 신규 LoveAlgo.Core.SaveData와 충돌 방지. 구 Save 모듈 폐기(M5) 시 함께 제거.
using SaveData = LoveAlgo.Story.SaveSystem.SaveData;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// SaveDataSerializer의 round-trip + atomic write + .bak 복구 + Version 마이그레이션 검증.
    /// MonoBehaviour 의존 없는 정적 클래스만 호출하므로 EditMode로 충분.
    /// 사용자 실제 세이브와 충돌 방지를 위해 슬롯 번호 91/92 사용(일반 슬롯 0~29 범위 밖).
    /// </summary>
    [TestFixture]
    public class SaveLoadRoundTripTests
    {
        const int TestSlot1 = 91;
        const int TestSlot2 = 92;

        [SetUp]
        public void SetUp()
        {
            DeleteSlotFiles(TestSlot1);
            DeleteSlotFiles(TestSlot2);
        }

        [TearDown]
        public void TearDown()
        {
            DeleteSlotFiles(TestSlot1);
            DeleteSlotFiles(TestSlot2);
        }

        static void DeleteSlotFiles(int slot)
        {
            string path = SaveDataSerializer.GetSavePath(slot);
            if (File.Exists(path)) File.Delete(path);
            string bak = path + ".bak";
            if (File.Exists(bak)) File.Delete(bak);
            string tmp = path + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
        }

        // ── 1. 기본 round-trip ─────────────────────────────────────────────
        [Test]
        public void Save_Then_Load_Returns_Same_Fields()
        {
            var original = MakeSaveData();

            SaveDataSerializer.SaveToFile(original, TestSlot1);
            var loaded = SaveDataSerializer.LoadFromFile(TestSlot1);

            Assert.IsNotNull(loaded, "LoadFromFile이 null 반환 — atomic write 또는 경로 문제");
            Assert.AreEqual(original.PlayerName, loaded.PlayerName, "PlayerName");
            Assert.AreEqual(original.Money, loaded.Money, "Money");
            Assert.AreEqual(original.CurrentDay, loaded.CurrentDay, "CurrentDay");
            Assert.AreEqual(original.RemainingActions, loaded.RemainingActions, "RemainingActions");
            Assert.AreEqual(original.Phase, loaded.Phase, "Phase");
            Assert.AreEqual(original.ScriptName, loaded.ScriptName, "ScriptName");
            Assert.AreEqual(original.LineId, loaded.LineId, "LineId");
            Assert.AreEqual(original.LineIndex, loaded.LineIndex, "LineIndex");
            Assert.AreEqual(original.Strength, loaded.Strength, "Strength");
            Assert.AreEqual(original.Intelligence, loaded.Intelligence, "Intelligence");
            Assert.AreEqual(original.Fatigue, loaded.Fatigue, "Fatigue");
            Assert.AreEqual(30, loaded.LovePoints["Roa"], "LovePoints Dictionary 복원");
            Assert.IsTrue(loaded.Flags["MetRoa"], "Flags Dictionary 복원");
            Assert.AreEqual(2, loaded.ChoiceHistory.Count, "ChoiceHistory List 복원");

            // H3+H4에서 도입한 Version 필드 자동 갱신
            Assert.AreEqual(SaveData.CurrentVersion, loaded.Version, "저장 시 CurrentVersion으로 갱신");
        }

        // ── 2. 두 번 저장하면 .bak가 첫 번째 데이터를 보존하는가 (H3 atomic) ──
        [Test]
        public void Save_Twice_Creates_Backup_With_Previous_Data()
        {
            var first = MakeSaveData();
            first.PlayerName = "First";
            SaveDataSerializer.SaveToFile(first, TestSlot1);

            var second = MakeSaveData();
            second.PlayerName = "Second";
            SaveDataSerializer.SaveToFile(second, TestSlot1);

            string path = SaveDataSerializer.GetSavePath(TestSlot1);
            string bak = path + ".bak";
            Assert.IsTrue(File.Exists(bak), "두 번째 저장 시 .bak가 생성되어야 함");

            // 본 파일은 두 번째 데이터
            var loaded = SaveDataSerializer.LoadFromFile(TestSlot1);
            Assert.AreEqual("Second", loaded.PlayerName, "본 파일은 최신 데이터");

            // .bak 안의 내용도 직접 검증 (parse만 — LoadFromFile은 본 파일 우선)
            string bakJson = File.ReadAllText(bak);
            StringAssert.Contains("\"First\"", bakJson, ".bak에 첫 번째 PlayerName 보존");
        }

        // ── 3. 본 파일 손상 시 .bak에서 복구 ───────────────────────────────
        [Test]
        public void Load_Falls_Back_To_Backup_When_Main_Corrupted()
        {
            // 1차 저장(.bak 없음) → 2차 저장(.bak 생성됨)
            var first = MakeSaveData();
            first.PlayerName = "BackupCheck";
            SaveDataSerializer.SaveToFile(first, TestSlot2);

            var second = MakeSaveData();
            second.PlayerName = "MainNew";
            SaveDataSerializer.SaveToFile(second, TestSlot2);

            // 본 파일을 의도적으로 손상
            string path = SaveDataSerializer.GetSavePath(TestSlot2);
            File.WriteAllText(path, "{ this is not valid json ");

            // .bak에서 복구되어야 함 (.bak 안의 데이터는 1차 = "BackupCheck")
            // 손상된 본 파일 파싱 시 LogError 발생 — 기대값으로 등록
            LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex(@"\[SaveDataSerializer\] 파일 로드 실패"));
            LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex(@"본 파일 손상/없음 — 백업"));

            var loaded = SaveDataSerializer.LoadFromFile(TestSlot2);

            Assert.IsNotNull(loaded, "본 파일 손상 시에도 .bak에서 복구되어야 함");
            Assert.AreEqual("BackupCheck", loaded.PlayerName, ".bak의 원본 데이터로 복구");
        }

        // ── 4. 옛 버전(v0) 세이브가 현재 버전으로 마이그레이션 (H4) ────────
        [Test]
        public void Old_Version_Save_Migrates_To_Current_Version()
        {
            // 현재 버전으로 저장
            var data = MakeSaveData();
            SaveDataSerializer.SaveToFile(data, TestSlot1);

            // JSON을 직접 수정해 Version=0(버전 도입 전)으로 위장
            string path = SaveDataSerializer.GetSavePath(TestSlot1);
            string json = File.ReadAllText(path);
            string oldJson = json.Replace($"\"Version\": {SaveData.CurrentVersion}", "\"Version\": 0");
            Assert.AreNotEqual(json, oldJson, "Version 필드 replace 실패 — JSON 포맷 변경됐을 수 있음");
            File.WriteAllText(path, oldJson);

            // 마이그레이션 로그 기대
            LogAssert.Expect(UnityEngine.LogType.Log,
                new System.Text.RegularExpressions.Regex(@"마이그레이션 v0 → v" + SaveData.CurrentVersion));

            var loaded = SaveDataSerializer.LoadFromFile(TestSlot1);

            Assert.IsNotNull(loaded);
            Assert.AreEqual(SaveData.CurrentVersion, loaded.Version,
                "마이그레이션 후 Version이 CurrentVersion으로 갱신");
        }

        // ── 5. 존재하지 않는 슬롯 로드는 null 반환 (예외 X) ────────────────
        [Test]
        public void Load_Nonexistent_Slot_Returns_Null()
        {
            // 슬롯 91 비어 있음 (SetUp이 삭제)
            var loaded = SaveDataSerializer.LoadFromFile(TestSlot1);
            Assert.IsNull(loaded, "빈 슬롯 로드는 null 반환 (예외 X)");
        }

        // ── Helper: 의미 있는 필드가 채워진 SaveData ───────────────────────
        static SaveData MakeSaveData()
        {
            return new SaveData
            {
                Phase = GamePhase.DayLoop,
                CurrentDay = 5,
                RemainingActions = 1,
                ScriptName = "TestScript",
                LineId = "checkpoint_a",
                LineIndex = 42,
                PlayerName = "테스트",
                Money = 10000,
                LovePoints = new Dictionary<string, int>
                {
                    { "Roa", 30 },
                    { "HaYeEun", 20 }
                },
                Flags = new Dictionary<string, bool>
                {
                    { "MetRoa", true },
                    { "Confessed", false }
                },
                Strength = 5,
                Intelligence = 7,
                Sociability = 3,
                Perseverance = 4,
                Fatigue = 2,
                SaveTime = System.DateTime.Now,
                ChapterName = "테스트 챕터",
                ChoiceHistory = new List<string> { "choice_a", "choice_b" },
                FiredEvents = new List<string>(),
                Characters = new List<CharacterSaveInfo>()
            };
        }
    }
}
