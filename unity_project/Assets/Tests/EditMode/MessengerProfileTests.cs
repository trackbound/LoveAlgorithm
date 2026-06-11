using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Messenger;

namespace LoveAlgo.Tests.Editor
{
    /// <summary>
    /// 플레이어 프로필 순수 로직 검증 — 기록(음수/널 보정), 표시 폴백(빈 상메=기본 문구),
    /// 후보 카탈로그 인덱스 클램프(후보 축소에도 세이브 안전).
    /// </summary>
    public class MessengerProfileTests
    {
        GameStateSO _gs;

        [SetUp]
        public void SetUp() => _gs = ScriptableObject.CreateInstance<GameStateSO>();

        [TearDown]
        public void TearDown() => Object.DestroyImmediate(_gs);

        [Test]
        public void SetPlayerProfile_Writes_And_Sanitizes()
        {
            MessengerService.SetPlayerProfile(_gs, 2, 1, "오늘도 화이팅");
            Assert.AreEqual(2, _gs.Data.messengerProfileImage);
            Assert.AreEqual(1, _gs.Data.messengerProfileBg);
            Assert.AreEqual("오늘도 화이팅", _gs.Data.messengerStatusMessage);

            MessengerService.SetPlayerProfile(_gs, -3, -1, null);
            Assert.AreEqual(0, _gs.Data.messengerProfileImage, "음수 인덱스 0 보정");
            Assert.AreEqual(0, _gs.Data.messengerProfileBg);
            Assert.AreEqual("", _gs.Data.messengerStatusMessage, "null 상메는 빈 문자열");
        }

        [Test]
        public void PlayerStatus_Falls_Back_When_Empty()
        {
            Assert.AreEqual("기본 문구", MessengerService.PlayerStatus(_gs, "기본 문구"));
            MessengerService.SetPlayerProfile(_gs, 0, 0, "내가 쓴 상메");
            Assert.AreEqual("내가 쓴 상메", MessengerService.PlayerStatus(_gs, "기본 문구"));
        }

        [Test]
        public void ProfileCatalog_Clamps_Index()
        {
            var catalog = ScriptableObject.CreateInstance<MessengerProfileCatalogSO>();
            var tex = new Texture2D(2, 2);
            var a = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero);
            var b = Sprite.Create(tex, new Rect(0, 0, 2, 2), Vector2.zero);
            try
            {
                Assert.IsNull(catalog.ProfileImage(0), "빈 목록 = null");

                catalog.SetData(new List<Sprite> { a, b }, new List<Sprite> { a });
                Assert.AreSame(a, catalog.ProfileImage(-5), "음수 → 첫 후보");
                Assert.AreSame(b, catalog.ProfileImage(99), "범위 밖 → 마지막 후보(후보 축소 세이브 안전망)");
                Assert.AreSame(a, catalog.Background(3));
            }
            finally
            {
                Object.DestroyImmediate(catalog);
                Object.DestroyImmediate(a);
                Object.DestroyImmediate(b);
                Object.DestroyImmediate(tex);
            }
        }
    }
}
