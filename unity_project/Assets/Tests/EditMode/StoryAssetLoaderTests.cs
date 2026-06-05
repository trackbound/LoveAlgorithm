using System.IO;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.Story; // StoryAssetLoader

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// StoryAssetLoader 파일 I/O + 목록 필터. Write→Read 라운드트립·미존재 null은 격리된 임시 서브폴더에서,
    /// 목록 필터(IsListableCsv)는 순수로 검증. 실제 StreamingAssets/Story 루트는 오염하지 않는다.
    /// </summary>
    public class StoryAssetLoaderTests
    {
        const string Sub = "__loader_test__";
        static string Rel(string name) => $"{Sub}/{name}";
        static string AbsSub => Path.Combine(Application.streamingAssetsPath, "Story", Sub);

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(AbsSub)) Directory.Delete(AbsSub, true);
        }

        [Test]
        public void Write_Then_Read_RoundTrips()
        {
            const string csv = "LineID,Type,Speaker,Value,Next\n,Text,,hi,>";
            Assert.IsTrue(StoryAssetLoader.Write(Rel("a.csv"), csv));
            Assert.AreEqual(csv, StoryAssetLoader.Read(Rel("a.csv")));
        }

        [Test]
        public void Read_Missing_Returns_Null()
        {
            LogAssert.Expect(LogType.Warning, new Regex("CSV 없음"));
            Assert.IsNull(StoryAssetLoader.Read(Rel("nope.csv")));
        }

        [Test]
        public void IsListableCsv_Filters_Csv_And_Excludes_Backups()
        {
            Assert.IsTrue(StoryAssetLoader.IsListableCsv("Event1.csv"));
            Assert.IsTrue(StoryAssetLoader.IsListableCsv("Prologue.csv"));
            Assert.IsFalse(StoryAssetLoader.IsListableCsv("Prologue.csv.bak_pre_alias"));
            Assert.IsFalse(StoryAssetLoader.IsListableCsv("notes.txt"));
            Assert.IsFalse(StoryAssetLoader.IsListableCsv(null));
            Assert.IsFalse(StoryAssetLoader.IsListableCsv(""));
        }
    }
}
