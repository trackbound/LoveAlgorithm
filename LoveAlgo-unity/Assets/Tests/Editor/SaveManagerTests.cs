using NUnit.Framework;
using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using LoveAlgo.Story;
using LoveAlgo.Core;

namespace LoveAlgo.Tests
{
    /// <summary>
    /// SaveData 직렬화/역직렬화 테스트 (실제 SaveData 클래스 사용)
    /// </summary>
    public class SaveManagerTests
    {
        private string testSavePath;

        [SetUp]
        public void Setup()
        {
            testSavePath = Path.Combine(Application.temporaryCachePath, "test_save.json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(testSavePath))
                File.Delete(testSavePath);
        }

        /// <summary>
        /// 기본 필드가 포함된 테스트용 SaveData 생성 헬퍼
        /// </summary>
        static SaveData CreateSampleData()
        {
            return new SaveData
            {
                Phase = GamePhase.DayLoop,
                CurrentDay = 5,
                RemainingActions = 2,
                ScriptName = "Prologue",
                LineId = "Morning_Start",
                LineIndex = 3,
                PlayerName = "테스트플레이어",
                Money = 15000,
                Strength = 20,
                Intelligence = 25,
                Sociability = 10,
                Perseverance = 5,
                Fatigue = 30,
                SaveTime = new DateTime(2025, 1, 15, 12, 30, 0),
                ChapterName = "프롤로그",
                CurrentBG = "School_Day",
                CurrentBGM = "Morning",
                CurrentCG = null,
                CurrentOverlay = null,
                IsMonologueDimShowing = false,
                IsFadeBlack = false,
                IsEyeClosed = false
            };
        }

        [Test]
        public void SaveData_SerializesToJson_Correctly()
        {
            var data = CreateSampleData();
            data.LovePoints["Roa"] = 35;
            data.LovePoints["Daeun"] = 10;
            data.Flags["Met_Roa"] = true;

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);

            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("테스트플레이어"));
            // GamePhase enum은 정수로 직렬화되므로 enum 이름 대신 값 존재 확인
            Assert.IsTrue(json.Contains("\"Phase\":"));
            Assert.IsTrue(json.Contains("Roa"));
        }

        [Test]
        public void SaveData_DeserializesFromJson_Correctly()
        {
            var data = CreateSampleData();
            data.LovePoints["Bom"] = 20;
            data.Flags["Confessed"] = true;

            var json = JsonConvert.SerializeObject(data);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.AreEqual("테스트플레이어", loaded.PlayerName);
            Assert.AreEqual(5, loaded.CurrentDay);
            Assert.AreEqual(GamePhase.DayLoop, loaded.Phase);
            Assert.AreEqual(25, loaded.Intelligence);
            Assert.AreEqual(30, loaded.Fatigue);
            Assert.AreEqual(20, loaded.LovePoints["Bom"]);
            Assert.IsTrue(loaded.Flags["Confessed"]);
            Assert.AreEqual("Prologue", loaded.ScriptName);
            Assert.AreEqual("Morning_Start", loaded.LineId);
        }

        [Test]
        public void SaveData_WriteAndRead_FileIntegrity()
        {
            var data = CreateSampleData();
            data.Money = 50000;
            data.LovePoints["Heewon"] = 15;

            // 저장
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(testSavePath, json);
            Assert.IsTrue(File.Exists(testSavePath));

            // 로드
            var loadedJson = File.ReadAllText(testSavePath);
            var loaded = JsonConvert.DeserializeObject<SaveData>(loadedJson);

            Assert.AreEqual("테스트플레이어", loaded.PlayerName);
            Assert.AreEqual(5, loaded.CurrentDay);
            Assert.AreEqual(50000, loaded.Money);
            Assert.AreEqual(15, loaded.LovePoints["Heewon"]);
        }

        [Test]
        public void SaveData_EmptyDictionaries_HandleCorrectly()
        {
            var data = new SaveData { PlayerName = "빈딕셔너리" };

            var json = JsonConvert.SerializeObject(data);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.IsNotNull(loaded.LovePoints);
            Assert.IsNotNull(loaded.Flags);
            Assert.AreEqual(0, loaded.LovePoints.Count);
            Assert.AreEqual(0, loaded.Flags.Count);
        }

