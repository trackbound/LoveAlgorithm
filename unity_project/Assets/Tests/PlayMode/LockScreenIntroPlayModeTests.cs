using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.TestTools;
using TMPro;
using LoveAlgo.UI;
using LoveAlgo.Common;   // EventBus
using LoveAlgo.Events;   // ShowLockScreenCommand, LockMode, CompletionHandle, PasswordAcceptedEvent, PasswordVerifyFailedEvent

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

        [UnityTest]
        public IEnumerator View_Accepted_Event_Hides_Overlay()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            view.Overlay = overlay;
            viewGo.SetActive(true);
            yield return null; // OnEnable 구독

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            Assert.IsTrue(overlay.activeSelf, "Normal Show → 활성");

            EventBus.Publish(new PasswordAcceptedEvent());
            Assert.IsFalse(overlay.activeSelf, "Accepted → 닫힘");

            Object.DestroyImmediate(viewGo);
        }

        [UnityTest]
        public IEnumerator View_Failed_Event_Shakes_And_Clears_Input()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(viewGo.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            var pf = viewGo.AddComponent<PasswordInputField>();
            pf.Input = input;
            pf.ShakeDuration = 0.1f;
            view.Overlay = overlay;
            view.Input = input;
            view.PasswordField = pf;
            viewGo.SetActive(true);
            yield return null; // OnEnable 구독

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            input.text = "9999";
            var rt = (RectTransform)input.transform;
            Vector2 basePos = rt.anchoredPosition;

            EventBus.Publish(new PasswordVerifyFailedEvent(1));
            Assert.AreEqual("", input.text, "실패 → 입력 초기화");

            // 진동 중 한 프레임은 기준 위치에서 벗어난다
            yield return null;
            bool moved = (rt.anchoredPosition - basePos).sqrMagnitude > 0.0001f;
            // 진동 종료까지 대기 후 복원 확인
            float t = 0f;
            while (t < 0.3f) { t += Time.deltaTime; yield return null; }
            Assert.That(rt.anchoredPosition.x, Is.EqualTo(basePos.x).Within(0.01f), "진동 후 복원");
            Assert.IsTrue(moved, "실패 → 진동 발생");

            Object.DestroyImmediate(viewGo);
        }

        [UnityTest]
        public IEnumerator View_ThreeFails_Reveals_Key_And_Lost_Guide()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(viewGo.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var guide = viewGo.AddComponent<LockScreenGuideText>();
            guide.Label = label; guide.LostText = "분실";
            var keyVis = new GameObject("KeyVisual");
            keyVis.transform.SetParent(viewGo.transform);
            var kr = viewGo.AddComponent<KeyResetButton>();
            kr.Root = keyVis;
            view.Overlay = overlay; view.Guide = guide; view.KeyButton = kr; view.LostThreshold = 3;
            viewGo.SetActive(true);
            yield return null; // OnEnable 구독

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            Assert.IsFalse(keyVis.activeSelf, "초기엔 열쇠 숨김");

            EventBus.Publish(new PasswordVerifyFailedEvent(1));
            Assert.IsFalse(keyVis.activeSelf, "1회는 미노출");
            EventBus.Publish(new PasswordVerifyFailedEvent(3));
            Assert.IsTrue(keyVis.activeSelf, "3회+ → 열쇠 노출");
            Assert.AreEqual("분실", label.text, "3회+ → 분실 가이드");

            Object.DestroyImmediate(viewGo);
        }

        [UnityTest]
        public IEnumerator View_ResetRequest_Reconfigures_To_Setup_And_Hides_Key()
        {
            var viewGo = new GameObject("View");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<LockScreenView>();
            var overlay = new GameObject("Overlay");
            overlay.transform.SetParent(viewGo.transform);
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(viewGo.transform);
            var input = inputGo.AddComponent<TMP_InputField>();
            var labelGo = new GameObject("Label");
            labelGo.transform.SetParent(viewGo.transform);
            var label = labelGo.AddComponent<TextMeshProUGUI>();
            var guide = viewGo.AddComponent<LockScreenGuideText>();
            guide.Label = label; guide.SetupText = "설정"; guide.NormalText = "입력"; guide.LostText = "분실";
            var keyVis = new GameObject("KeyVisual");
            keyVis.transform.SetParent(viewGo.transform);
            var kr = viewGo.AddComponent<KeyResetButton>();
            kr.Root = keyVis;
            view.Overlay = overlay; view.Input = input; view.Guide = guide; view.KeyButton = kr; view.LostThreshold = 3;
            viewGo.SetActive(true);
            yield return null;

            view.OnShow(new ShowLockScreenCommand(LockMode.Normal, false, null, new CompletionHandle()));
            EventBus.Publish(new PasswordVerifyFailedEvent(3)); // 열쇠+분실
            Assert.IsTrue(keyVis.activeSelf);

            EventBus.Publish(new RequestPasswordResetCommand());
            Assert.IsFalse(keyVis.activeSelf, "재설정 → 열쇠 숨김");
            Assert.AreEqual("설정", label.text, "재설정 → 설정 가이드(Reset=FirstSetup UI)");

            Object.DestroyImmediate(viewGo);
        }
    }
}
