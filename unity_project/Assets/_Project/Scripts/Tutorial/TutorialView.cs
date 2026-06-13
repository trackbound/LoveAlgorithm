using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events;

namespace LoveAlgo.Tutorial
{
    /// <summary>
    /// 튜토리얼 오버레이(*View, 기획 내부 콘텐츠 p7~34) — 스텝별 딤(디자이너 베이크 텍스처 스왑) +
    /// 로아 아이콘(표정 3종)·말풍선 + 클릭 진행. 강제 클릭 스텝은 지정 앵커 영역 클릭만 통과시켜
    /// 실제 버튼을 실행(패스스루 — "한 번 눌러봐!" = 진짜 화면 전환), 그 외 클릭은 전부 흡수.
    /// 자동(오토) 진행 불가 — autoAdvanceSeconds 명시 스텝(마지막 인사)만 예외. 종료 시 1회 기록
    /// (PlayerPrefs) + <see cref="TutorialFinishedEvent"/> 발행.
    /// </summary>
    public class TutorialView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] Image dimImage;          // 풀스크린 딤(raycast 캐처 겸) — 스텝별 스프라이트 스왑
        [SerializeField] Sprite fallbackDim;      // 스텝 딤 미지정 시(풀딤 dim_0)
        [SerializeField] RectTransform roaGroup;  // 로아 아이콘+말풍선 묶음(스텝별 위치 이동)
        [SerializeField] Image roaImage;
        [SerializeField] Sprite roaBasic;         // roa_1 로아기본
        [SerializeField] Sprite roaSmile;         // roa_2 로아눈웃음(밝게웃음 폴백)
        [SerializeField] Sprite roaBeam;          // roa_3 로아활짝
        [SerializeField] TMP_Text bubbleText;
        [Header("말풍선 자동 크기(목업: 대사 길이에 따라 가로+세로 가변)")]
        [SerializeField] RectTransform bubbleRect;
        [Tooltip("텍스트 줄바꿈 최대 폭 — 이 폭을 넘으면 줄 수로 세로 증가.")]
        [SerializeField] float bubbleMaxTextWidth = 560f;
        [Tooltip("말풍선 최소 폭(9-슬라이스 꼬리/모서리 보호).")]
        [SerializeField] float bubbleMinWidth = 240f;
        [Tooltip("텍스트 주변 여백(가로/세로 합산 — 좌우 합, 상하 합).")]
        [SerializeField] Vector2 bubblePadding = new(76f, 58f);
        [SerializeField] GameStateSO state;       // {{Player}} 치환
        [SerializeField] TutorialSequenceSO sequence;

        public GameObject Root { get => root; set => root = value; }
        public Image DimImage { get => dimImage; set => dimImage = value; }
        public Sprite FallbackDim { get => fallbackDim; set => fallbackDim = value; }
        public RectTransform RoaGroup { get => roaGroup; set => roaGroup = value; }
        public Image RoaImage { get => roaImage; set => roaImage = value; }
        public Sprite RoaBasic { get => roaBasic; set => roaBasic = value; }
        public Sprite RoaSmile { get => roaSmile; set => roaSmile = value; }
        public Sprite RoaBeam { get => roaBeam; set => roaBeam = value; }
        public TMP_Text BubbleText { get => bubbleText; set => bubbleText = value; }
        public RectTransform BubbleRect { get => bubbleRect; set => bubbleRect = value; }
        public GameStateSO State { get => state; set => state = value; }
        public TutorialSequenceSO Sequence { get => sequence; set => sequence = value; }

        /// <summary>현재 스텝 인덱스(-1=비재생). 테스트/디버그 관찰용.</summary>
        public int CurrentStep { get; private set; } = -1;
        public bool IsRunning => CurrentStep >= 0;

        readonly List<IDisposable> _subs = new();
        Coroutine _run;
        bool _clickAdvance;     // 이번 스텝에서 유효 클릭 발생
        bool _waitingForClick;  // 클릭 대기 중(표시 완료 후)

        void OnEnable() => _subs.Add(EventBus.Subscribe<StartTutorialCommand>(_ => Play()));

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
            if (_run != null) { StopCoroutine(_run); _run = null; }
            CurrentStep = -1;
        }

        /// <summary>시퀀스 재생(이미 재생 중이면 무시). 완료 기록 여부는 호출측(컨트롤러) 판단.</summary>
        public void Play()
        {
            if (IsRunning || sequence == null || sequence.Steps.Count == 0) return;
            _run = StartCoroutine(Run());
        }

        IEnumerator Run()
        {
            if (root != null) root.SetActive(true);

            for (int i = 0; i < sequence.Steps.Count; i++)
            {
                CurrentStep = i;
                var step = sequence.Steps[i];

                // 표시 전 지연(기획: 첫 스텝 진입 2초 후) — 지연 동안은 말풍선 숨김, 딤은 유지.
                SetBubbleVisible(false);
                ApplyDim(step);
                if (step.appearDelay > 0f)
                    yield return Wait(step.appearDelay);

                ApplyStep(step);
                SetBubbleVisible(true);

                // 진행 대기: 자동 스텝 = n초, 그 외 = 유효 클릭(제한 스텝은 지정 앵커만).
                _clickAdvance = false;
                _waitingForClick = true;
                if (step.autoAdvanceSeconds > 0f)
                    yield return Wait(step.autoAdvanceSeconds);
                else
                    while (!_clickAdvance) yield return null;
                _waitingForClick = false;
            }

            Finish();
        }

        void ApplyStep(TutorialSequenceSO.Step step)
        {
            if (bubbleText != null)
            {
                string playerName = state != null ? state.Data.playerName : "";
                string resolved = TutorialService.ResolveText(step.text, playerName);
                bubbleText.text = resolved;
                ResizeBubble(resolved);
            }
            if (roaImage != null)
            {
                var sprite = step.emote switch
                {
                    RoaTutorialEmote.Beam => roaBeam,
                    RoaTutorialEmote.Smile => roaSmile,
                    RoaTutorialEmote.Bright => roaSmile, // 아트 3종 — 밝게웃음은 눈웃음 폴백
                    _ => roaBasic
                };
                if (sprite != null) roaImage.sprite = sprite;
            }
            if (roaGroup != null) roaGroup.anchoredPosition = step.roaPosition;
        }

        void ApplyDim(TutorialSequenceSO.Step step)
        {
            if (dimImage == null) return;
            var sprite = step.dim != null ? step.dim : fallbackDim;
            dimImage.sprite = sprite;
            dimImage.enabled = sprite != null;
        }

        void SetBubbleVisible(bool visible)
        {
            if (roaGroup != null) roaGroup.gameObject.SetActive(visible);
        }

        /// <summary>
        /// 말풍선 자동 크기(목업 사양) — 최대 폭에서 줄바꿈한 텍스트 선호 크기를 측정해
        /// 가로(긴 줄)+세로(줄 수)를 함께 키운다. 9-슬라이스 보호용 최소 폭 클램프.
        /// </summary>
        void ResizeBubble(string resolvedText)
        {
            if (bubbleRect == null || bubbleText == null) return;

            Vector2 preferred = bubbleText.GetPreferredValues(resolvedText, bubbleMaxTextWidth, 0f);
            float textW = Mathf.Min(preferred.x, bubbleMaxTextWidth);
            float textH = preferred.y;

            float w = Mathf.Max(bubbleMinWidth, textW + bubblePadding.x);
            float h = textH + bubblePadding.y;
            bubbleRect.sizeDelta = new Vector2(w, h);

            var textRt = bubbleText.transform as RectTransform;
            if (textRt != null) textRt.sizeDelta = new Vector2(textW, textH);
        }

        /// <summary>딤 클릭 수신(TutorialClickCatcher) — 진행/게이트 판정(기획: 클릭마다 다음, 제한 스텝은 지정 버튼만).</summary>
        public void HandleClick(PointerEventData eventData)
        {
            if (!IsRunning || !_waitingForClick) return;
            var step = sequence.Steps[CurrentStep];

            if (TutorialService.AdvancesOnAnyClick(step))
            {
                _clickAdvance = true;
                return;
            }

            // 강제 클릭 스텝 — 지정 앵커 영역 클릭만 실제 버튼으로 패스스루 후 진행.
            var anchor = TutorialAnchor.Find(step.requiredClickAnchor);
            if (anchor == null)
            {
                // 앵커 미부착(씬 배선 전) — 막히지 않게 통과(fail-open, 경고 1회).
                Log.Warn($"[TutorialView] 앵커 '{step.requiredClickAnchor}' 미발견 — 게이트 없이 진행(씬에 TutorialAnchor 부착 필요).");
                _clickAdvance = true;
                return;
            }

            if (RectTransformUtility.RectangleContainsScreenPoint(anchor.Rect, eventData.position, eventData.pressEventCamera))
            {
                anchor.Invoke(); // 실제 버튼 실행(상점 진입/돌아가기)
                _clickAdvance = true;
            }
            // 영역 밖 클릭은 흡수(기획: "버튼만 누를 수 있게 제한")
        }

        void Finish()
        {
            CurrentStep = -1;
            _run = null;
            if (root != null) root.SetActive(false);
            TutorialFlag.MarkDone(sequence != null ? sequence.prefsKey : null);
            EventBus.Publish(new TutorialFinishedEvent());
        }

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }
    }
}