        [Test]
        public void SaveData_KoreanText_PreservedCorrectly()
        {
            var data = new SaveData
            {
                PlayerName = "한글이름테스트",
                ScriptName = "프롤로그",
                ChapterName = "제1장"
            };

            var json = JsonConvert.SerializeObject(data);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.AreEqual("한글이름테스트", loaded.PlayerName);
            Assert.AreEqual("프롤로그", loaded.ScriptName);
            Assert.AreEqual("제1장", loaded.ChapterName);
        }

        [Test]
        public void SaveData_AllStats_PreservedCorrectly()
        {
            var data = new SaveData
            {
                Strength = 10,
                Intelligence = 20,
                Sociability = 30,
                Perseverance = 40,
                Fatigue = 50
            };

            var json = JsonConvert.SerializeObject(data);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.AreEqual(10, loaded.Strength);
            Assert.AreEqual(20, loaded.Intelligence);
            Assert.AreEqual(30, loaded.Sociability);
            Assert.AreEqual(40, loaded.Perseverance);
            Assert.AreEqual(50, loaded.Fatigue);
        }

        [Test]
        public void SaveData_DateTime_PreservedCorrectly()
        {
            var saveTime = new DateTime(2025, 6, 15, 14, 30, 45);
            var data = new SaveData { SaveTime = saveTime };

            var json = JsonConvert.SerializeObject(data);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.AreEqual(saveTime, loaded.SaveTime);
        }

        [Test]
        public void SaveData_Characters_PreservedCorrectly()
        {
            var data = new SaveData();
            data.Characters.Add(new CharacterSaveInfo
            {
                Slot = "C",
                Character = "Roa",
                Emote = "Happy"
            });
            data.Characters.Add(new CharacterSaveInfo
            {
                Slot = "L",
                Character = "Daeun",
                Emote = "Default"
            });

            var json = JsonConvert.SerializeObject(data);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.AreEqual(2, loaded.Characters.Count);
            Assert.AreEqual("C", loaded.Characters[0].Slot);
            Assert.AreEqual("Roa", loaded.Characters[0].Character);
            Assert.AreEqual("Happy", loaded.Characters[0].Emote);
        }

        [Test]
        public void SaveData_SceneState_PreservedCorrectly()
        {
            var data = new SaveData
            {
                CurrentBG = "Cafe_Night",
                CurrentBGM = "Romantic",
                CurrentCG = "CG/Roa_FirstMeet",
                CurrentOverlay = "Roa_Theme",
                IsMonologueDimShowing = true,
                IsFadeBlack = false,
                IsEyeClosed = true
            };

            var json = JsonConvert.SerializeObject(data);
            var loaded = JsonConvert.DeserializeObject<SaveData>(json);

            Assert.AreEqual("Cafe_Night", loaded.CurrentBG);
            Assert.AreEqual("Romantic", loaded.CurrentBGM);
            Assert.AreEqual("CG/Roa_FirstMeet", loaded.CurrentCG);
            Assert.AreEqual("Roa_Theme", loaded.CurrentOverlay);
            Assert.IsTrue(loaded.IsMonologueDimShowing);
            Assert.IsFalse(loaded.IsFadeBlack);
            Assert.IsTrue(loaded.IsEyeClosed);
        }

        [Test]
        public void SaveData_GamePhaseEnum_SerializesAsExpected()
        {
            // 모든 GamePhase 값이 올바르게 라운드트립되는지 확인
            foreach (GamePhase phase in Enum.GetValues(typeof(GamePhase)))
            {
                var data = new SaveData { Phase = phase };
                var json = JsonConvert.SerializeObject(data);
                var loaded = JsonConvert.DeserializeObject<SaveData>(json);
                Assert.AreEqual(phase, loaded.Phase, $"GamePhase.{phase} 라운드트립 실패");
            }
        }
    }
}
