using NUnit.Framework;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

namespace LoveAlgo.Tests
{
    /// <summary>
    /// SaveManager 저장/로드 테스트
    /// </summary>
    public class SaveManagerTests
    {
        // 테스트용 SaveData 구조 (실제 SaveData와 동일해야 함)
        [System.Serializable]
        public class TestSaveData
        {
            public int version = 1;
            public string playerName = "";
            public int currentDay = 1;
            public string currentPhase = "Title";
            public string lastScriptName = "";
            public int lastLineIndex = 0;
            
            // 스탯
            public int strength;
            public int intelligence;
            public int sociability;
            public int perseverance;
            public int fatigue;
            public int money;
            
            // 호감도
            public System.Collections.Generic.Dictionary<string, int> lovePoints = new();
            
            // 플래그
            public System.Collections.Generic.Dictionary<string, bool> flags = new();
            
            // 메타
            public string savedAt;
            public string thumbnailBase64;
        }

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
            {
                File.Delete(testSavePath);
            }
        }

        [Test]
        public void SaveData_SerializesToJson_Correctly()
        {
            var data = new TestSaveData
            {
                playerName = "테스트플레이어",
                currentDay = 5,
                currentPhase = "DayLoop",
                strength = 20,
                intelligence = 25,
                money = 15000
            };
            data.lovePoints["Roa"] = 35;
            data.lovePoints["Daeun"] = 10;
            data.flags["Met_Roa"] = true;

            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            
            Assert.IsNotNull(json);
            Assert.IsTrue(json.Contains("테스트플레이어"));
            Assert.IsTrue(json.Contains("DayLoop"));
            Assert.IsTrue(json.Contains("Roa"));
        }

        [Test]
        public void SaveData_DeserializesFromJson_Correctly()
        {
            var originalData = new TestSaveData
            {
                playerName = "로드테스트",
                currentDay = 3,
                intelligence = 30,
                fatigue = 50
            };
            originalData.lovePoints["Bom"] = 20;
            originalData.flags["Confessed"] = true;

            var json = JsonConvert.SerializeObject(originalData);
            var loadedData = JsonConvert.DeserializeObject<TestSaveData>(json);

            Assert.AreEqual("로드테스트", loadedData.playerName);
            Assert.AreEqual(3, loadedData.currentDay);
            Assert.AreEqual(30, loadedData.intelligence);
            Assert.AreEqual(50, loadedData.fatigue);
            Assert.AreEqual(20, loadedData.lovePoints["Bom"]);
            Assert.IsTrue(loadedData.flags["Confessed"]);
        }

        [Test]
        public void SaveData_WriteAndRead_FileIntegrity()
        {
            var data = new TestSaveData
            {
                playerName = "파일테스트",
                currentDay = 7,
                money = 50000
            };
            data.lovePoints["Heewon"] = 15;

            // 저장
            var json = JsonConvert.SerializeObject(data, Formatting.Indented);
            File.WriteAllText(testSavePath, json);

            Assert.IsTrue(File.Exists(testSavePath));

            // 로드
            var loadedJson = File.ReadAllText(testSavePath);
            var loadedData = JsonConvert.DeserializeObject<TestSaveData>(loadedJson);

            Assert.AreEqual("파일테스트", loadedData.playerName);
            Assert.AreEqual(7, loadedData.currentDay);
            Assert.AreEqual(50000, loadedData.money);
            Assert.AreEqual(15, loadedData.lovePoints["Heewon"]);
        }

        [Test]
        public void SaveData_EmptyDictionaries_HandleCorrectly()
        {
            var data = new TestSaveData
            {
                playerName = "빈딕셔너리테스트"
            };

            var json = JsonConvert.SerializeObject(data);
            var loadedData = JsonConvert.DeserializeObject<TestSaveData>(json);

            Assert.IsNotNull(loadedData.lovePoints);
            Assert.IsNotNull(loadedData.flags);
            Assert.AreEqual(0, loadedData.lovePoints.Count);
            Assert.AreEqual(0, loadedData.flags.Count);
        }

        [Test]
        public void SaveData_KoreanText_PreservedCorrectly()
        {
            var data = new TestSaveData
            {
                playerName = "한글이름테스트",
                lastScriptName = "프롤로그"
            };

            var json = JsonConvert.SerializeObject(data);
            var loadedData = JsonConvert.DeserializeObject<TestSaveData>(json);

            Assert.AreEqual("한글이름테스트", loadedData.playerName);
            Assert.AreEqual("프롤로그", loadedData.lastScriptName);
        }

        [Test]
        public void SaveData_AllStats_PreservedCorrectly()
        {
            var data = new TestSaveData
            {
                strength = 10,
                intelligence = 20,
                sociability = 30,
                perseverance = 40,
                fatigue = 50
            };

            var json = JsonConvert.SerializeObject(data);
            var loadedData = JsonConvert.DeserializeObject<TestSaveData>(json);

            Assert.AreEqual(10, loadedData.strength);
            Assert.AreEqual(20, loadedData.intelligence);
            Assert.AreEqual(30, loadedData.sociability);
            Assert.AreEqual(40, loadedData.perseverance);
            Assert.AreEqual(50, loadedData.fatigue);
        }
    }
}
