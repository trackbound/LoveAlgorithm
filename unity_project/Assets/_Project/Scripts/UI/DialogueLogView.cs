using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // OverlayGate
using LoveAlgo.Events; // OpenDialogueLogCommand
using UnityEngine;
using UnityEngine.UI;

namespace LoveAlgo.UI
{
    /// <summary>
    /// 대사 로그(백로그) 팝업 뷰(*View, ADR-013 Overlay — 로그 목업 동결). <see cref="OpenDialogueLogCommand"/>로 표시.
    /// 렌더 모델 = "run 컨테이너"(카카오톡식): <see cref="DialogueLogStore"/>의 박스들을 연속 동일 화자 구간(run)으로
    /// 묶어, run마다 [좌측 화자 헤더(초상+이름, run 전체 높이를 자연히 덮음) | 우측 대사 버블 세로 스택]을 만든다.
    /// 진행(대사 한 줄)마다 버블 1개 → 버블 사이/​run 사이 간격이 모두 동일(개행 균일). 이름표·초상은 run당 1회만.
    /// 나레이션(독백) run은 헤더 없이 좌측 거터만 비우고 분홍 번짐 버블을 쌓는다. 헤더/버블 모두 <see cref="DialogueLogEntrySlot"/>
    /// 재사용(헤더=초상+이름만, 버블=본문만 배선). 히로인 초상은 SpeakerId(c01~)→스프라이트 매핑(미등록=초상 없음=엑스트라).
    /// 닫기 = 돌아가기 버튼/공용 뒤로가기. SetVisible/게이트/SetAsLastSibling = SettingsView 미러.
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
        [Tooltip("run 컨테이너를 스폰할 컨테이너(ScrollRect Content).")]
        [SerializeField] Transform content;
        [SerializeField] ScrollRect scrollRect;
        [SerializeField] Button returnButton;

        [Header("프리팹")]
        [Tooltip("캐릭터 화자 헤더(초상+캐릭터 이름박스). 캐릭터 run의 좌측에 1회.")]
        [SerializeField] DialogueLogEntrySlot speakerHeaderPrefab;
        [Tooltip("주인공 화자 헤더(초상 없이 플레이어 이름박스). 플레이어 run의 좌측에 1회.")]
        [SerializeField] DialogueLogEntrySlot playerHeaderPrefab;
        [Tooltip("캐릭터 대사 버블(textbox_character, 검정 본문).")]
        [SerializeField] DialogueLogEntrySlot characterBubblePrefab;
        [Tooltip("주인공 대사 버블(textbox_player, 흰 본문).")]
        [SerializeField] DialogueLogEntrySlot playerBubblePrefab;
        [Tooltip("독백 버블(분홍 번짐 배경, 흰 본문).")]
        [SerializeField] DialogueLogEntrySlot narrationBubblePrefab;

        [Header("레이아웃")]
        [Tooltip("좌측 헤더/거터 열 폭(모든 종류 동일 — 본문 좌측 정렬 일치).")]
        [SerializeField] float gutterWidth = 150f;
        [Tooltip("헤더와 버블 스택 사이 가로 간격.")]
        [SerializeField] float headerGap = 24f;
        [Tooltip("같은 화자 버블 사이 세로 간격(run 내부).")]
        [SerializeField] float lineSpacing = 20f;
        [Tooltip("화자가 바뀌는 run 사이 세로 간격 — 구분이 나게 lineSpacing보다 크게.")]
        [SerializeField] float runSpacing = 44f;

        [Tooltip("히로인 초상(SpeakerId c01~c05 → 스프라이트). 미등록 화자 = 초상 없음(엑스트라).")]
        [SerializeField] List<PortraitPair> portraits = new();

        // 테스트/배선 주입용(SaveLoadView 패턴).
        public CanvasGroup Group { get => canvasGroup; set => canvasGroup = value; }
        public Transform Content { get => content; set => content = value; }
        public ScrollRect Scroll { get => scrollRect; set => scrollRect = value; }
        public Button ReturnButton { get => returnButton; set => returnButton = value; }
        public DialogueLogEntrySlot SpeakerHeaderPrefab { get => speakerHeaderPrefab; set => speakerHeaderPrefab = value; }
        public DialogueLogEntrySlot PlayerHeaderPrefab { get => playerHeaderPrefab; set => playerHeaderPrefab = value; }
        public DialogueLogEntrySlot CharacterBubblePrefab { get => characterBubblePrefab; set => characterBubblePrefab = value; }
        public DialogueLogEntrySlot PlayerBubblePrefab { get => playerBubblePrefab; set => playerBubblePrefab = value; }
        public DialogueLogEntrySlot NarrationBubblePrefab { get => narrationBubblePrefab; set => narrationBubblePrefab = value; }
        public List<PortraitPair> Portraits => portraits;
        public bool IsVisible => _visible;

