using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus, DebugInput
using LoveAlgo.Events; // ShowModalCommand, ModalRequest, ModalButton, ModalButtonKind
using LoveAlgo.Core;   // OverlayGate
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 범용 모달 표시 뷰(*View). <see cref="ShowModalCommand"/>를 구독해 버튼 종류 시그니처로 템플릿을 고르고
    /// 인스턴스화한다(<see cref="ModalTemplate"/>). 정적 틀은 미리 배치된 슬롯에 라벨·콜백만 Bind, 매칭 없으면
    /// 빈 시그니처(폴백) 틀에 종류별 버튼을 동적 스폰(<see cref="ButtonSlot"/> 재사용). 클릭/키 시 완료 핸들
    /// (<see cref="ModalRequest"/>)에 인덱스를 채운다(ADR-007: 표시만, 의미·아트 분리). 단일 모달 — 새 명령 시 재생성.
    /// <para>키 입력은 <see cref="OverlayGate"/>에 등록해 공용 라우터(<c>OverlayHotkeyRouter</c>)를 경유한다 —
    /// ESC=취소(아니오/닫기 버튼), Enter=확정(예 버튼, 단일 버튼 모달은 그 버튼). 게이트 최상단으로만 라우팅되어
    /// 설정·세이브로드 위에 모달이 떠도 키는 모달만 받는다(중복 차단). 자체 키 폴링은 두지 않는다.</para>
    /// </summary>
    public class ModalView : MonoBehaviour
    {
        /// <summary>버튼 종류 → 스타일 프리팹 매핑 1건(폴백 동적 스폰 전용).</summary>
        [Serializable]
        public struct KindPrefab
        {
            public ModalButtonKind kind;
            public ButtonSlot prefab;
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
        [SerializeField] ButtonSlot defaultButtonPrefab;

        [Header("눈감김(아이마스크) 동안 정렬 상승")]
        [Tooltip("눈꺼풀 차폐 중에만 모달 Canvas를 이 정렬값으로 올려 암전 위에 모달이 보이고 클릭되게 한다. " +
                 "눈꺼풀 바(95)·대사창 상승(96)보다 크고 ScreenFade(100)보다 작아야 함. 차폐 해제 시 authored 정렬(80)로 복원.")]
        [SerializeField] int eyeMaskShroudSortingOrder = 98;

        public GameObject Root { get => root; set => root = value; }
        public Transform TemplateContainer { get => templateContainer; set => templateContainer = value; }
        public List<ModalTemplate> TemplatePrefabs { get => templatePrefabs; set => templatePrefabs = value; }
        public List<KindPrefab> ButtonPrefabs => buttonPrefabs;
        public ButtonSlot DefaultButtonPrefab { get => defaultButtonPrefab; set => defaultButtonPrefab = value; }

        IDisposable _sub, _eyeShroudSub;
        IDisposable _gate;       // OverlayGate 토큰(표시 중에만 non-null — ESC=취소·Enter=확정 라우팅 대상)
        Canvas _canvas;          // 모달 루트 Canvas(차폐 동안만 정렬 상승 — overrideSorting은 항상 ON@80 유지).
        int _baseSortingOrder;   // authored 정렬(80) — 차폐 해제 시 복원.
        bool _baseCaptured;
        ModalRequest _active;
        IReadOnlyList<ModalButton> _buttons;
        ModalTemplate _activeTemplate;                  // 인스턴스화된 템플릿(닫을 때 Destroy)
        readonly List<ButtonSlot> _boundSlots = new();  // Esc 취소 인덱스·라벨 로그용

        void Awake()
        {
            if (root == gameObject)
            {
                Debug.LogError("[ModalView] root가 모달 GO 자신으로 바인딩 — 비주얼 자식(딤+컨테이너)을 바인딩해야 한다. 부팅 숨김 생략.");
                return;
            }
            if (root != null) root.SetActive(false); // authored-active 비주얼을 플레이 시작 시 숨김
        }

        void OnEnable()
        {
            _sub = EventBus.Subscribe<ShowModalCommand>(OnShow);
            // 눈감김(아이마스크) 차폐 동안에만 모달을 눈꺼풀 위로 올린다(엔딩 EyeClose→ReturnToTitle 모달이
            // 눈꺼풀 95 아래 80에 깔려 안 보이고 클릭 안 되던 영구 멈춤 방지). 대사창의 조건부 상승 패턴 미러.
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_canvas != null && !_baseCaptured) { _baseSortingOrder = _canvas.sortingOrder; _baseCaptured = true; }
            _eyeShroudSub = EventBus.Subscribe<EyeMaskShroudChanged>(OnEyeMaskShroud);
        }

        void OnDisable()
        {
            _sub?.Dispose();
            _eyeShroudSub?.Dispose();
            _sub = _eyeShroudSub = null;
            _gate?.Dispose(); _gate = null; // 표시 중 비활성 시 게이트 누수 방지(중복 무해)
            // 차폐 정렬 상승이 남지 않도록 authored 정렬로 복원(다음 표시가 기본 80에서 시작).
            if (_canvas != null && _baseCaptured) _canvas.sortingOrder = _baseSortingOrder;
            Clear();
            if (root != null) root.SetActive(false);
        }

        // 눈꺼풀 차폐 동안만 모달 Canvas 정렬을 눈꺼풀 위로 올린다(차폐 해제 시 authored 80 복원). overrideSorting은
        // 항상 ON(평상시 80 nested)이라 건드리지 않고 sortingOrder만 스왑한다 — 정렬값 상승만으로 raycast도 눈꺼풀보다 우선.
        void OnEyeMaskShroud(EyeMaskShroudChanged e)
        {
            if (_canvas == null) _canvas = GetComponent<Canvas>();
            if (_canvas == null) return;
            if (!_baseCaptured) { _baseSortingOrder = _canvas.sortingOrder; _baseCaptured = true; }
            _canvas.sortingOrder = e.Active ? eyeMaskShroudSortingOrder : _baseSortingOrder;
        }

        /// <summary>모달 표시 — 시그니처로 템플릿 선택 → 인스턴스화 → 제목/본문 + 슬롯 바인딩/동적 스폰. 직접 호출도 가능(테스트).</summary>
        public void OnShow(ShowModalCommand e)
        {
            Clear();
            _active = e.Handle;
            _buttons = e.Buttons;
            if (root != null) root.SetActive(true);

            // ESC=취소·Enter=확정을 공용 라우터가 최상단으로만 전달하도록 게이트에 등록(자체 키 폴링 없음).
            // 취소=아니오/닫기 버튼(없으면 마지막 = 기존 Esc 관례), 확정=예 버튼(없고 단일 버튼이면 그 버튼).
            _gate?.Dispose();
            int cancelIdx = CancelIndex(e.Buttons);
            int confirmIdx = ConfirmIndex(e.Buttons);
            _gate = OverlayGate.Push(
                () => Close(cancelIdx),
                confirmIdx >= 0 ? (Action)(() => Close(confirmIdx)) : null);

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
            if (_activeTemplate.Message != null)
            {
                _activeTemplate.Message.text = e.Message ?? "";
                _activeTemplate.Message.color = Color.white; // 모달 본문은 항상 깔끔한 하얀색(템플릿 프리팹별 색 편차 제거).
            }

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
                tpl.Slots[i].Bind(i, CanonicalLabel(buttons[i].Kind, buttons.Count, buttons[i].Label), OnSelected);
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
                slot.Bind(i, CanonicalLabel(buttons[i].Kind, buttons.Count, buttons[i].Label), OnSelected);
                _boundSlots.Add(slot);
            }
        }

        // 모달 버튼 라벨 통일(caller/인스펙터 라벨 무시) — 어느 화면에서 띄워도 동일 문구를 강제한다.
        // No=아니오 · Yes=예(YesNo 2버튼) 또는 확인(단일 버튼=YesOnly 확인 모달) · Close=확인.
        // Default(종류 미지정)만 caller 라벨을 유지(특수/테스트). 문구를 한 곳에 고정해 인스펙터 드리프트를 차단.
        static string CanonicalLabel(ModalButtonKind kind, int buttonCount, string fallback)
        {
            switch (kind)
            {
                case ModalButtonKind.No:    return "아니오";
                case ModalButtonKind.Close: return "확인";
                case ModalButtonKind.Yes:   return buttonCount <= 1 ? "확인" : "예";
                default:                    return fallback ?? "";
            }
        }

        // ESC(취소) 대상 인덱스: 아니오 > 닫기 > 마지막 버튼(기존 Esc 관례 폴백). 버튼 없으면 -1(닫기만).
        static int CancelIndex(IReadOnlyList<ModalButton> buttons)
        {
            if (buttons == null || buttons.Count == 0) return -1;
            int i = IndexOfKind(buttons, ModalButtonKind.No);
            if (i < 0) i = IndexOfKind(buttons, ModalButtonKind.Close);
            if (i < 0) i = buttons.Count - 1;
            return i;
        }

        // Enter(확정) 대상 인덱스: 예 버튼, 없고 단일 버튼이면 그 버튼(확인 모달). 그 외 -1(Enter 무동작).
        static int ConfirmIndex(IReadOnlyList<ModalButton> buttons)
        {
            if (buttons == null || buttons.Count == 0) return -1;
            int i = IndexOfKind(buttons, ModalButtonKind.Yes);
            if (i < 0 && buttons.Count == 1) i = 0;
            return i;
        }

        static int IndexOfKind(IReadOnlyList<ModalButton> buttons, ModalButtonKind kind)
        {
            for (int i = 0; i < buttons.Count; i++) if (buttons[i].Kind == kind) return i;
            return -1;
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

        ButtonSlot PrefabFor(ModalButtonKind kind)
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

        void Close(int index)
        {
            _gate?.Dispose(); _gate = null;
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
