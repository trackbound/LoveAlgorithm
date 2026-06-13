using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus, DebugInput
using LoveAlgo.Events; // ShowModalCommand, ModalRequest, ModalButton, ModalButtonKind
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;   // Keyboard (Esc 모달 취소)
using UnityEngine.Serialization; // FormerlySerializedAs (buttonPrefab → defaultButtonPrefab 이관)

namespace LoveAlgo.UI
{
    /// <summary>
    /// 범용 모달 표시 뷰(*View). <see cref="ShowModalCommand"/>를 구독해 제목/본문을 채우고, 버튼마다 그 종류
    /// (<see cref="ModalButtonKind"/>)에 맞는 **스타일 프리팹**을 골라 동적 생성한다(<see cref="ChoiceSlot"/> 바인딩
    /// 재사용 — index+label+callback). 클릭/Esc 시 완료 핸들(<see cref="ModalRequest"/>)에 인덱스를 채운다
    /// (ADR-007: 표시만, 의미·아트 분리 — 명령은 종류만, 스프라이트/색은 프리팹에). 호버 스왑은 버튼 프리팹이 자체
    /// 처리(Unity Button Sprite Swap + 글씨색 컴포넌트). 표시 중 새 명령이 오면 기존 모달을 닫고 다시 그린다(단일 모달).
    /// </summary>
    public class ModalView : MonoBehaviour
    {
        /// <summary>버튼 종류 → 스타일 프리팹 매핑 1건. 인스펙터에서 종류별 버튼 프리팹(btn_common_*)을 배선.</summary>
        [Serializable]
        public struct KindPrefab
        {
            public ModalButtonKind kind;
            public ChoiceSlot prefab;
        }

        [Tooltip("모달 비주얼 루트(딤 배경+패널). 표시 중에만 활성. 미바인딩 시 토글 생략.")]
        [SerializeField] GameObject root;
        [Tooltip("제목 텍스트(선택). 미바인딩 시 제목 생략.")]
        [SerializeField] TMP_Text titleText;
        [Tooltip("본문 텍스트(선택). 미바인딩 시 본문 생략.")]
        [SerializeField] TMP_Text messageText;
        [Tooltip("버튼이 생성될 컨테이너(예: HorizontalLayoutGroup).")]
        [SerializeField] Transform buttonContainer;
        [Tooltip("종류별 스타일 버튼 프리팹(Yes/No/Close 등). 좌→우 순서는 명령의 버튼 배열 순서.")]
        [SerializeField] List<KindPrefab> buttonPrefabs = new();
        [Tooltip("종류에 맞는 프리팹이 없을 때 폴백 버튼 프리팹(ChoiceSlot).")]
        [FormerlySerializedAs("buttonPrefab")]
        [SerializeField] ChoiceSlot defaultButtonPrefab;

        public GameObject Root { get => root; set => root = value; }
        public TMP_Text TitleText { get => titleText; set => titleText = value; }
        public TMP_Text MessageText { get => messageText; set => messageText = value; }
        public Transform ButtonContainer { get => buttonContainer; set => buttonContainer = value; }
        public ChoiceSlot DefaultButtonPrefab { get => defaultButtonPrefab; set => defaultButtonPrefab = value; }
        public List<KindPrefab> ButtonPrefabs => buttonPrefabs;

        readonly List<ChoiceSlot> _spawned = new();
        IDisposable _sub;
        ModalRequest _active;
        IReadOnlyList<ModalButton> _buttons; // 활성 모달 버튼(라벨+종류, 디버그 로그용)

        void Awake()
        {
            // root는 반드시 비주얼 "자식"(딤+패널) — 자기 자신이면 숨김이 GO를 꺼서 구독까지 죽는다
            // (2026-06-12 Game.unity 인스턴스 오바인딩 실증). 치명 동작을 막고 에러로 시정 지시.
            if (root == gameObject)
            {
                Debug.LogError("[ModalView] root가 모달 GO 자신으로 바인딩 — 비주얼 자식(딤+패널)을 바인딩해야 한다. 부팅 숨김 생략.");
                return;
            }
            // 씬에 authored-active로 저장된 비주얼을 플레이 시작 시 숨김 — 부팅 일괄 활성화(UiBootActivator)가
            // GO를 켤 때 placeholder 모달이 노출되는 사고 방지(DialogueView.HideEndMark 선례). 표시는 ShowModalCommand로만.
            if (root != null) root.SetActive(false);
        }

        void OnEnable() => _sub = EventBus.Subscribe<ShowModalCommand>(OnShow);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
            Clear();
            if (root != null) root.SetActive(false);
        }

        /// <summary>모달 표시 — 제목/본문 세팅 + 종류별 버튼 동적 생성. 직접 호출도 가능(테스트).</summary>
        public void OnShow(ShowModalCommand e)
        {
            Clear();
            _active = e.Handle;
            _buttons = e.Buttons;
            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = e.Title ?? "";
            if (messageText != null) messageText.text = e.Message ?? "";

            if (buttonContainer == null)
            {
                Debug.LogError("[ModalView] buttonContainer 미바인딩 — 모달 버튼 표시 불가.");
                return;
            }
            if (e.Buttons == null) return;
            for (int i = 0; i < e.Buttons.Count; i++)
            {
                var prefab = PrefabFor(e.Buttons[i].Kind);
                if (prefab == null)
                {
                    Debug.LogError($"[ModalView] 버튼 프리팹 없음(종류={e.Buttons[i].Kind}, 폴백도 미바인딩) — 버튼 생략.");
                    continue;
                }
                var slot = Instantiate(prefab, buttonContainer);
                slot.Bind(i, e.Buttons[i].Label, OnSelected);
                _spawned.Add(slot);
            }
        }

        // 종류 → 스타일 프리팹(매핑에 없으면 폴백). 첫 일치 사용.
        ChoiceSlot PrefabFor(ModalButtonKind kind)
        {
            for (int i = 0; i < buttonPrefabs.Count; i++)
                if (buttonPrefabs[i].kind == kind && buttonPrefabs[i].prefab != null)
                    return buttonPrefabs[i].prefab;
            return defaultButtonPrefab;
        }

        // 버튼 클릭 콜백(좌클릭). 선택 인덱스를 로그하고 모달을 닫는다.
        void OnSelected(int index)
        {
            DebugInput.Log($"좌클릭 → 모달 확인: index={index}{LabelSuffix(index)}");
            Close(index);
        }

        // Esc로 모달 취소 — 마지막 버튼(취소 관례)을 선택해 닫는다. 모달 표시 중일 때만.
        void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || _active == null || !kb.escapeKey.wasPressedThisFrame) return;
            int cancel = _spawned.Count - 1;
            if (cancel < 0) return; // 버튼 없는 모달 — 취소 대상 인덱스 없음
            DebugInput.Log($"Esc → 모달 취소: index={cancel}{LabelSuffix(cancel)}");
            Close(cancel);
        }

        // 핸들 회수 + 정리 + 숨김(클릭/Esc 공통). 정리 후 마지막에 통지: 콜백이 또 다른 모달을 띄우거나
        // 종료를 발행해도 현재 모달 상태와 꼬이지 않게.
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
            foreach (var s in _spawned)
                if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
        }
    }
}
