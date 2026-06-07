using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowModalCommand, ModalRequest
using LoveAlgo.UI;     // ModalView, ChoiceSlot

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 범용 모달 뷰 PlayMode: ShowModalCommand 구독 → 제목/본문 세팅 + 버튼 동적 생성(ChoiceSlot 재사용),
    /// 버튼 클릭 → 핸들에 인덱스 채움 + 콜백 호출 + 루트 숨김(ADR-007 표시만). ModalView가 OnEnable에서
    /// 구독하므로 inactive GO에 바인딩한 뒤 활성화해 타이밍을 맞춘다(ChoiceView/TitleView 테스트와 동일).
    /// </summary>
    public class ModalViewPlayModeTests
    {
        // ChoiceSlot 버튼 프리팹을 런타임 구성(Button + 라벨). Instantiate 대상이라 활성 상태로 둔다.
        static ChoiceSlot MakeButtonPrefab()
        {
            var go = new GameObject("ModalButton", typeof(RectTransform), typeof(Button), typeof(ChoiceSlot));
            var slot = go.GetComponent<ChoiceSlot>();
            slot.Button = go.GetComponent<Button>();
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform);
            slot.LabelText = labelGo.AddComponent<TextMeshProUGUI>();
            return slot;
        }

        // ModalView를 완전 바인딩해 구성(inactive로 만들어 바인딩 후 활성화 → OnEnable 구독).
        static ModalView BuildView(out GameObject viewGo, out Transform container)
        {
            viewGo = new GameObject("ModalView");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<ModalView>();

            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(viewGo.transform);
            view.Root = root;

            var titleGo = new GameObject("Title", typeof(RectTransform));
            titleGo.transform.SetParent(root.transform);
            view.TitleText = titleGo.AddComponent<TextMeshProUGUI>();

            var msgGo = new GameObject("Message", typeof(RectTransform));
            msgGo.transform.SetParent(root.transform);
            view.MessageText = msgGo.AddComponent<TextMeshProUGUI>();

            var containerGo = new GameObject("Buttons", typeof(RectTransform));
            containerGo.transform.SetParent(root.transform);
            container = containerGo.transform;
            view.ButtonContainer = container;

            view.ButtonPrefab = MakeButtonPrefab();
            return view;
        }

        [UnityTest]
        public IEnumerator ShowModal_SetsText_AndCreatesButtons()
        {
            var view = BuildView(out var viewGo, out var container);
            viewGo.SetActive(true); // OnEnable → Subscribe<ShowModalCommand>
            yield return null;

            try
            {
                EventBus.Publish(new ShowModalCommand(
                    "게임 종료", "정말 종료하시겠습니까?",
                    new[] { "예", "아니오" }, new ModalRequest()));

                Assert.AreEqual("게임 종료", view.TitleText.text, "제목 세팅");
                Assert.AreEqual("정말 종료하시겠습니까?", view.MessageText.text, "본문 세팅");
                Assert.AreEqual(2, container.GetComponentsInChildren<Button>().Length, "라벨 수만큼 버튼 생성");
                Assert.IsTrue(view.Root.activeSelf, "표시 중 루트 활성");
            }
            finally
            {
                Object.DestroyImmediate(viewGo);
            }
        }

        [UnityTest]
        public IEnumerator ButtonClick_FillsHandle_InvokesCallback_HidesRoot()
        {
            var view = BuildView(out var viewGo, out var container);
            viewGo.SetActive(true);
            yield return null;

            int picked = -1;
            var handle = new ModalRequest(i => picked = i);
            try
            {
                EventBus.Publish(new ShowModalCommand(
                    "확인", "선택", new[] { "예", "아니오" }, handle));
                yield return null;

                var buttons = container.GetComponentsInChildren<Button>();
                buttons[1].onClick.Invoke(); // "아니오"(index 1)

                Assert.AreEqual(1, picked, "콜백에 클릭 인덱스 전달");
                Assert.AreEqual(1, handle.SelectedIndex, "핸들에 인덱스 회수");
                Assert.IsTrue(handle.IsComplete);
                Assert.IsFalse(view.Root.activeSelf, "선택 후 루트 숨김");
            }
            finally
            {
                Object.DestroyImmediate(viewGo);
            }
        }
    }
}
