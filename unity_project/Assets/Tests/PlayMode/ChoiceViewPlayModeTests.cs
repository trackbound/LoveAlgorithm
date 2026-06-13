using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowChoiceCommand, ChoiceRequest
using LoveAlgo.UI;     // ChoiceView, ChoiceSlot

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// ChoiceView 런타임 계약 — 특히 **연속 선택**(2026-06-12 프롤로그 2번째 선택지 무반응 회귀):
    /// 선택 후에도 구독이 살아 다음 ShowChoiceCommand를 받아야 한다(숨김 = root "자식" 토글, 뷰 GO 불변).
    /// + 부팅 숨김(authored-active 방어) + root 자기-바인딩 가드.
    /// </summary>
    public class ChoiceViewPlayModeTests
    {
        GameObject _holder;

        [TearDown]
        public void TearDown()
        {
            if (_holder != null) Object.DestroyImmediate(_holder);
        }

        ChoiceView CreateView(out GameObject visualRoot)
        {
            _holder = new GameObject("ChoiceView", typeof(RectTransform));
            _holder.SetActive(false); // Awake 전 주입

            var view = _holder.AddComponent<ChoiceView>();
            visualRoot = new GameObject("Root", typeof(RectTransform));
            visualRoot.transform.SetParent(_holder.transform, false);
            var container = new GameObject("Container", typeof(RectTransform));
            container.transform.SetParent(visualRoot.transform, false);

            // 슬롯 템플릿(활성 보관 — 비활성 원본 클론은 Awake가 안 돈다, Shop 테스트 선례).
            // 직렬화 button 바인딩 — Instantiate가 클론 내부 참조로 재매핑한다.
            var slotTemplate = new GameObject("ChoiceSlotTemplate", typeof(RectTransform), typeof(Button));
            slotTemplate.transform.SetParent(_holder.transform, false);
            var slot = slotTemplate.AddComponent<ChoiceSlot>();
            slot.Button = slotTemplate.GetComponent<Button>();

            view.Root = visualRoot;
            view.SlotContainer = container.transform;
            view.SlotPrefab = slot;
            _holder.SetActive(true); // Awake(부팅 숨김) + OnEnable(구독)
            return view;
        }

        static ChoiceSlot[] SpawnedSlots(ChoiceView view)
            => view.SlotContainer.GetComponentsInChildren<ChoiceSlot>(true);

        [UnityTest]
        public IEnumerator ConsecutiveChoices_SecondShowStillWorks()
        {
            var view = CreateView(out var root);
            yield return null;

            Assert.IsFalse(root.activeSelf, "부팅 시 비주얼 숨김(authored-active 방어)");

            // 1번째 선택
            var first = new ChoiceRequest();
            EventBus.Publish(new ShowChoiceCommand(new[] { "A", "B" }, first));
            Assert.IsTrue(root.activeSelf, "1번째 선택지 표시");
            var slots = SpawnedSlots(view);
            Assert.AreEqual(2, slots.Length, "옵션 2개 스폰");

            slots[0].GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(first.IsComplete, "선택 → 핸들 완료");
            Assert.AreEqual(0, first.SelectedIndex);
            Assert.IsFalse(root.activeSelf, "선택 후 비주얼 숨김");
            Assert.IsTrue(view.gameObject.activeSelf, "뷰 GO는 살아있음(구독 유지) — 자기-끄기 금지");
            yield return null; // 지연 파괴 정리

            // 2번째 선택 — 회귀 핵심: 구독이 살아 다시 떠야 한다
            var second = new ChoiceRequest();
            EventBus.Publish(new ShowChoiceCommand(new[] { "C", "D", "E" }, second));
            Assert.IsTrue(root.activeSelf, "2번째 선택지 표시(프롤로그 pro_059 회귀)");
            slots = SpawnedSlots(view);
            Assert.AreEqual(3, slots.Length, "옵션 3개 스폰(이전 슬롯 정리 포함)");

            slots[2].GetComponent<Button>().onClick.Invoke();
            Assert.IsTrue(second.IsComplete && second.SelectedIndex == 2, "2번째 선택 정상 완료");
        }

        [UnityTest]
        public IEnumerator SelfBoundRoot_GuardedWithError_SubscriptionSurvives()
        {
            _holder = new GameObject("ChoiceView", typeof(RectTransform));
            _holder.SetActive(false);
            var view = _holder.AddComponent<ChoiceView>();
            view.Root = _holder; // 사고 재현: root=자기 자신

            LogAssert.Expect(LogType.Error, new Regex("자신으로 바인딩"));
            _holder.SetActive(true); // Awake 가드 발동
            yield return null;

            Assert.IsTrue(_holder.activeSelf, "가드 덕에 GO 생존(구독 유지)");
            Assert.IsNull(view.Root, "치명 토글 대상 무장해제");
        }
    }
}
