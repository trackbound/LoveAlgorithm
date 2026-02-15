using NUnit.Framework;
using LoveAlgo.Core;

namespace LoveAlgo.Tests
{
    /// <summary>
    /// NameValidator 유효성 검증 테스트
    /// </summary>
    public class NameValidatorTests
    {
        #region Valid

        [Test]
        public void Validate_KoreanName_Valid()
        {
            Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("로아"));
            Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("하예은"));
        }

        [Test]
        public void Validate_EnglishName_Valid()
        {
            Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("Player"));
            Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("TestUser123"));
        }

        [Test]
        public void Validate_MixedName_Valid()
        {
            Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("로아123"));
        }

        #endregion

        #region Empty

        [Test]
        public void Validate_Null_ReturnsEmpty()
        {
            Assert.AreEqual(NameValidator.Result.Empty, NameValidator.Validate(null));
        }

        [Test]
        public void Validate_EmptyString_ReturnsEmpty()
        {
            Assert.AreEqual(NameValidator.Result.Empty, NameValidator.Validate(""));
        }

        [Test]
        public void Validate_Whitespace_ReturnsEmpty()
        {
            Assert.AreEqual(NameValidator.Result.Empty, NameValidator.Validate("   "));
        }

        #endregion

        #region TooShort

        [Test]
        public void Validate_SingleChar_TooShort()
        {
            Assert.AreEqual(NameValidator.Result.TooShort, NameValidator.Validate("가"));
            Assert.AreEqual(NameValidator.Result.TooShort, NameValidator.Validate("A"));
        }

        #endregion

        #region TooLong

        [Test]
        public void Validate_KoreanTooLong_ReturnsTooLong()
        {
            // MaxLengthKorean = 6, 7자 한글
            Assert.AreEqual(NameValidator.Result.TooLong, NameValidator.Validate("가나다라마바사"));
        }

        [Test]
        public void Validate_EnglishTooLong_ReturnsTooLong()
        {
            // MaxLengthEnglish = 12, 13자 영문
            Assert.AreEqual(NameValidator.Result.TooLong, NameValidator.Validate("ABCDEFGHIJKLM"));
        }

        [Test]
        public void Validate_KoreanMaxLength_IsValid()
        {
            // 정확히 6자 한글
            Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("가나다라마바"));
        }

        [Test]
        public void Validate_EnglishMaxLength_IsValid()
        {
            // 정확히 12자 영문
            Assert.AreEqual(NameValidator.Result.Valid, NameValidator.Validate("ABCDEFGHIJKL"));
        }

        #endregion

        #region InvalidCharacter

        [Test]
        public void Validate_SpecialChars_ReturnsInvalidCharacter()
        {
            Assert.AreEqual(NameValidator.Result.InvalidCharacter, NameValidator.Validate("로아!"));
            Assert.AreEqual(NameValidator.Result.InvalidCharacter, NameValidator.Validate("test@name"));
            Assert.AreEqual(NameValidator.Result.InvalidCharacter, NameValidator.Validate("이름 공백"));
        }

        #endregion

        #region IsValid / GetErrorMessage

        [Test]
        public void IsValid_ReturnsBoolean()
        {
            Assert.IsTrue(NameValidator.IsValid("로아"));
            Assert.IsFalse(NameValidator.IsValid(""));
            Assert.IsFalse(NameValidator.IsValid("A"));
        }

        [Test]
        public void GetErrorMessage_Valid_ReturnsEmpty()
        {
            Assert.AreEqual("", NameValidator.GetErrorMessage(NameValidator.Result.Valid));
        }

        [Test]
        public void GetErrorMessage_HasMessage_NotEmpty()
        {
            Assert.IsNotEmpty(NameValidator.GetErrorMessage(NameValidator.Result.Empty));
            Assert.IsNotEmpty(NameValidator.GetErrorMessage(NameValidator.Result.TooShort));
            Assert.IsNotEmpty(NameValidator.GetErrorMessage(NameValidator.Result.TooLong));
            Assert.IsNotEmpty(NameValidator.GetErrorMessage(NameValidator.Result.InvalidCharacter));
            Assert.IsNotEmpty(NameValidator.GetErrorMessage(NameValidator.Result.BannedWord));
        }

        #endregion
    }
}
