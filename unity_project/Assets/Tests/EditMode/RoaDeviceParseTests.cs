using NUnit.Framework;
using LoveAlgo.Events;

namespace LoveAlgo.Tests.Editor
{
    [TestFixture]
    public class RoaDeviceParseTests
    {
        [Test] public void Parse_Pc() { Assert.IsTrue(RoaDeviceParse.TryParse("pc", out var d)); Assert.AreEqual(RoaDevice.Pc, d); }
        [Test] public void Parse_Pc_CaseInsensitive() { Assert.IsTrue(RoaDeviceParse.TryParse("PC", out var d)); Assert.AreEqual(RoaDevice.Pc, d); }
        [Test] public void Parse_Mobile_Korean() { Assert.IsTrue(RoaDeviceParse.TryParse("모바일", out var d)); Assert.AreEqual(RoaDevice.Mobile, d); }
        [Test] public void Parse_Mobile_English() { Assert.IsTrue(RoaDeviceParse.TryParse("Mobile", out var d)); Assert.AreEqual(RoaDevice.Mobile, d); }
        [Test] public void Parse_Unknown_False() { Assert.IsFalse(RoaDeviceParse.TryParse("xyz", out _)); }
        [Test] public void Parse_Empty_False() { Assert.IsFalse(RoaDeviceParse.TryParse("", out _)); }
        [Test] public void ToToken_RoundTrips() { Assert.AreEqual("모바일", RoaDeviceParse.ToToken(RoaDevice.Mobile)); Assert.AreEqual("pc", RoaDeviceParse.ToToken(RoaDevice.Pc)); }
    }
}
