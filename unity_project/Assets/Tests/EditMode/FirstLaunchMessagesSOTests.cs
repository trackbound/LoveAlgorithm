using NUnit.Framework;
using UnityEditor;
using LoveAlgo.MessageStack;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>첫실행 메시지 SO가 존재하고 ROA 발신·4줄로 바인딩됐는지 검증(연출 데이터 가드).</summary>
    public class FirstLaunchMessagesSOTests
    {
        const string Path = "Assets/_Project/Data/FirstLaunchMessages.asset";

        [Test]
        public void Asset_Exists_Ros_FourLines()
        {
            var so = AssetDatabase.LoadAssetAtPath<MessageSequenceSO>(Path);
            Assert.IsNotNull(so, $"SO 로드: {Path}");
            Assert.AreEqual("ROA", so.SenderName, "발신자=ROA.");
            Assert.AreEqual(4, so.Lines.Count, "placeholder 4줄.");
        }
    }
}
