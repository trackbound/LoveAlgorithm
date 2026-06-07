using NUnit.Framework;
using UnityEngine;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// FirstLaunchFlag(PlayerPrefs 어댑터) 단위테스트. 전역 PlayerPrefs를 만지므로 SetUp에서 기존 값을 백업하고
    /// TearDown에서 복원한다(에디터 prefs 오염 방지).
    /// </summary>
    public class FirstLaunchFlagTests
    {
        bool _had;
        int _saved;

        [SetUp]
        public void Backup()
        {
            _had = PlayerPrefs.HasKey(FirstLaunchFlag.Key);
            _saved = PlayerPrefs.GetInt(FirstLaunchFlag.Key, 0);
        }

        [TearDown]
        public void Restore()
        {
            if (_had) PlayerPrefs.SetInt(FirstLaunchFlag.Key, _saved);
            else PlayerPrefs.DeleteKey(FirstLaunchFlag.Key);
            PlayerPrefs.Save();
        }

        [Test]
        public void Reset_Then_NotSeen()
        {
            FirstLaunchFlag.Reset();
            Assert.IsFalse(FirstLaunchFlag.Seen);
        }

        [Test]
        public void MarkSeen_Then_Seen()
        {
            FirstLaunchFlag.Reset();
            FirstLaunchFlag.MarkSeen();
            Assert.IsTrue(FirstLaunchFlag.Seen, "표시 기록 후 Seen=true → 이후 구동은 타이틀로");
        }

        [Test]
        public void Reset_AfterMark_ClearsSeen()
        {
            FirstLaunchFlag.MarkSeen();
            FirstLaunchFlag.Reset();
            Assert.IsFalse(FirstLaunchFlag.Seen, "초기화 후 다시 첫실행처럼");
        }
    }
}
