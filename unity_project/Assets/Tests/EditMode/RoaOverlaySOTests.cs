using NUnit.Framework;
using UnityEngine;
using LoveAlgo.Events;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class RoaOverlaySOTests
    {
        static readonly string[] Pos = { "41", "42" };
        static readonly string[] Neg = { "51" };

        [Test] public void ResolveCategory_Positive() => Assert.AreEqual(RoaOverlaySO.Category.Positive, RoaOverlaySO.ResolveCategory(Pos, Neg, "41"));
        [Test] public void ResolveCategory_Negative() => Assert.AreEqual(RoaOverlaySO.Category.Negative, RoaOverlaySO.ResolveCategory(Pos, Neg, "51"));
        [Test] public void ResolveCategory_Unlisted_Default() => Assert.AreEqual(RoaOverlaySO.Category.Default, RoaOverlaySO.ResolveCategory(Pos, Neg, "00"));
        [Test] public void ResolveCategory_Empty_Default() => Assert.AreEqual(RoaOverlaySO.Category.Default, RoaOverlaySO.ResolveCategory(Pos, Neg, ""));

        [Test]
        public void OverlayName_BuildsFromDeviceAndCategory()
        {
            var so = ScriptableObject.CreateInstance<RoaOverlaySO>();
            try
            {
                Assert.AreEqual("pc_기본", so.OverlayName(RoaDevice.Pc, RoaOverlaySO.Category.Default));
                Assert.AreEqual("모바일_긍정", so.OverlayName(RoaDevice.Mobile, RoaOverlaySO.Category.Positive));
                Assert.AreEqual("pc_부정", so.OverlayName(RoaDevice.Pc, RoaOverlaySO.Category.Negative));
            }
            finally { Object.DestroyImmediate(so); }
        }
    }
}
