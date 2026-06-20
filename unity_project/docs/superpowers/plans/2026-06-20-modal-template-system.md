# 모달 템플릿 시스템 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 자주 쓰는 모달 모양(yes/no, yes-only)을 버튼까지 박힌 정교한 템플릿 프리팹으로 고정하고, ModalView가 버튼 종류 시그니처로 템플릿을 라우팅하되 매칭 없으면 기존 동적 표시로 폴백해 모든 소비처를 무변경으로 유지한다.

**Architecture:** `ModalTemplate` 컴포넌트가 틀의 약속(signature·title·message·slots·dynamicContainer)을 노출. ModalView는 직렬화된 템플릿 프리팹 리스트에서 명령의 버튼 종류열과 일치하는 정적 틀을 골라 인스턴스화·슬롯 바인딩하고, 매칭이 없으면 빈 시그니처(폴백) 템플릿에 종류별 버튼을 동적 스폰한다. 셸(Modal.prefab)은 Dim+TemplateContainer만 보유.

**Tech Stack:** Unity 6 LTS, C#, uGUI, TextMeshPro, NUnit + Unity Test Framework, EventBus(`LoveAlgo.Common`), `ShowModalCommand`/`ModalButton`/`ModalButtonKind`/`ModalRequest`(`LoveAlgo.Events`), `ChoiceSlot`(`LoveAlgo.UI`).

## Global Constraints

- `ShowModalCommand`/`ModalButton`/`ModalButtonKind`/`ModalRequest` API는 **변경하지 않는다**(소비처 무변경).
- 피처 간 통신은 EventBus만(ADR-007). Obsolete API 금지(Unity 6).
- 로깅: 일반 `Log.Info/Warn`, 사용자 보고용 에러만 `Debug.LogError`.
- 한 기능 = 한 커밋(Atomic). 커밋 메시지 본문에 "왜(Why)". 끝에 `Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>`.
- 신규 코드 위치: `Assets/_Project/Scripts/UI/`. 테스트: EditMode=`Assets/Tests/EditMode/`, PlayMode=`Assets/Tests/PlayMode/`.
- `ModalButtonKind` 값: `Default, Yes, No, Close`. 폴백 템플릿 = `signature` 빈 배열 하나(정확히 1개).
- 시그니처 매칭은 **종류 순서**(좌→우) 일치. 소비처 관례 No-좌/Yes-우 준수.

---

### Task 1: ModalTemplate 컴포넌트 + 시그니처 매칭 순수 로직 (EditMode TDD)

틀의 약속을 노출하는 `ModalTemplate` 컴포넌트와, 명령 종류열 → 템플릿 인덱스 선택의 순수 정적 함수를 만든다. ModalView는 이 단계에서 건드리지 않는다.

**Files:**
- Create: `Assets/_Project/Scripts/UI/ModalTemplate.cs`
- Test: `Assets/Tests/EditMode/ModalTemplateMatchTests.cs`

**Interfaces:**
- Consumes: `LoveAlgo.Events.ModalButtonKind`, `LoveAlgo.UI.ChoiceSlot`.
- Produces (Task 2가 의존):
  - `class ModalTemplate : MonoBehaviour` — 프로퍼티 `Signature`(`ModalButtonKind[]`), `Title`/`Message`(`TMP_Text`), `Slots`(`ChoiceSlot[]`), `DynamicContainer`(`Transform`), `IsStatic`(`bool`, slots 비지 않으면 true). 모두 `{ get; set; }`.
  - `static int ModalTemplate.MatchTemplate(IReadOnlyList<ModalButtonKind> commandKinds, IReadOnlyList<ModalButtonKind[]> signatures)` — 정확 매칭 인덱스 우선, 없으면 첫 빈-시그니처(폴백) 인덱스, 둘 다 없으면 -1.

- [ ] **Step 1: Write the failing test**

`Assets/Tests/EditMode/ModalTemplateMatchTests.cs`:

