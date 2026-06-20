using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events; // ShowUsernameCommand, CompletionHandle, NarrativeFinishedEvent
using LoveAlgo.UI;     // UsernameScreenView

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 이름 입력 화면 계약(스토리 Flow Username — LockScreen 미러): 표시→빈 입력 차단→유효 입력 저장+핸들 완료+숨김,
    /// 내러티브 종료 안전망(핸들 해제). 부팅 비주얼 숨김(authored-active 방어).
    /// </summary>
    public class UsernameScreenPlayModeTests
    {
        GameObject _root;
        GameStateSO _state;

        [TearDown]
        public void TearDown()
        {
            if (_root != null) Object.DestroyImmediate(_root);
            if (_state != null) Object.DestroyImmediate(_state);
        }

        UsernameScreenView CreateView()
        {
            // 상주 Game 씬 인스턴스 중화 — 같은 명령에 함께 반응하면 핸들/저장 이중 처리.
            foreach (var v in Object.FindObjectsByType<UsernameScreenView>(FindObjectsInactive.Include))
                Object.DestroyImmediate(v.gameObject);

            _state = ScriptableObject.CreateInstance<GameStateSO>();
            _root = new GameObject("UsernameScreen", typeof(RectTransform));
            _root.SetActive(false);

            var view = _root.AddComponent<UsernameScreenView>();
            var overlay = new GameObject("Box", typeof(RectTransform));
            overlay.transform.SetParent(_root.transform, false);
            var inputGo = new GameObject("Input", typeof(RectTransform));
            inputGo.transform.SetParent(overlay.transform, false);
            var input = inputGo.AddComponent<TMP_InputField>();
            var textArea = new GameObject("Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            textArea.transform.SetParent(inputGo.transform, false);
            input.textComponent = textArea;
            var confirmGo = new GameObject("Confirm", typeof(RectTransform), typeof(Button));
            confirmGo.transform.SetParent(overlay.transform, false);

            view.State = _state;
            view.Overlay = overlay;
            view.Input = input;
            view.ConfirmButton = confirmGo.GetComponent<Button>();
            var errGo = new GameObject("Error", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            errGo.transform.SetParent(overlay.transform, false);
            view.ErrorLabel = errGo;
            _root.SetActive(true); // Awake(부팅 숨김)+OnEnable(구독)
            return view;
        }

        [UnityTest]
        public IEnumerator Show_RejectEmpty_SaveNameAndComplete()
        {
            var view = CreateView();
            yield return null;

            Assert.IsFalse(view.IsShown, "부팅 시 비주얼 숨김");

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowUsernameCommand(handle));
            Assert.IsTrue(view.IsShown, "표시 명령 → 표시");
            Assert.IsFalse(handle.IsComplete, "입력 전 핸들 미완료(엔진 대기)");

            view.Input.text = "   ";
            view.ConfirmButton.onClick.Invoke();
            Assert.IsTrue(view.IsShown, "빈 입력(공백) → 확인 무시(입력 강제)");
            Assert.IsFalse(handle.IsComplete);

            view.Input.text = " 철수 ";
            view.ConfirmButton.onClick.Invoke();
            Assert.AreEqual("철수", _state.Data.playerName, "트림된 이름 저장({{Player}} 치환 소스)");
            Assert.IsTrue(handle.IsComplete, "확인 → 핸들 완료(스토리 재개)");
            Assert.IsFalse(view.IsShown, "저장 후 숨김");
        }

        [UnityTest]
        public IEnumerator InvalidName_NotSaved_AndErrorShown()
        {
            var view = CreateView();
            yield return null;

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowUsernameCommand(handle));
            Assert.IsTrue(view.IsShown);

            view.Input.text = "ㄱㄴ"; // 비문(미완성 자모) → InvalidCharacter
            view.ConfirmButton.onClick.Invoke();

            Assert.IsTrue(string.IsNullOrEmpty(_state.Data.playerName), "무효 이름은 저장 안 됨");
            Assert.IsTrue(view.IsShown, "무효면 화면 유지");
            Assert.IsFalse(handle.IsComplete, "무효면 핸들 미완료(스토리 대기)");
            Assert.IsFalse(string.IsNullOrEmpty(view.ErrorLabel.text), "에러 메시지 표시");
        }

        [UnityTest]
        public IEnumerator NarrativeFinished_SafetyRelease()
        {
            var view = CreateView();
            yield return null;

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowUsernameCommand(handle));
            Assert.IsTrue(view.IsShown);

            EventBus.Publish(new NarrativeFinishedEvent("pro"));
            Assert.IsFalse(view.IsShown, "내러티브 종료 → 즉시 숨김(LockScreen 미러)");
            Assert.IsTrue(handle.IsComplete, "미완료 핸들 해제(hang 방지)");
        }
    }
}
