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
        ModalTemplate _activeTemplate;                  // 인스턴스화된 템플릿(닫을 때 Destroy)
        readonly List<ChoiceSlot> _boundSlots = new();  // Esc 취소 인덱스·라벨 로그용

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

        // 정적 틀: 미리 배치된 슬롯에 라벨·콜백 Bind(스킨은 박힘). 슬롯 수 불일치는 로그.
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