```csharp
using NUnit.Framework;
using LoveAlgo.UI;
using LoveAlgo.Events;

namespace LoveAlgo.Tests.EditMode
{
    /// <summary>
    /// ModalTemplate.MatchTemplate 순수 선택 로직(GameObject 불필요). 정확 매칭 우선·없으면 폴백(빈 시그니처)·
    /// 폴백도 없으면 -1. 어댑터(ModalView 인스턴스화/바인딩)는 PlayMode에서 검증.
    /// </summary>
    public class ModalTemplateMatchTests
    {
        static ModalButtonKind[][] Sigs() => new[]
        {
            new[] { ModalButtonKind.No, ModalButtonKind.Yes }, // 0 = YesNo
            new[] { ModalButtonKind.Yes },                     // 1 = YesOnly
            new ModalButtonKind[0],                            // 2 = 폴백
        };

        [Test]
        public void Match_ExactSignature_ReturnsThatIndex()
        {
            Assert.AreEqual(0, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.No, ModalButtonKind.Yes }, Sigs()));
            Assert.AreEqual(1, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.Yes }, Sigs()));
        }

        [Test]
        public void Match_NoExact_ReturnsFallbackIndex()
        {
            // 종류 다름(Default), Close, 순서 뒤바뀜 → 모두 폴백(2)
            Assert.AreEqual(2, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.Default, ModalButtonKind.Default }, Sigs()));
            Assert.AreEqual(2, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.Close }, Sigs()));
            Assert.AreEqual(2, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.Yes, ModalButtonKind.No }, Sigs())); // 순서 역
            Assert.AreEqual(2, ModalTemplate.MatchTemplate(new ModalButtonKind[0], Sigs()));
        }

        [Test]
        public void Match_NoFallbackAvailable_ReturnsMinusOne()
        {
            var noFallback = new[] { new[] { ModalButtonKind.Yes } };
            Assert.AreEqual(-1, ModalTemplate.MatchTemplate(new[] { ModalButtonKind.No, ModalButtonKind.Yes }, noFallback));
        }
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Unity Test Runner(EditMode) → `ModalTemplateMatchTests`.
Expected: 컴파일 에러 — `ModalTemplate` 미정의.

- [ ] **Step 3: Write minimal implementation**

`Assets/_Project/Scripts/UI/ModalTemplate.cs`:

```csharp
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using LoveAlgo.Events; // ModalButtonKind

namespace LoveAlgo.UI
{
    /// <summary>
    /// 모달 "틀"의 약속. ModalView가 명령의 버튼 종류열로 틀을 고르고(시그니처), 인스턴스화 후 title/message를 채운다.
    /// 정적 틀은 미리 배치된 <see cref="Slots"/>(버튼 스킨 박힘)에 라벨·콜백만 Bind. 폴백 틀(시그니처 빈 배열)은
    /// <see cref="DynamicContainer"/>에 ModalView가 종류별 버튼을 동적 스폰한다.
    /// </summary>
    public class ModalTemplate : MonoBehaviour
    {
        [Tooltip("이 틀이 담당하는 버튼 종류 순서(예: [No,Yes], [Yes]). 빈 배열 = 동적 폴백 틀.")]
        [SerializeField] ModalButtonKind[] signature;
        [Tooltip("제목 TMP(선택, 미바인딩 시 제목 생략).")]
        [SerializeField] TMP_Text title;
        [Tooltip("본문 TMP(선택).")]
        [SerializeField] TMP_Text message;
        [Tooltip("정적 틀: 미리 배치된 버튼 슬롯(좌→우). 폴백이면 비움.")]
        [SerializeField] ChoiceSlot[] slots;
        [Tooltip("폴백 전용: 종류별 버튼을 스폰할 컨테이너. 정적 틀이면 비움.")]
        [SerializeField] Transform dynamicContainer;

