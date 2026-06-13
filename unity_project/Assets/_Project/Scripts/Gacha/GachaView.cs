using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using LoveAlgo.Common; // EventBus
using LoveAlgo.Core;   // GameStateSO
using LoveAlgo.Events;

namespace LoveAlgo.Gacha
{
    /// <summary>
    /// 가챠 화면(*View, 기획 p46~49) — 퍼즐판(6×5)·모은 수 카운터·전체화면 보기·나가기 + 오픈 연출
    /// (가챠권 뿅 등장→흔들→조각 공개→해당 칸으로 비행→안착, 완성 시 컨페티→전체화면 자동 전환).
    /// 추첨·기록은 GachaController가 이미 확정(GachaDrawResultEvent 수신) — 여기는 표시/연출만(ADR-007).
    /// 미완성 상태의 전체화면 보기도 허용(기획). 연출 수치 = GachaTuningSO(감독 튜닝).
    /// </summary>
    public class GachaView : MonoBehaviour
    {
        [SerializeField] GameObject root;
        [SerializeField] RectTransform boardContainer; // GridLayoutGroup
        [SerializeField] GachaPieceSlot pieceSlotPrefab;
        [SerializeField] TMP_Text counterText;
        [Header("오픈 연출(중앙 슬롯 구역)")]
        [SerializeField] RectTransform ticketImage;   // 가챠권(뿅 등장 + 흔들)
        [SerializeField] RectTransform pieceFlyImage; // 공개된 조각(칸으로 비행)
        [SerializeField] TMP_Text bonusLabel;         // 완성 후 구매 안내("이미 완성!")
        [SerializeField] RectTransform confettiContainer;
        [Header("전체화면")]
        [SerializeField] GameObject fullscreenRoot;
        [SerializeField] Image fullscreenImage;
        [SerializeField] Button fullscreenButton;     // "전체화면 보기"
        [SerializeField] Button fullscreenCloseButton;// 우하단 작게(기획)
        [SerializeField] Button exitButton;           // 나가기
        [Header("데이터")]
        [SerializeField] GameStateSO state;
        [SerializeField] GachaTuningSO tuning;

        public GameObject Root { get => root; set => root = value; }
        public RectTransform BoardContainer { get => boardContainer; set => boardContainer = value; }
        public GachaPieceSlot PieceSlotPrefab { get => pieceSlotPrefab; set => pieceSlotPrefab = value; }
        public TMP_Text CounterText { get => counterText; set => counterText = value; }
        public RectTransform TicketImage { get => ticketImage; set => ticketImage = value; }
        public RectTransform PieceFlyImage { get => pieceFlyImage; set => pieceFlyImage = value; }
        public TMP_Text BonusLabel { get => bonusLabel; set => bonusLabel = value; }
        public RectTransform ConfettiContainer { get => confettiContainer; set => confettiContainer = value; }
        public GameObject FullscreenRoot { get => fullscreenRoot; set => fullscreenRoot = value; }
        public Image FullscreenImage { get => fullscreenImage; set => fullscreenImage = value; }
        public Button FullscreenButton { get => fullscreenButton; set => fullscreenButton = value; }
        public Button FullscreenCloseButton { get => fullscreenCloseButton; set => fullscreenCloseButton = value; }
        public Button ExitButton { get => exitButton; set => exitButton = value; }
        public GameStateSO State { get => state; set => state = value; }
        public GachaTuningSO Tuning { get => tuning; set => tuning = value; }

        readonly List<GachaPieceSlot> _slots = new();
        readonly List<IDisposable> _subs = new();
        Coroutine _reveal;

        void Awake()
        {
            if (fullscreenButton != null) fullscreenButton.onClick.AddListener(() => ShowFullscreen(true));
            if (fullscreenCloseButton != null) fullscreenCloseButton.onClick.AddListener(() => ShowFullscreen(false));
            if (exitButton != null) exitButton.onClick.AddListener(() => EventBus.Publish(new CloseGachaCommand()));
        }

        void OnEnable()
        {
            _subs.Add(EventBus.Subscribe<OpenGachaCommand>(OnOpen));
            _subs.Add(EventBus.Subscribe<CloseGachaCommand>(_ => Close()));
            _subs.Add(EventBus.Subscribe<GachaDrawResultEvent>(OnDrawResult));
        }