        readonly List<GameObject> _runs = new();
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

        /// <summary>로그 열기 — 저장소 전체를 run 컨테이너로 재구성하고 최신(맨 아래)으로 스크롤.</summary>
        public void Show()
        {
            Rebuild();
            SetVisible(true);
            if (isActiveAndEnabled) StartCoroutine(ScrollToBottomNextFrame());
        }

        public void Hide() => SetVisible(false);

        void Rebuild()
        {
            foreach (var r in _runs)
                if (r != null) Destroy(r);
            _runs.Clear();
            if (content == null) return;

            // run 사이 간격(화자 변경)은 run 내부 줄 간격보다 크게 — Content VLG가 컨테이너를 쌓는 간격.
            if (content.TryGetComponent<VerticalLayoutGroup>(out var contentVlg))
                contentVlg.spacing = runSpacing;

            var entries = DialogueLogStore.Entries;
            int i = 0;
            while (i < entries.Count)
            {
                // 연속 동일 화자 = 한 run. [i, j) 구간을 한 컨테이너로.
                int j = i + 1;
                while (j < entries.Count && DialogueLogStore.IsSameSpeaker(entries[i], entries[j])) j++;
                BuildRun(entries, i, j);
                i = j;
            }
        }

        void BuildRun(IReadOnlyList<DialogueLogEntry> entries, int start, int end)
        {
            var first = entries[start];
            var bubblePrefab = BubbleFor(first.Kind);
            if (bubblePrefab == null) return; // 변형 미배선 = 해당 종류 생략(부분 바인딩 안전)

            var run = new GameObject("Run", typeof(RectTransform));
            run.transform.SetParent(content, false);
            var hlg = run.AddComponent<HorizontalLayoutGroup>();
            hlg.childControlWidth = hlg.childControlHeight = true;
            hlg.childForceExpandWidth = hlg.childForceExpandHeight = false;
            hlg.childAlignment = TextAnchor.UpperLeft; // 헤더(초상)·버블 모두 상단 정렬
            hlg.spacing = headerGap;
            _runs.Add(run);

            // 좌측: 화자 헤더(캐릭터/플레이어) 또는 거터 스페이서(독백) — 폭은 동일.
            var headerPrefab = HeaderFor(first.Kind);
            if (headerPrefab == null)
            {
                var spacer = new GameObject("Gutter", typeof(RectTransform));
                spacer.transform.SetParent(run.transform, false);
                AddLayoutElement(spacer, gutterWidth);
            }
            else
            {
                var header = Instantiate(headerPrefab, run.transform);
                header.Bind(first, PortraitFor(first.SpeakerId)); // 플레이어=초상 null→자동 숨김
            }

            // 우측: 대사 버블 세로 스택(진행마다 1개).
            var stack = new GameObject("Stack", typeof(RectTransform));
            stack.transform.SetParent(run.transform, false);
            var stackLe = stack.AddComponent<LayoutElement>();
            stackLe.flexibleWidth = 1f;
            var svlg = stack.AddComponent<VerticalLayoutGroup>();
            svlg.childControlWidth = svlg.childControlHeight = true;
            svlg.childForceExpandWidth = true;
            svlg.childForceExpandHeight = false;
            svlg.spacing = lineSpacing;

            for (int k = start; k < end; k++)
            {
                var bubble = Instantiate(bubblePrefab, stack.transform);
                bubble.Bind(entries[k], null); // 버블엔 본문만(헤더/초상은 좌측 헤더가 담당)
            }
        }

        void AddLayoutElement(GameObject go, float width)
        {
            var le = go.AddComponent<LayoutElement>();
            le.preferredWidth = width;
            le.flexibleWidth = 0f;
        }

        DialogueLogEntrySlot BubbleFor(DialogueLogKind kind) => kind switch
        {
            DialogueLogKind.Player => playerBubblePrefab,
            DialogueLogKind.Narration => narrationBubblePrefab,
            _ => characterBubblePrefab,
        };

        // 독백 = 헤더 없음(좌측 거터만). 캐릭터/플레이어 = 각자 헤더.
        DialogueLogEntrySlot HeaderFor(DialogueLogKind kind) => kind switch
        {
            DialogueLogKind.Player => playerHeaderPrefab,
            DialogueLogKind.Narration => null,
            _ => speakerHeaderPrefab,
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
