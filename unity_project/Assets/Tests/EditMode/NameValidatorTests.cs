using NUnit.Framework;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>UsernameScreen이 의존하는 NameValidator 규칙 회귀(완성형만·금칙어·길이).</summary>
    public class NameValidatorTests
    {
        [Test] public void Valid_Korean() => Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("가나"));
        [Test] public void IncompleteJamo_Rejected() => Assert.AreEqual(NameValidator.Result.InvalidCharacter, NameValidator.Validate("ㄱㄴ"));
        [Test] public void TooShort() => Assert.AreEqual(NameValidator.Result.TooShort, NameValidator.Validate("가"));
        [Test] public void BannedWord() => Assert.AreEqual(NameValidator.Result.BannedWord, NameValidator.Validate("admin"));
    }
}
