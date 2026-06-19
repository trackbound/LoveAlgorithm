using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
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

        [UnityTest]
        public IEnumerator IntroDirector_Play_Slides_Fades_And_Calls_Back()
        {
            var go = new GameObject("Intro");
            // dim
            var dimGo = new GameObject("Dim");
            dimGo.transform.SetParent(go.transform);
            var dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 1f); // 시작값이 1이어도 ResetToStart가 0으로 만든다
            // input group
            var inputGo = new GameObject("InputGroup");
            inputGo.transform.SetParent(go.transform);
            var inputGroup = inputGo.AddComponent<CanvasGroup>();
            // widget
            var wGo = new GameObject("Widget", typeof(RectTransform));
            wGo.transform.SetParent(go.transform);
            var wRt = (RectTransform)wGo.transform;
            wRt.anchoredPosition = new Vector2(10f, 0f);

            var intro = go.AddComponent<LockScreenIntroDirector>();
            intro.Dim = dim;
            intro.InputGroup = inputGroup;
            intro.DimTargetAlpha = 0.58f;
            intro.SetTimings(0.02f, 0.05f, 0.05f, 0.05f); // hold, slide, dim, reveal (빠르게)
            intro.SetWidgets(new[] { (wRt, new Vector2(-200f, 0f)) });
            yield return null;

            intro.ResetToStart();
            Assert.AreEqual(0f, dim.color.a, 0.001f, "Reset 후 dim 0");
            Assert.AreEqual(0f, inputGroup.alpha, 0.001f, "Reset 후 입력 0");

            bool called = false;
            intro.Play(() => called = true);

            float guard = 0f;
            while (intro.IsPlaying && guard < 3f) { guard += Time.deltaTime; yield return null; }

            Assert.IsTrue(called, "onInputReady 콜백 도달");
            Assert.AreEqual(0.58f, dim.color.a, 0.02f, "dim 최종 alpha 도달");
            Assert.AreEqual(1f, inputGroup.alpha, 0.02f, "입력 그룹 노출");
            Assert.That(wRt.anchoredPosition.x, Is.EqualTo(-190f).Within(1f), "위젯 슬라이드아웃(10 + -200)");

            Object.DestroyImmediate(go);
        }
    }
}
