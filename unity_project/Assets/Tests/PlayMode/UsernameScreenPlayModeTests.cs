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
        System.IDisposable _modalSub;
        ShowModalCommand _lastModal;
        bool _gotModal;

        [SetUp]
        public void SetUp()
        {
            // ModalView 없이 확인 모달 명령을 가로채 직접 Yes/No를 구동(저장은 모달 콜백 경유).
            _gotModal = false;
            _modalSub = EventBus.Subscribe<ShowModalCommand>(e => { _lastModal = e; _gotModal = true; });
        }

        [TearDown]
        public void TearDown()
        {
            _modalSub?.Dispose();
            _modalSub = null;
            if (_root != null) Object.DestroyImmediate(_root);
            if (_state != null) Object.DestroyImmediate(_state);
        }

        // 가로챈 확인 모달에서 버튼 선택(0=No, 1=Yes).
        void SelectModal(int index)
        {
            Assert.IsTrue(_gotModal, "확정 시 확인 모달이 발행돼야 함");
            _lastModal.Handle.Select(index);
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
        public IEnumerator EmptyInput_SavesDefaultName_AndComplete()
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
            Assert.IsTrue(string.IsNullOrEmpty(_state.Data.playerName), "Yes 전에는 저장 안 됨");
            Assert.IsFalse(handle.IsComplete, "Yes 전에는 핸들 미완료");
            StringAssert.Contains(view.DefaultName, _lastModal.Message, "모달 본문에 기본 이름 표시");

            SelectModal(1); // Yes
            Assert.AreEqual(view.DefaultName, _state.Data.playerName, "빈 입력(공백) → 기본 이름 저장");
            Assert.IsTrue(handle.IsComplete, "Yes → 핸들 완료");
            Assert.IsFalse(view.IsShown, "기본 이름 저장 후 숨김");
        }

        [UnityTest]
        public IEnumerator ValidInput_SaveNameAndComplete()
        {
            var view = CreateView();
            yield return null;

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowUsernameCommand(handle));
            Assert.IsTrue(view.IsShown, "표시 명령 → 표시");

            view.Input.text = " 철수 ";
            view.ConfirmButton.onClick.Invoke();
            StringAssert.Contains("철수", _lastModal.Message, "모달 본문에 트림된 이름 표시");
            Assert.IsFalse(handle.IsComplete, "Yes 전에는 핸들 미완료");

            SelectModal(1); // Yes
            Assert.AreEqual("철수", _state.Data.playerName, "트림된 이름 저장({{Player}} 치환 소스)");
            Assert.IsTrue(handle.IsComplete, "Yes → 핸들 완료(스토리 재개)");
            Assert.IsFalse(view.IsShown, "저장 후 숨김");
        }

        [UnityTest]
        public IEnumerator ConfirmNo_KeepsScreen_NoSave()
        {
            var view = CreateView();
            yield return null;

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowUsernameCommand(handle));

            view.Input.text = "철수";
            view.ConfirmButton.onClick.Invoke();
            SelectModal(0); // No → 재입력

            Assert.IsTrue(string.IsNullOrEmpty(_state.Data.playerName), "No → 저장 안 됨");
            Assert.IsFalse(handle.IsComplete, "No → 핸들 미완료(스토리 대기)");
            Assert.IsTrue(view.IsShown, "No → 화면 유지(재입력)");
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

            Assert.IsFalse(_gotModal, "무효 입력은 확인 모달도 안 띄움");
            Assert.IsTrue(string.IsNullOrEmpty(_state.Data.playerName), "무효 이름은 저장 안 됨");
            Assert.IsTrue(view.IsShown, "무효면 화면 유지");
            Assert.IsFalse(handle.IsComplete, "무효면 핸들 미완료(스토리 대기)");
            Assert.IsFalse(string.IsNullOrEmpty(view.ErrorLabel.text), "에러 메시지 표시");
        }

        [UnityTest]
        public IEnumerator EnterSubmit_SavesAndCompletes()
        {
            var view = CreateView();
            yield return null;

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowUsernameCommand(handle));
            Assert.IsTrue(view.IsShown);

            view.Input.text = "영희";
            view.Input.onSubmit.Invoke(view.Input.text); // 엔터 = 확정(버튼과 동일 경로)
            SelectModal(1); // Yes
            Assert.AreEqual("영희", _state.Data.playerName, "엔터로도 저장");
            Assert.IsTrue(handle.IsComplete, "엔터 → 핸들 완료");
            Assert.IsFalse(view.IsShown, "엔터 저장 후 숨김");
        }

        [UnityTest]
        public IEnumerator EnterEmpty_SavesDefaultName()
        {
            var view = CreateView();
            yield return null;

            var handle = new CompletionHandle();
            EventBus.Publish(new ShowUsernameCommand(handle));
            Assert.IsTrue(view.IsShown);

            view.Input.text = ""; // 입력 없이 엔터
            view.Input.onSubmit.Invoke(view.Input.text);
            SelectModal(1); // Yes
            Assert.AreEqual(view.DefaultName, _state.Data.playerName, "빈 엔터 → 기본 이름 저장");
            Assert.IsTrue(handle.IsComplete, "빈 엔터도 핸들 완료");
            Assert.IsFalse(view.IsShown, "저장 후 숨김");
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
