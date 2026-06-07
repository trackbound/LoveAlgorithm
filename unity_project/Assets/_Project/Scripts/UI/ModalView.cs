using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowModalCommand, ModalRequest
using TMPro;
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 범용 모달 표시 뷰(*View). <see cref="ShowModalCommand"/>를 구독해 제목/본문을 채우고 버튼 라벨마다
    /// 버튼을 동적 생성하며(<see cref="ChoiceSlot"/> 재사용 — index+label+callback 범용 버튼), 클릭 시 완료 핸들
    /// (<see cref="ModalRequest"/>)에 인덱스를 채운다(ADR-007: 표시만, 의미는 호출부). 구조는 <see cref="ChoiceView"/>
    /// 미러. 모든 UI 위에 떠야 하므로 최상위 오버레이 캔버스(_ScreenOverlay 류)에 배치한다. 표시 중 새 명령이 오면
    /// 기존 모달을 닫고 다시 그린다(중첩 미지원 — 단일 시스템 모달). 입력 필드는 없다(메시지+버튼만, 후속 확장).
    /// </summary>
    public class ModalView : MonoBehaviour
    {
        [Tooltip("모달 비주얼 루트(딤 배경+패널). 표시 중에만 활성. 미바인딩 시 토글 생략.")]
        [SerializeField] GameObject root;
        [Tooltip("제목 텍스트(선택). 미바인딩 시 제목 생략.")]
        [SerializeField] TMP_Text titleText;
        [Tooltip("본문 텍스트(선택). 미바인딩 시 본문 생략.")]
        [SerializeField] TMP_Text messageText;
        [Tooltip("버튼이 생성될 컨테이너(예: HorizontalLayoutGroup).")]
        [SerializeField] Transform buttonContainer;
        [Tooltip("버튼 프리팹 — ChoiceSlot 재사용(라벨+인덱스 콜백 범용 버튼).")]
        [SerializeField] ChoiceSlot buttonPrefab;

        public GameObject Root { get => root; set => root = value; }
        public TMP_Text TitleText { get => titleText; set => titleText = value; }
        public TMP_Text MessageText { get => messageText; set => messageText = value; }
        public Transform ButtonContainer { get => buttonContainer; set => buttonContainer = value; }
        public ChoiceSlot ButtonPrefab { get => buttonPrefab; set => buttonPrefab = value; }

        readonly List<ChoiceSlot> _spawned = new();
        IDisposable _sub;
        ModalRequest _active;

        void OnEnable() => _sub = EventBus.Subscribe<ShowModalCommand>(OnShow);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
            Clear();
            if (root != null) root.SetActive(false);
        }

        /// <summary>모달 표시 — 제목/본문 세팅 + 버튼 동적 생성. 직접 호출도 가능(테스트).</summary>
        public void OnShow(ShowModalCommand e)
        {
            Clear();
            _active = e.Handle;
            if (root != null) root.SetActive(true);
            if (titleText != null) titleText.text = e.Title ?? "";
            if (messageText != null) messageText.text = e.Message ?? "";

            if (buttonPrefab == null || buttonContainer == null)
            {
                Debug.LogError("[ModalView] buttonPrefab/buttonContainer 미바인딩 — 모달 버튼 표시 불가.");
                return;
            }

            var labels = e.ButtonLabels;
            if (labels == null) return;
            for (int i = 0; i < labels.Count; i++)
            {
                var slot = Instantiate(buttonPrefab, buttonContainer);
                slot.Bind(i, labels[i], OnSelected);
                _spawned.Add(slot);
            }
        }

        void OnSelected(int index)
        {
            var handle = _active;
            _active = null;
            Clear();
            if (root != null) root.SetActive(false);
            // 정리 후 마지막에 통지: 콜백이 또 다른 모달을 띄우거나 종료를 발행해도 현재 모달 상태와 꼬이지 않게.
            handle?.Select(index);
        }

        void Clear()
        {
            foreach (var s in _spawned)
                if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
        }
    }
}
