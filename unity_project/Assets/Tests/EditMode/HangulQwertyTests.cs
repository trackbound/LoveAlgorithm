using NUnit.Framework;
using LoveAlgo.Core;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// 한글→두벌식 QWERTY 역매핑(GameObject 불필요). 완성형 음절 분해·호환자모·복합자모·ASCII 통과·strip.
    /// </summary>
    public class HangulQwertyTests
    {
        [Test] public void Syllable_Basic()
        {
            Assert.AreEqual("rk", HangulQwerty.ToQwerty("가"));   // ㄱ+ㅏ
            Assert.AreEqual("dks", HangulQwerty.ToQwerty("안"));  // ㅇ+ㅏ+ㄴ
            Assert.AreEqual("dkssud", HangulQwerty.ToQwerty("안녕")); // 안 + ㄴ+ㅕ+ㅇ
        }

        [Test] public void Syllable_CompoundJong_AndVowel()
        {
            Assert.AreEqual("rkqt", HangulQwerty.ToQwerty("값")); // ㄱ+ㅏ+ㅄ
            Assert.AreEqual("dhk", HangulQwerty.ToQwerty("와"));  // ㅇ+ㅘ
        }

        [Test] public void CompatibilityJamo()
        {
            Assert.AreEqual("qwe", HangulQwerty.ToQwerty("ㅂㅈㄷ"));
            Assert.AreEqual("r", HangulQwerty.ToQwerty("ㄱ"));
        }

        [Test] public void AsciiPassthrough()
        {
            Assert.AreEqual("Pass1!@#", HangulQwerty.ToQwerty("Pass1!@#")); // 영/숫/특수 통과
            Assert.AreEqual("aZ09~`", HangulQwerty.ToQwerty("aZ09~`"));     // 경계 ASCII 통과
        }

        [Test] public void Strip_SpaceAndNonMappable()
        {
            Assert.AreEqual("abc", HangulQwerty.ToQwerty("a b c")); // 공백 제거
            Assert.AreEqual("hi", HangulQwerty.ToQwerty("hi😀"));   // 이모지(서로게이트) 제거
        }

        [Test] public void Mixed_KoreanAscii()
        {
            Assert.AreEqual("rkPass1", HangulQwerty.ToQwerty("가Pass1"));
        }
    }
}