        public ModalButtonKind[] Signature { get => signature; set => signature = value; }
        public TMP_Text Title { get => title; set => title = value; }
        public TMP_Text Message { get => message; set => message = value; }
        public ChoiceSlot[] Slots { get => slots; set => slots = value; }
        public Transform DynamicContainer { get => dynamicContainer; set => dynamicContainer = value; }

        /// <summary>정적 틀이면 true(slots 사용), 폴백이면 false(dynamicContainer 사용).</summary>
        public bool IsStatic => slots != null && slots.Length > 0;

        /// <summary>
        /// 명령 종류열 → 선택할 템플릿 인덱스. 정확 매칭(순서·길이 일치) 우선, 없으면 첫 빈-시그니처(폴백) 인덱스,
        /// 둘 다 없으면 -1. GameObject 불필요(EditMode 테스트 대상).
        /// </summary>
        public static int MatchTemplate(IReadOnlyList<ModalButtonKind> commandKinds, IReadOnlyList<ModalButtonKind[]> signatures)
        {
            int fallback = -1;
            for (int t = 0; t < signatures.Count; t++)
            {
                var sig = signatures[t];
                if (sig == null || sig.Length == 0) { if (fallback < 0) fallback = t; continue; } // 폴백 후보
                if (commandKinds == null || sig.Length != commandKinds.Count) continue;
                bool match = true;
                for (int i = 0; i < sig.Length; i++)
                    if (sig[i] != commandKinds[i]) { match = false; break; }
                if (match) return t;
            }
            return fallback;
        }
    }
}
```

- [ ] **Step 4: Run test to verify it passes**

Unity Test Runner(EditMode) → `ModalTemplateMatchTests`. Expected: 3 PASS.

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/ModalTemplate.cs Assets/_Project/Scripts/UI/ModalTemplate.cs.meta Assets/Tests/EditMode/ModalTemplateMatchTests.cs Assets/Tests/EditMode/ModalTemplateMatchTests.cs.meta
git commit -m "$(cat <<'EOF'
feat(ui): ModalTemplate 컴포넌트 + 시그니처 매칭 순수 로직

왜: 모달 모양을 정교한 틀로 고정하기 위한 첫 단계 — 틀의 약속(signature/slots/
dynamicContainer)과 명령 종류열→틀 선택의 순수 함수를 먼저 두고 EditMode로 고정한다.
ModalView 리팩터 전에 정확매칭/폴백/미존재 규칙을 검증.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

### Task 2: ModalView 리팩터 — 템플릿 라우팅 + 정적/동적 바인딩 (PlayMode TDD)

ModalView를 템플릿 프리팹 리스트 기반으로 바꾼다. 시그니처로 템플릿을 골라 인스턴스화하고, 정적 틀은 슬롯 Bind, 폴백 틀은 종류별 버튼 동적 스폰. 기존 인플레이스 Title/Message/Buttons 직렬화는 제거(이제 템플릿이 보유)하고, 종류→프리팹 매핑(buttonPrefabs/defaultButtonPrefab)은 폴백 스폰용으로 유지.

**Files:**
- Modify: `Assets/_Project/Scripts/UI/ModalView.cs` (전면 교체)
- Modify: `Assets/Tests/PlayMode/ModalViewPlayModeTests.cs` (새 구조에 맞게 재작성)

**Interfaces:**
- Consumes: Task 1의 `ModalTemplate`(`Signature`/`Title`/`Message`/`Slots`/`DynamicContainer`/`IsStatic`/`MatchTemplate`), `ChoiceSlot.Bind(int,string,Action<int>)`, `ShowModalCommand`/`ModalButton`/`ModalButtonKind`/`ModalRequest`.
- Produces (Task 3 배선이 의존): ModalView 직렬화 필드 `root`(GameObject), `templateContainer`(Transform), `templatePrefabs`(List<ModalTemplate>), `buttonPrefabs`(List<KindPrefab>), `defaultButtonPrefab`(ChoiceSlot). public `GameObject Root { get; set; }`. public `void OnShow(ShowModalCommand e)`.

- [ ] **Step 1: Rewrite the PlayMode tests (failing)**

`Assets/Tests/PlayMode/ModalViewPlayModeTests.cs` 전체를 교체:

```csharp
using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowModalCommand, ModalRequest, ModalButton, ModalButtonKind
using LoveAlgo.UI;     // ModalView, ModalTemplate, ChoiceSlot
using System.Collections.Generic;

