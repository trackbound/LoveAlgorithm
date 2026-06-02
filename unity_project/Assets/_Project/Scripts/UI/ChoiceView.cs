using System;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Events; // ShowChoiceCommand, ChoiceRequest
using UnityEngine;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 선택지 표시 뷰(*View). <see cref="ShowChoiceCommand"/>를 구독해 옵션 라벨마다 버튼을 동적 생성하고,
    /// 클릭 시 완료 핸들(<see cref="ChoiceRequest"/>)에 선택 인덱스를 채운다(ADR-007: 표시만, 해석은 엔진).
    /// 구조는 Schedule의 ScheduleUI 미러 — 슬롯 prefab 동적 생성. 효과/점프는 NarrativePlayer가 처리한다.
    /// </summary>
    public class ChoiceView : MonoBehaviour
    {
        [Tooltip("선택지 비주얼 루트(선택). 표시 중에만 활성, 선택 후 숨김.")]
        [SerializeField] GameObject root;
        [SerializeField] Transform slotContainer;
        [SerializeField] ChoiceOptionSlot slotPrefab;

        public GameObject Root { get => root; set => root = value; }
        public Transform SlotContainer { get => slotContainer; set => slotContainer = value; }
        public ChoiceOptionSlot SlotPrefab { get => slotPrefab; set => slotPrefab = value; }

        readonly List<ChoiceOptionSlot> _spawned = new();
        IDisposable _sub;
        ChoiceRequest _active;

        void OnEnable() => _sub = EventBus.Subscribe<ShowChoiceCommand>(OnShow);

        void OnDisable()
        {
            _sub?.Dispose();
            _sub = null;
            Clear();
        }

        void OnShow(ShowChoiceCommand e)
        {
            Clear();
            _active = e.Handle;
            if (root != null) root.SetActive(true);

            if (slotPrefab == null || slotContainer == null)
            {
                Debug.LogError("[ChoiceView] slotPrefab/slotContainer 미바인딩 — 선택지 표시 불가.");
                return;
            }

            var texts = e.OptionTexts;
            if (texts == null) return;
            for (int i = 0; i < texts.Count; i++)
            {
                var slot = Instantiate(slotPrefab, slotContainer);
                slot.Bind(i, texts[i], OnSelected);
                _spawned.Add(slot);
            }
        }

        void OnSelected(int index)
        {
            _active?.Select(index);
            _active = null;
            Clear();
            if (root != null) root.SetActive(false);
        }

        void Clear()
        {
            foreach (var s in _spawned)
                if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
        }
    }
}
