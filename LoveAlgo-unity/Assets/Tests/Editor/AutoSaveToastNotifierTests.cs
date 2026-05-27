using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Save;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// D7 자동저장 토스트 — PlayerPrefs 토글이 정상 동작하는지 검증.
    /// (실제 토스트 표시는 PopupManager 가용해야 하므로 PlayMode 영역, 여긴 토글만.)
    /// </summary>
    [TestFixture]
    public class AutoSaveToastNotifierTests
    {
        int _savedPref;
        bool _hadKey;

        [SetUp]
        public void SetUp()
        {
            // 기존 PlayerPrefs 값 보존
            _hadKey = PlayerPrefs.HasKey(AutoSaveToastNotifier.EnabledPlayerPref);
            _savedPref = _hadKey ? PlayerPrefs.GetInt(AutoSaveToastNotifier.EnabledPlayerPref) : -1;
        }

        [TearDown]
        public void TearDown()
        {
            // 다른 테스트에 토글 잔재가 새지 않도록 복원
            if (_hadKey)
                PlayerPrefs.SetInt(AutoSaveToastNotifier.EnabledPlayerPref, _savedPref);
            else
                PlayerPrefs.DeleteKey(AutoSaveToastNotifier.EnabledPlayerPref);
            PlayerPrefs.Save();
        }

        [Test]
        public void Enabled_DefaultsTrue_WhenPrefMissing()
        {
            PlayerPrefs.DeleteKey(AutoSaveToastNotifier.EnabledPlayerPref);
            PlayerPrefs.Save();

            Assert.IsTrue(AutoSaveToastNotifier.Enabled,
                "PlayerPrefs 키 없을 때 기본값은 true (옵션 미설정 = 보여줌)");
        }

        [Test]
        public void Enabled_SetterPersistsToPlayerPrefs()
        {
            AutoSaveToastNotifier.Enabled = false;
            Assert.AreEqual(0, PlayerPrefs.GetInt(AutoSaveToastNotifier.EnabledPlayerPref, 1),
                "Enabled=false → PlayerPrefs 0");

            AutoSaveToastNotifier.Enabled = true;
            Assert.AreEqual(1, PlayerPrefs.GetInt(AutoSaveToastNotifier.EnabledPlayerPref, 0),
                "Enabled=true → PlayerPrefs 1");
        }

        [Test]
        public void Enabled_RoundTrips()
        {
            AutoSaveToastNotifier.Enabled = false;
            Assert.IsFalse(AutoSaveToastNotifier.Enabled);

            AutoSaveToastNotifier.Enabled = true;
            Assert.IsTrue(AutoSaveToastNotifier.Enabled);
        }
    }
}