namespace LoveAlgo.Tests.PlayMode
{
    /// <summary>
    /// 범용 모달 뷰 PlayMode: 버튼 종류 시그니처로 템플릿 선택 → 정적 틀은 슬롯 Bind, 폴백 틀은 동적 스폰.
    /// ModalView가 OnEnable에서 구독하므로 inactive GO에 바인딩 후 활성화해 타이밍을 맞춘다.
    /// </summary>
    public class ModalViewPlayModeTests
    {
        // ChoiceSlot 슬롯 1개(Button + 라벨)를 가진 GameObject 생성.
        static ChoiceSlot MakeSlot(Transform parent)
        {
            var go = new GameObject("Btn", typeof(RectTransform), typeof(Button), typeof(ChoiceSlot));
            go.transform.SetParent(parent, false);
            var slot = go.GetComponent<ChoiceSlot>();
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
            var slots = new ChoiceSlot[slotCount];
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
            tpl.Slots = new ChoiceSlot[0];
            return tpl;
        }

        static ModalView BuildView(out GameObject viewGo, List<ModalTemplate> templates, ChoiceSlot dynamicButtonPrefab)
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
                var slots = view.Root.GetComponentsInChildren<ChoiceSlot>(true);
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

                var spawned = view.Root.GetComponentsInChildren<ChoiceSlot>(true);
                Assert.AreEqual(2, spawned.Length, "폴백 동적 스폰 2개");
                spawned[0].Button.onClick.Invoke();
                Assert.AreEqual(0, picked, "index 0 클릭 → 핸들 회수");
            }
            finally { Object.DestroyImmediate(viewGo); }
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Unity Test Runner(PlayMode) → `ModalViewPlayModeTests`.
Expected: 컴파일 에러(`TemplateContainer`/`TemplatePrefabs` 미정의) 또는 FAIL.

> MCP 함정([[unity-mcp-test-and-execute-gotchas]]): 컴파일 깨진 채 두면 Safe Mode 모달이 MCP를 막는다. Step 3 구현을 곧바로 작성해 컴파일을 복구한 뒤 테스트.

- [ ] **Step 3: Rewrite ModalView**

`Assets/_Project/Scripts/UI/ModalView.cs` 전체 교체:

```csharp
using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus, DebugInput
using LoveAlgo.Events; // ShowModalCommand, ModalRequest, ModalButton, ModalButtonKind
using UnityEngine;
using UnityEngine.InputSystem; // Keyboard (Esc 모달 취소)

namespace LoveAlgo.UI
{
    /// <summary>
    /// 범용 모달 표시 뷰(*View). <see cref="ShowModalCommand"/>를 구독해 버튼 종류 시그니처로 템플릿을 고르고
    /// 인스턴스화한다(<see cref="ModalTemplate"/>). 정적 틀은 미리 배치된 슬롯에 라벨·콜백만 Bind, 매칭 없으면
    /// 빈 시그니처(폴백) 틀에 종류별 버튼을 동적 스폰(<see cref="ChoiceSlot"/> 재사용). 클릭/Esc 시 완료 핸들
    /// (<see cref="ModalRequest"/>)에 인덱스를 채운다(ADR-007: 표시만, 의미·아트 분리). 단일 모달 — 새 명령 시 재생성.
    /// </summary>
    public class ModalView : MonoBehaviour
    {
        /// <summary>버튼 종류 → 스타일 프리팹 매핑 1건(폴백 동적 스폰 전용).</summary>
        [Serializable]
        public struct KindPrefab
        {
            public ModalButtonKind kind;
            public ChoiceSlot prefab;
        }

        [Tooltip("모달 비주얼 루트(딤+템플릿 컨테이너). 표시 중에만 활성. 미바인딩 시 토글 생략.")]
        [SerializeField] GameObject root;
        [Tooltip("선택된 템플릿이 인스턴스화되는 컨테이너.")]
        [SerializeField] Transform templateContainer;
        [Tooltip("템플릿 프리팹 리스트(YesNo/YesOnly/Dynamic). 정확히 하나는 signature 빈 배열=폴백.")]
        [SerializeField] List<ModalTemplate> templatePrefabs = new();
        [Tooltip("폴백 동적 스폰: 종류별 버튼 프리팹.")]
        [SerializeField] List<KindPrefab> buttonPrefabs = new();
        [Tooltip("폴백 동적 스폰: 종류 매칭 없을 때 버튼 프리팹.")]
        [SerializeField] ChoiceSlot defaultButtonPrefab;

        public GameObject Root { get => root; set => root = value; }
        public Transform TemplateContainer { get => templateContainer; set => templateContainer = value; }
        public List<ModalTemplate> TemplatePrefabs { get => templatePrefabs; set => templatePrefabs = value; }
        public List<KindPrefab> ButtonPrefabs => buttonPrefabs;
        public ChoiceSlot DefaultButtonPrefab { get => defaultButtonPrefab; set => defaultButtonPrefab = value; }

        IDisposable _sub;
        ModalRequest _active;
        IReadOnlyList<ModalButton> _buttons;
        ModalTemplate _activeTemplate;            // 인스턴스화된 템플릿(닫을 때 Destroy)
        readonly List<ChoiceSlot> _boundSlots = new(); // Esc 취소 인덱스·라벨 로그용

        void Awake()
        {
            if (root == gameObject)
            {
                Debug.LogError("[ModalView] root가 모달 GO 자신으로 바인딩 — 비주얼 자식(딤+컨테이너)을 바인딩해야 한다. 부팅 숨김 생략.");
                return;
            }
            if (root != null) root.SetActive(false); // authored-active 비주얼을 플레이 시작 시 숨김
        }

        void OnEnable() => _sub = EventBus.Subscribe<ShowModalCommand>(OnShow);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
            Clear();
            if (root != null) root.SetActive(false);
        }

        /// <summary>모달 표시 — 시그니처로 템플릿 선택 → 인스턴스화 → 제목/본문 + 슬롯 바인딩/동적 스폰. 직접 호출도 가능(테스트).</summary>
        public void OnShow(ShowModalCommand e)
        {
            Clear();
            _active = e.Handle;
            _buttons = e.Buttons;
            if (root != null) root.SetActive(true);

            if (templateContainer == null)
            {
                Debug.LogError("[ModalView] templateContainer 미바인딩 — 모달 표시 불가.");
                return;
            }

            int idx = ModalTemplate.MatchTemplate(KindsOf(e.Buttons), SignaturesOf());
            if (idx < 0)
            {
                Debug.LogError("[ModalView] 매칭 템플릿도 폴백(빈 시그니처)도 없음 — 모달 표시 불가.");
                return;
            }

            _activeTemplate = Instantiate(templatePrefabs[idx], templateContainer);
            if (_activeTemplate.Title != null) _activeTemplate.Title.text = e.Title ?? "";
            if (_activeTemplate.Message != null) _activeTemplate.Message.text = e.Message ?? "";

            if (e.Buttons == null) return;
            if (_activeTemplate.IsStatic) BindStatic(_activeTemplate, e.Buttons);
            else SpawnDynamic(_activeTemplate, e.Buttons);
        }

        // 정적 틀: 미리 배치된 슬롯에 라벨·콜백 Bind(스킨은 박힘). 슬롯 수 부족분은 로그.
        void BindStatic(ModalTemplate tpl, IReadOnlyList<ModalButton> buttons)
        {
            if (tpl.Slots.Length != buttons.Count)
                Debug.LogError($"[ModalView] 정적 틀 슬롯 수({tpl.Slots.Length}) ≠ 버튼 수({buttons.Count}) — 가능한 만큼 바인딩.");
            int n = Mathf.Min(tpl.Slots.Length, buttons.Count);
            for (int i = 0; i < n; i++)
            {
                if (tpl.Slots[i] == null) continue;
                tpl.Slots[i].Bind(i, buttons[i].Label, OnSelected);
                _boundSlots.Add(tpl.Slots[i]);
            }
        }

        // 폴백 틀: 종류별 버튼 프리팹을 dynamicContainer에 스폰·Bind(기존 동작 보존).
        void SpawnDynamic(ModalTemplate tpl, IReadOnlyList<ModalButton> buttons)
        {
            if (tpl.DynamicContainer == null)
            {
                Debug.LogError("[ModalView] 폴백 틀에 dynamicContainer 미바인딩 — 버튼 표시 불가.");
                return;
            }
            for (int i = 0; i < buttons.Count; i++)
            {
                var prefab = PrefabFor(buttons[i].Kind);
                if (prefab == null)
                {
                    Debug.LogError($"[ModalView] 버튼 프리팹 없음(종류={buttons[i].Kind}, 폴백도 미바인딩) — 버튼 생략.");
                    continue;
                }
                var slot = Instantiate(prefab, tpl.DynamicContainer);
                slot.Bind(i, buttons[i].Label, OnSelected);
                _boundSlots.Add(slot);
            }
        }

        static IReadOnlyList<ModalButtonKind> KindsOf(IReadOnlyList<ModalButton> buttons)
        {
            var kinds = new ModalButtonKind[buttons?.Count ?? 0];
            for (int i = 0; i < kinds.Length; i++) kinds[i] = buttons[i].Kind;
            return kinds;
        }

        IReadOnlyList<ModalButtonKind[]> SignaturesOf()
        {
            var sigs = new ModalButtonKind[templatePrefabs.Count][];
            for (int i = 0; i < sigs.Length; i++)
                sigs[i] = templatePrefabs[i] != null ? templatePrefabs[i].Signature : null;
            return sigs;
        }

        ChoiceSlot PrefabFor(ModalButtonKind kind)
        {
            for (int i = 0; i < buttonPrefabs.Count; i++)
                if (buttonPrefabs[i].kind == kind && buttonPrefabs[i].prefab != null)
                    return buttonPrefabs[i].prefab;
            return defaultButtonPrefab;
        }

        void OnSelected(int index)
        {
            DebugInput.Log($"좌클릭 → 모달 확인: index={index}{LabelSuffix(index)}");
            Close(index);
        }

        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _active == null || !kb.escapeKey.wasPressedThisFrame) return;
            int cancel = _boundSlots.Count - 1;
            if (cancel < 0) return;
            DebugInput.Log($"Esc → 모달 취소: index={cancel}{LabelSuffix(cancel)}");
            Close(cancel);
        }

        void Close(int index)
        {
            var handle = _active;
            _active = null;
            _buttons = null;
            Clear();
            if (root != null) root.SetActive(false);
            handle?.Select(index);
        }

        string LabelSuffix(int index)
            => (_buttons != null && index >= 0 && index < _buttons.Count) ? $" '{_buttons[index].Label}'" : "";

        void Clear()
        {
            _boundSlots.Clear();
            if (_activeTemplate != null) Destroy(_activeTemplate.gameObject);
            _activeTemplate = null;
        }
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Unity Test Runner(PlayMode) → `ModalViewPlayModeTests`(2) + EditMode `ModalTemplateMatchTests`(3).
Expected: 모두 PASS. 다른 테스트 회귀는 Task 3 프리팹 배선 후 확인(현재 씬 Modal.prefab은 아직 구 구조라 `GameSceneOverlayBoot`/`TitleView` 모달 경로는 Task 3에서 검증).

- [ ] **Step 5: Commit**

```bash
git add Assets/_Project/Scripts/UI/ModalView.cs Assets/Tests/PlayMode/ModalViewPlayModeTests.cs
git commit -m "$(cat <<'EOF'
feat(ui): ModalView 템플릿 라우팅 리팩터(정적 틀 Bind/동적 폴백)

왜: 모달 모양을 정교한 틀로 고정하되 제네릭 명령 계약을 보존하기 위해, ModalView를
종류 시그니처→템플릿 선택 구조로 바꾼다. 정적 틀은 박힌 슬롯에 라벨·콜백만 Bind,
매칭 없으면 빈 시그니처(폴백) 틀에 종류별 버튼 동적 스폰(기존 동작 보존). 인플레이스
Title/Message/Buttons 직렬화는 템플릿으로 이관. PlayMode 2 + EditMode 3 통과.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

### Task 3: 프리팹 재구성 + 배선 + 회귀 (Unity 에디터/MCP)

`Modal.prefab`을 셸(Dim+TemplateContainer)로 줄이고, 현재 내용을 `ModalDynamic.prefab`(폴백)으로 추출, `ModalYesNo`/`ModalYesOnly`를 정교 제작, ModalView.templatePrefabs를 배선한다. **프리팹/씬 작업은 Unity 에디터(또는 Unity MCP)** — YAML 수기 편집 금지.

**Files:**
- Modify(에디터): `Assets/_Project/Prefabs/UI/Modal.prefab`
- Create(에디터): `Assets/_Project/Prefabs/UI/ModalDynamic.prefab`, `ModalYesNo.prefab`, `ModalYesOnly.prefab`
- Test(회귀): `Assets/Tests/PlayMode/GameSceneOverlayBootPlayModeTests.cs`, `ModalViewPlayModeTests.cs`, `TitleViewPlayModeTests.cs`(기존)

**Interfaces:**
- Consumes: Task 2의 ModalView 직렬화 필드(`Root`/`TemplateContainer`/`TemplatePrefabs`/`ButtonPrefabs`/`DefaultButtonPrefab`), `ModalTemplate` 배선.
- Produces: 런타임 동작만(코드 산출물 없음). 모든 소비처 모달이 패리티로 동작.

- [ ] **Step 1: ModalDynamic.prefab 추출 (폴백 틀)**

Unity에서 `Modal.prefab`을 Prefab Mode로 열고:
1. `Root/Panel`, `Root/Title`, `Root/Message`, `Root/Buttons`를 묶어 새 빈 오브젝트 `DynamicBody` 아래로 이동(또는 Panel을 루트로). 이 묶음을 **`ModalDynamic.prefab`로 추출**(드래그 to Project, 또는 `manage_prefabs create_from_gameobject`).
2. `ModalDynamic` 루트에 **ModalTemplate 추가**: `signature`=빈 배열, `title`=Title, `message`=Message, `slots`=빈, `dynamicContainer`=Buttons(HorizontalLayoutGroup).

- [ ] **Step 2: ModalYesNo / ModalYesOnly 정교 제작**

`ModalDynamic`을 복제(또는 신규)해 두 틀을 만든다:
- `ModalYesNo.prefab`: 패널+메시지(폰트/위치 정교)+**NoButton(좌)·YesButton(우) 인스턴스 미리 배치**(HorizontalLayoutGroup 대신 손배치 가능). ModalTemplate: `signature`=`[No, Yes]`, `message`=메시지 TMP, `slots`=[NoButton의 ChoiceSlot, YesButton의 ChoiceSlot](좌→우 순서), `dynamicContainer`=비움.
- `ModalYesOnly.prefab`: 패널+메시지+**YesButton(가운데) 1개 미리 배치**. ModalTemplate: `signature`=`[Yes]`, `slots`=[YesButton의 ChoiceSlot], 나머지 비움.
- 각 YesButton/NoButton 인스턴스는 `ChoiceSlot`(Button+labelText) 포함 — 라벨은 런타임 주입되므로 placeholder 텍스트 무관.

- [ ] **Step 3: Modal.prefab 셸 정리 + ModalView 배선**

`Modal.prefab`에서 추출된 내용 제거 → `Root` 아래 `Dim` + 빈 `TemplateContainer`(stretch)만 남김. ModalView 인스펙터:
- `Root`=Root, `TemplateContainer`=TemplateContainer,
- `TemplatePrefabs`=[ModalYesNo, ModalYesOnly, ModalDynamic](순서 무관, 폴백은 ModalDynamic),
- `ButtonPrefabs`=기존 종류별 매핑(Yes/No/Close→해당 프리팹), `DefaultButtonPrefab`=기존 폴백 버튼 프리팹.

- [ ] **Step 4: 컴파일/콘솔 확인**

Unity Console에 에러·Missing 참조 없는지 확인(`read_console types=[error]`). ModalView 인스펙터 필드가 전부 채워졌는지 육안 확인.

- [ ] **Step 5: 회귀 테스트 (PlayMode)**

Unity Test Runner(PlayMode)에서 실행:
- `ModalViewPlayModeTests`(Task 2, 2건) — 합성 템플릿 경로.
- `GameSceneOverlayBootPlayModeTests` — `[Close]` 모달 → 폴백(ModalDynamic) 경로로 표시·루트 활성.
- `TitleViewPlayModeTests` — 종료(`[No,Yes]`)·이어하기 없음(`[Yes]`) 모달 발행이 정상.

Expected: 전부 PASS.

- [ ] **Step 6: 수동 패리티 (Play) + 스크린샷**

Play로 각 모양을 띄워 확인(MCP `manage_camera screenshot include_image=true`):
- [ ] 타이틀 종료 → ModalYesNo(아니오 좌·예 우, 라벨 정확, 호버/클릭).
- [ ] 타이틀 이어하기(세이브 없음) → ModalYesOnly(확인 가운데).
- [ ] SaveLoad 오토세이브 안내 → 폴백(ModalDynamic)로 기존처럼 표시.
- [ ] 각 모달 클릭·Esc로 닫힘 + 콜백 동작.

- [ ] **Step 7: Commit**

```bash
git add Assets/_Project/Prefabs/UI/Modal.prefab Assets/_Project/Prefabs/UI/ModalDynamic.prefab Assets/_Project/Prefabs/UI/ModalYesNo.prefab Assets/_Project/Prefabs/UI/ModalYesOnly.prefab
git commit -m "$(cat <<'EOF'
feat(ui): 모달 셸/템플릿 프리팹 재구성 + 배선

왜: 모달 내용을 템플릿 프리팹으로 분리해 모양별 정교한 배치를 고정한다. Modal.prefab은
Dim+TemplateContainer 셸로, 현재 내용은 ModalDynamic(폴백)로 추출, ModalYesNo/ModalYesOnly를
정교 제작. ModalView.templatePrefabs 배선으로 [No,Yes]/[Yes]는 정교한 틀, 나머지(Close·Default)는
폴백으로 라우팅 — 소비처 무변경. 회귀(Boot/Title/ModalView) 통과.

Co-Authored-By: Claude Opus 4.8 <noreply@anthropic.com>
EOF
)"
```

---

## 부록: 후속 (이 계획 밖)

- Close 1버튼 등 추가 모양의 전용 틀 승격(폴백에서 이전).
- 템플릿 선택을 종류 시그니처 대신 명시적 키로 전환(필요 시).
