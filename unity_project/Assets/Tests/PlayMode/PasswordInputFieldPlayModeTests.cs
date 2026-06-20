using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>PasswordInputField: 한글 입력이 onValueChanged에서 두벌식 QWERTY 영문으로 변환되는지.</summary>
    public class PasswordInputFieldPlayModeTests
    {
        [UnityTest]
        public IEnumerator KoreanInput_ConvertedToQwerty()
        {
            var go = new GameObject("Pw", typeof(RectTransform));
            go.SetActive(false);
            var input = go.AddComponent<TMP_InputField>();
            var textArea = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            textArea.transform.SetParent(go.transform, false);
            input.textComponent = textArea;
            var pw = go.AddComponent<PasswordInputField>();
            pw.Input = input;
            go.SetActive(true); // OnEnable → onValueChanged 등록
            yield return null;
            try
            {
                input.text = "ㅂㅈㄷ"; // IME 잔여 자모
                Assert.AreEqual("qwe", input.text, "한글 자모 → QWERTY");
                input.text = "가Pass1!";
                Assert.AreEqual("rkPass1!", input.text, "혼합: 한글 변환 + ASCII 통과");
                input.text = "Plain9#";
                Assert.AreEqual("Plain9#", input.text, "ASCII는 그대로");
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
