using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate
using LoveAlgo.Events; // OpenDialogueLogCommand
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그(백로그) 팝업 뷰(*View, ADR-013 Overlay — 로그 목업 동결). <see cref="OpenDialogueLogCommand"/>로
    /// 표시: <see cref="DialogueLogStore"/>의 박스들을 종류별 슬롯 프리팹(캐릭터/플레이어/나레이션,
    /// ModalView Kind→프리팹 매핑 미러)으로 스폰하고 맨 아래(최신)로 스크롤. 히로인 초상은
    /// SpeakerId(c01~c05)→스프라이트 직렬화 매핑(미등록 = 초상 없음 — 엑스트라).
    /// 닫기 = 돌아가기 버튼/공용 뒤로가기. SetVisible/게이트 토큰/SetAsLastSibling = SettingsView 1:1 미러
    /// (표시 중 오토·진행은 OverlayGate가 자동 정지).
    /// </summary>
    public class DialogueLogView : MonoBehaviour
    {
        /// <summary>히로인 초상 매핑 1건(SpeakerId → 스프라이트). 인스펙터 배선(roa.png 등).</summary>
        [Serializable]
        public struct PortraitPair
        {
            public string speakerId;
            public Sprite sprite;
        }

        [SerializeField] CanvasGroup canvasGroup;
        [Tooltip("박스 슬롯을 스폰할 컨테이너(ScrollRect Content).")]
        [SerializeField] Transform content;
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] Button returnButton;

        [Header("종류별 슬롯 프리팹")]
        [SerializeField] DialogueLogEntrySlot characterSlotPrefab;
        [SerializeField] DialogueLogEntrySlot playerSlotPrefab;
        [SerializeField] DialogueLogEntrySlot narrationSlotPrefab;

        [Tooltip("히로인 초상(SpeakerId c01~c05 → 스프라이트). 미등록 화자 = 초상 없음(엑스트라).")]
        [SerializeField] List<PortraitPair> portraits = new();

        // 테스트/배선 주입용(SaveLoadView 패턴).
        public CanvasGroup Group { get => canvasGroup; set => canvasGroup = value; }
        public Transform Content { get => content; set => content = value; }
        public ScrollRect Scroll { get => scrollRect; set => scrollRect = value; }
        public Button ReturnButton { get => returnButton; set => returnButton = value; }
        public DialogueLogEntrySlot CharacterSlotPrefab { get => characterSlotPrefab; set => characterSlotPrefab = value; }
        public DialogueLogEntrySlot PlayerSlotPrefab { get => playerSlotPrefab; set => playerSlotPrefab = value; }
        public DialogueLogEntrySlot NarrationSlotPrefab { get => narrationSlotPrefab; set => narrationSlotPrefab = value; }
        public List<PortraitPair> Portraits => portraits;
        public bool IsVisible => _visible;

        readonly List<DialogueLogEntrySlot> _spawned = new();
        IDisposable _openSub;
        IDisposable _gate;
        bool _visible;

        void Awake()
        {
            if (returnButton != null) returnButton.onClick.AddListener(Hide);
            SetVisible(false); // 부팅 숨김
        }

        void OnEnable() => _openSub = EventBus.Subscribe<OpenDialogueLogCommand>(_ => Show());

        void OnDisable()
        {
            _openSub?.Dispose();
            _openSub = null;
            _gate?.Dispose(); // 표시 중 비활성 시 게이트 누수 방지
            _gate = null;
            _visible = false;
        }

        /// <summary>로그 열기 — 저장소 전체를 박스 슬롯으로 재구성하고 최신(맨 아래)으로 스크롤.</summary>
        public void Show()
        {
            Rebuild();
            SetVisible(true);
            if (isActiveAndEnabled) StartCoroutine(ScrollToBottomNextFrame());
        }

        public void Hide() => SetVisible(false);

        void Rebuild()
        {
            foreach (var s in _spawned)
                if (s != null) Destroy(s.gameObject);
            _spawned.Clear();
            if (content == null) return;

            var entries = DialogueLogStore.Entries;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                var prefab = PrefabFor(entry.Kind);
                if (prefab == null) continue; // 변형 미배선 = 해당 종류 생략(부분 바인딩 안전)
                // 연속 동일 화자 구간(run)의 첫 박스에만 이름표/초상 — 화자가 바뀌면 다시 표시.
                bool showSpeaker = i == 0 || !DialogueLogStore.IsSameSpeaker(entries[i - 1], entry);
                var slot = Instantiate(prefab, content);
                slot.Bind(entry, PortraitFor(entry.SpeakerId), showSpeaker);
                _spawned.Add(slot);
            }
        }

        DialogueLogEntrySlot PrefabFor(DialogueLogKind kind) => kind switch
        {
            DialogueLogKind.Player => playerSlotPrefab,
            DialogueLogKind.Narration => narrationSlotPrefab,
            _ => characterSlotPrefab,
        };

        Sprite PortraitFor(string speakerId)
        {
            if (string.IsNullOrEmpty(speakerId)) return null;
            for (int i = 0; i < portraits.Count; i++)
                if (portraits[i].speakerId == speakerId) return portraits[i].sprite;
            return null;
        }

        // 레이아웃 갱신 뒤(다음 프레임) 최하단 스크롤 — 같은 프레임엔 ContentSizeFitter가 아직 안 돌았다.
        IEnumerator ScrollToBottomNextFrame()
        {
            yield return null;
            if (scrollRect != null) scrollRect.verticalNormalizedPosition = 0f;
        }

        void SetVisible(bool v)
        {
            if (canvasGroup == null) return;
            canvasGroup.alpha = v ? 1f : 0f;
            canvasGroup.interactable = v;
            canvasGroup.blocksRaycasts = v;
            if (v)
            {
                transform.SetAsLastSibling(); // 시각·논리 최상단 동기화(SettingsView 미러)
                _gate?.Dispose();
                _gate = OverlayGate.Push(() => SetVisible(false));
                _visible = true;
            }
            else if (_visible)
            {
                _gate?.Dispose();
                _gate = null;
                _visible = false;
            }
        }
    }
}