        void OnDisable()
        {
            foreach (var s in _subs) s?.Dispose();
            _subs.Clear();
            StopReveal();
        }

        void OnOpen(OpenGachaCommand cmd)
        {
            if (root != null) root.SetActive(true);
            ShowFullscreen(false);
            EnsureBoard();
            RefreshBoard();
            // 현황 보기 진입은 슬롯 구역 비움(기획: "구매 없이 보러만 들어오면 비어 있음").
            // 구매 진입의 연출은 GachaDrawResultEvent(컨트롤러가 같은 프레임 발행)가 시작한다.
            HideRevealProps();
        }

        void Close()
        {
            StopReveal();
            HideRevealProps();
            ShowFullscreen(false);
            if (root != null) root.SetActive(false);
        }

        void OnDrawResult(GachaDrawResultEvent e)
        {
            if (root == null || !root.activeSelf) return; // 화면 밖 결과는 무시(열기 명령이 선행되는 계약)
            StopReveal();
            _reveal = StartCoroutine(RevealRoutine(e));
        }

        // ── 퍼즐판 ──

        void EnsureBoard()
        {
            if (boardContainer == null || pieceSlotPrefab == null || tuning == null) return;
            if (_slots.Count == tuning.PieceCount) return;

            foreach (var s in _slots) if (s != null) Destroy(s.gameObject);
            _slots.Clear();

            var grid = boardContainer.GetComponent<GridLayoutGroup>();
            Vector2 cell = grid != null ? grid.cellSize : new Vector2(100, 100);
            Vector2 boardSize = new(cell.x * tuning.columns, cell.y * tuning.rows);

            for (int i = 0; i < tuning.PieceCount; i++)
            {
                var slot = Instantiate(pieceSlotPrefab, boardContainer);
                slot.Setup(i, i % tuning.columns, i / tuning.columns, cell, boardSize, tuning.illustration);
                _slots.Add(slot);
            }
        }

        void RefreshBoard()
        {
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetOwned(GachaPuzzleService.IsOwned(state, i));
            UpdateCounter();
        }

        void UpdateCounter()
        {
            if (counterText != null && tuning != null)
                counterText.text = $"{GachaPuzzleService.OwnedCount(state)}/{tuning.PieceCount}";
        }

        // ── 오픈 연출 ──

        IEnumerator RevealRoutine(GachaDrawResultEvent e)
        {
            HideRevealProps();
            // 연출 전 보드는 "이번 조각 제외" 상태로 보여준다(새 조각이 날아가 안착하는 그림).
            for (int i = 0; i < _slots.Count; i++)
                _slots[i].SetOwned(GachaPuzzleService.IsOwned(state, i) && i != e.PieceIndex);

            // 1) 가챠권 뿅 등장(기획: 진입 후 약 1초 안에) + 흔들흔들
            if (ticketImage != null)
            {
                ticketImage.gameObject.SetActive(true);
                yield return ScalePop(ticketImage, 0.25f);
                yield return Shake(ticketImage, tuning != null ? tuning.ticketShakeDuration : 2f);
                ticketImage.gameObject.SetActive(false);
            }

            if (e.IsBonus)
            {
                // 완성 후 추가 구매 — 새 조각 없음, 안내만(업적 카운트는 이미 기록됨).
                if (bonusLabel != null)
                {
                    bonusLabel.gameObject.SetActive(true);
                    yield return Wait(1.2f);
                    bonusLabel.gameObject.SetActive(false);
                }
                _reveal = null;
                yield break;
            }

            // 2) 조각 공개(뒤집힘 느낌 = X 스케일 플립) → 해당 칸으로 비행 → 안착
            if (pieceFlyImage != null && e.PieceIndex >= 0 && e.PieceIndex < _slots.Count)
            {
                pieceFlyImage.gameObject.SetActive(true);
                pieceFlyImage.anchoredPosition = Vector2.zero;
                yield return Flip(pieceFlyImage, 0.3f);

                var target = (RectTransform)_slots[e.PieceIndex].transform;
                yield return FlyTo(pieceFlyImage, target, tuning != null ? tuning.pieceFlyDuration : 0.6f);
                pieceFlyImage.gameObject.SetActive(false);
            }

            RefreshBoard(); // 안착 반영(+카운터)

            // 3) 완성 — 컨페티 + 전체화면 자동 전환(기획 p49)
            if (e.IsComplete)
            {
                yield return Confetti(1.2f);
                yield return Wait(tuning != null ? tuning.completeLineFadeDuration : 0.8f);
                ShowFullscreen(true);
            }
            _reveal = null;
        }

