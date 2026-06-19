using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;
using LoveAlgo.UI;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 비밀번호 입력 커스텀 시스템 S1 신규 컴포넌트 중 진짜 런타임(StartCoroutine/Time.deltaTime)이
    /// 필요한 행동 검증(PlayMode). 순수 로직은 EditMode(LockScreenIntroEditModeTests)로 분리.
    /// </summary>
    public class LockScreenIntroPlayModeTests
    {
        [UnityTest]
        public IEnumerator PasswordField_Shake_Restores_Base_Position()
        {
            var go = new GameObject("PwField2");
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(go.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            var rt = (RectTransform)inputGo.transform;
            rt.anchoredPosition = new Vector2(100f, 50f);
            var field = go.AddComponent<PasswordInputField>();
            field.Input = input;
            field.ShakeDuration = 0.1f;
            yield return null;

            field.Shake();
            float t = 0f;
            while (t < 0.3f) { t += Time.deltaTime; yield return null; }

            Assert.That(rt.anchoredPosition.x, Is.EqualTo(100f).Within(0.01f), "진동 후 X 복원");
            Assert.That(rt.anchoredPosition.y, Is.EqualTo(50f).Within(0.01f), "진동 후 Y 복원");

            Object.DestroyImmediate(go);
        }
    }
}
