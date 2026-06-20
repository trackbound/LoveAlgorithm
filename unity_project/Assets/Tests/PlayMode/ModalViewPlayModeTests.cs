using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate
using LoveAlgo.Events; // ShowModalCommand, ModalRequest, ModalButton, ModalButtonKind
using LoveAlgo.UI;     // ModalView, ModalTemplate, ButtonSlot

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 범용 모달 뷰 PlayMode: 버튼 종류 시그니처로 템플릿 선택 → 정적 틀은 슬롯 Bind, 폴백 틀은 동적 스폰.
    /// 키 입력은 OverlayGate 경유(ESC=CloseTop=아니오, Enter=ConfirmTop=예) — 라우터 대신 게이트 API를 직접 구동해 검증.
    /// ModalView가 OnEnable에서 구독하므로 inactive GO에 바인딩 후 활성화해 타이밍을 맞춘다.
    /// </summary>
    public class ModalViewPlayModeTests
    {
        [SetUp] public void SetUp() => OverlayGate.Reset();
        [TearDown] public void TearDown() => OverlayGate.Reset();

        // ButtonSlot 슬롯 1개(Button + 라벨)를 가진 GameObject 생성.
        static ButtonSlot MakeSlot(Transform parent)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Button), typeof(ButtonSlot));
            go.transform.SetParent(parent, false);
            var slot = go.GetComponent<ButtonSlot>();
            slot.Button = go.GetComponent<Button>();
            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(go.transform, false);
            slot.LabelText = labelGo.AddComponent<TextMeshProUGUI>();
            return slot;
        }

        // 정적 틀 프리팹 역할(Instantiate 대상). signature/slots/message 배선.
        static ModalTemplate MakeStaticTemplate(string name, ModalButtonKind[] signature, int slotCount)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var tpl = go.AddComponent<ModalTemplate>();
            tpl.Signature = signature;
            var msgGo = new GameObject("Message", typeof(RectTransform));
            msgGo.transform.SetParent(go.transform, false);
            tpl.Message = msgGo.AddComponent<TextMeshProUGUI>();
            var slots = new ButtonSlot[slotCount];
            for (int i = 0; i < slotCount; i++) slots[i] = MakeSlot(go.transform);
            tpl.Slots = slots;
            return tpl;
        }

        // 폴백 틀 프리팹 역할: signature 빈 + dynamicContainer.
        static ModalTemplate MakeDynamicTemplate()
        {
            var go = new GameObject("Dynamic", typeof(RectTransform));
            var tpl = go.AddComponent<ModalTemplate>();
            tpl.Signature = new ModalButtonKind[0];
            var msgGo = new GameObject("Message", typeof(RectTransform));
            msgGo.transform.SetParent(go.transform, false);
            tpl.Message = msgGo.AddComponent<TextMeshProUGUI>();
            var cont = new GameObject("Buttons", typeof(RectTransform));
            cont.transform.SetParent(go.transform, false);
            tpl.DynamicContainer = cont.transform;
            tpl.Slots = new ButtonSlot[0];
            return tpl;
        }

        static ModalView BuildView(out GameObject viewGo, List<ModalTemplate> templates, ButtonSlot dynamicButtonPrefab)
        {
            viewGo = new GameObject("ModalView");
            viewGo.SetActive(false);
            var view = viewGo.AddComponent<ModalView>();

            var root = new GameObject("Root", typeof(RectTransform));
            root.transform.SetParent(viewGo.transform, false);
            view.Root = root;
            var container = new GameObject("TemplateContainer", typeof(RectTransform));
            container.transform.SetParent(root.transform, false);

            view.TemplateContainer = container.transform;
            view.TemplatePrefabs = templates;
            view.DefaultButtonPrefab = dynamicButtonPrefab;
            return view;
        }

        [UnityTest]
        public IEnumerator StaticTemplate_Selected_BindsSlots_AndReturnsIndex()
        {
            var yesNo = MakeStaticTemplate("YesNo", new[] { ModalButtonKind.No, ModalButtonKind.Yes }, 2);
            var dynamic = MakeDynamicTemplate();
            var view = BuildView(out var viewGo, new List<ModalTemplate> { yesNo, dynamic }, MakeSlot(null));
            viewGo.SetActive(true); // OnEnable → Subscribe
            yield return null;

            int picked = -1;
            var handle = new ModalRequest(i => picked = i);
            try
            {
                EventBus.Publish(new ShowModalCommand("종료", "정말?",
                    new[] { new ModalButton("아니오", ModalButtonKind.No), new ModalButton("예", ModalButtonKind.Yes) }, handle));
                yield return null;

                // 정적 틀(YesNo)이 인스턴스화되어 슬롯 라벨이 채워졌는지
                var slots = view.Root.GetComponentsInChildren<ButtonSlot>(true);
                Assert.AreEqual(2, slots.Length, "YesNo 틀 슬롯 2개");
                slots[1].Button.onClick.Invoke(); // 우(예, index 1)
                Assert.AreEqual(1, picked, "예(index 1) 클릭 → 핸들 회수");
                Assert.IsFalse(view.Root.activeSelf, "선택 후 루트 숨김");
            }
            finally { Object.DestroyImmediate(viewGo); }
        }

        [UnityTest]
        public IEnumerator NoMatch_FallsBackToDynamic_SpawnsByKind()
        {
            var yesNo = MakeStaticTemplate("YesNo", new[] { ModalButtonKind.No, ModalButtonKind.Yes }, 2);
            var dynamic = MakeDynamicTemplate();
            var view = BuildView(out var viewGo, new List<ModalTemplate> { yesNo, dynamic }, MakeSlot(null));
            viewGo.SetActive(true);
            yield return null;

            int picked = -1;
            var handle = new ModalRequest(i => picked = i);
            try
            {
                // 종류 Default 2개 → YesNo 미매칭 → 폴백(Dynamic)
                EventBus.Publish(new ShowModalCommand("확인", "선택",
                    new[] { new ModalButton("예"), new ModalButton("아니오") }, handle));
                yield return null;

                var spawned = view.Root.GetComponentsInChildren<ButtonSlot>(true);
                Assert.AreEqual(2, spawned.Length, "폴백 동적 스폰 2개");
                spawned[0].Button.onClick.Invoke();
                Assert.AreEqual(0, picked, "index 0 클릭 → 핸들 회수");
            }
            finally { Object.DestroyImmediate(viewGo); }
        }

        [UnityTest]
        public IEnumerator YesNo_EnterConfirmsYes_EscConfirmsNo_ViaGate()
        {
            var yesNo = MakeStaticTemplate("YesNo", new[] { ModalButtonKind.No, ModalButtonKind.Yes }, 2);
            var view = BuildView(out var viewGo, new List<ModalTemplate> { yesNo, MakeDynamicTemplate() }, MakeSlot(null));
            viewGo.SetActive(true);
            yield return null;

            try
            {
                // Enter(ConfirmTop) → 예(index 1)
                int picked = -1;
                EventBus.Publish(new ShowModalCommand("종료", "정말?",
                    new[] { new ModalButton("아니오", ModalButtonKind.No), new ModalButton("예", ModalButtonKind.Yes) },
                    new ModalRequest(i => picked = i)));
                yield return null;
                Assert.AreEqual(1, OverlayGate.Count, "모달 표시 중 게이트 1개(최상단)");

                Assert.IsTrue(OverlayGate.ConfirmTop(), "Enter → 최상단 모달 확정");
                Assert.AreEqual(1, picked, "Enter = 예(index 1)");
                Assert.IsFalse(view.Root.activeSelf, "확정 후 숨김");
                Assert.AreEqual(0, OverlayGate.Count, "닫히며 게이트 해제");

                // ESC(CloseTop) → 아니오(index 0)
                picked = -1;
                EventBus.Publish(new ShowModalCommand("종료", "정말?",
                    new[] { new ModalButton("아니오", ModalButtonKind.No), new ModalButton("예", ModalButtonKind.Yes) },
                    new ModalRequest(i => picked = i)));
                yield return null;

                Assert.IsTrue(OverlayGate.CloseTop(), "ESC → 최상단 모달 닫기");
                Assert.AreEqual(0, picked, "ESC = 아니오(index 0)");
                Assert.AreEqual(0, OverlayGate.Count, "닫히며 게이트 해제");
            }
            finally { Object.DestroyImmediate(viewGo); }
        }

        [UnityTest]
        public IEnumerator SingleButton_EnterAndEsc_BothConfirmThatButton()
        {
            // 단일 확인(Close) 버튼 모달: Enter·ESC 모두 그 버튼을 선택해야 한다.
            var ok = MakeStaticTemplate("OK", new[] { ModalButtonKind.Close }, 1);
            var view = BuildView(out var viewGo, new List<ModalTemplate> { ok, MakeDynamicTemplate() }, MakeSlot(null));
            viewGo.SetActive(true);
            yield return null;

            try
            {
                int picked = -1;
                EventBus.Publish(new ShowModalCommand("알림", "완료",
                    new[] { new ModalButton("확인", ModalButtonKind.Close) },
                    new ModalRequest(i => picked = i)));
                yield return null;

                Assert.IsTrue(OverlayGate.ConfirmTop(), "단일 버튼 모달 Enter 확정");
                Assert.AreEqual(0, picked, "Enter = 그 버튼(index 0)");
            }
            finally { Object.DestroyImmediate(viewGo); }
        }
    }
}