        void StopReveal()
        {
            if (_reveal != null) { StopCoroutine(_reveal); _reveal = null; }
        }

        void HideRevealProps()
        {
            if (ticketImage != null) ticketImage.gameObject.SetActive(false);
            if (pieceFlyImage != null) pieceFlyImage.gameObject.SetActive(false);
            if (bonusLabel != null) bonusLabel.gameObject.SetActive(false);
        }

        void ShowFullscreen(bool show)
        {
            if (fullscreenRoot == null) return;
            if (show && fullscreenImage != null && tuning != null && tuning.illustration != null)
                fullscreenImage.sprite = tuning.illustration;
            fullscreenRoot.SetActive(show); // 미완성 상태 그대로도 허용(기획)
        }

        // ── 연출 프리미티브(그레이박스 — 수치는 튜닝 SO/감독 영역) ──

        static IEnumerator Wait(float seconds)
        {
            for (float t = 0f; t < seconds; t += Time.deltaTime) yield return null;
        }

        static IEnumerator ScalePop(RectTransform rt, float duration)
        {
            float dur = Mathf.Max(0.0001f, duration);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                rt.localScale = Vector3.one * Mathf.LerpUnclamped(0.2f, 1f, k);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        static IEnumerator Shake(RectTransform rt, float duration)
        {
            Vector2 basePos = rt.anchoredPosition;
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                float decay = 1f - (t / Mathf.Max(0.0001f, duration)) * 0.3f;
                float z = Mathf.Sin(t * 24f) * 9f * decay;
                rt.localRotation = Quaternion.Euler(0f, 0f, z);
                yield return null;
            }
            rt.localRotation = Quaternion.identity;
            rt.anchoredPosition = basePos;
        }

        static IEnumerator Flip(RectTransform rt, float duration)
        {
            float dur = Mathf.Max(0.0001f, duration);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = t / dur;
                rt.localScale = new Vector3(Mathf.Abs(Mathf.Cos(k * Mathf.PI)), 1f, 1f);
                yield return null;
            }
            rt.localScale = Vector3.one;
        }

        IEnumerator FlyTo(RectTransform mover, RectTransform target, float duration)
        {
            Vector3 from = mover.position;
            float dur = Mathf.Max(0.0001f, duration);
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0f, 1f, t / dur);
                mover.position = Vector3.LerpUnclamped(from, target.position, k);
                mover.localScale = Vector3.one * Mathf.LerpUnclamped(1f, 0.5f, k);
                yield return null;
            }
            mover.localScale = Vector3.one;
        }

        IEnumerator Confetti(float duration)
        {
            if (confettiContainer == null) yield break;
            var pieces = new List<RectTransform>();
            var colors = new[] { new Color(1f, 0.6f, 0.72f), new Color(1f, 0.84f, 0.4f), new Color(0.55f, 0.78f, 1f), new Color(0.65f, 0.9f, 0.6f) };
            for (int i = 0; i < 24; i++)
            {
                var go = new GameObject("Confetti", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(confettiContainer, false);
                var img = go.GetComponent<Image>();
                img.color = colors[i % colors.Length];
                img.raycastTarget = false;
                var rt = (RectTransform)go.transform;
                rt.sizeDelta = new Vector2(14, 20);
                rt.anchoredPosition = new Vector2(UnityEngine.Random.Range(-360f, 360f), UnityEngine.Random.Range(60f, 240f));
                pieces.Add(rt);
            }
            for (float t = 0f; t < duration; t += Time.deltaTime)
            {
                foreach (var rt in pieces)
                {
                    rt.anchoredPosition += new Vector2(Mathf.Sin((t + rt.GetEntityId().GetHashCode() % 7) * 6f) * 1.6f, -260f * Time.deltaTime);
                    rt.localRotation = Quaternion.Euler(0f, 0f, t * 240f + rt.GetEntityId().GetHashCode() % 360);
                }
                yield return null;
            }
            foreach (var rt in pieces) if (rt != null) Destroy(rt.gameObject);
        }
    }
}
