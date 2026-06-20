using System;
using System.Collections;
using System.Collections.Generic;
using LoveAlgo.Common; // Log
using UnityEngine;

namespace LoveAlgo.MessageStack
{
    /// <summary>
    /// 연출용 간이 메시지 스택 오케스트레이터. <see cref="MessageSequenceSO"/>의 대사를 자동 타이머로 한 줄씩
    /// 화면 아래에서 앵커(슬롯0)로 띄우고, 기존 카드는 위로+투명 처리하며 스택한다(크기는 변하지 않음). 최대 maxVisible장 유지,
    /// 초과 시 가장 오래된 카드를 파괴(카카오톡/iMessage 알림 스택 느낌).
    /// EventBus/기존 대화 시스템과 무관한 자급자족 컴포넌트. 애니=코루틴 lerp(ScreenFadeView 관례),
    /// 수치는 인스펙터 노출(ADR-012 매직넘버 금지).
    /// </summary>
    public class MessageStackController : MonoBehaviour
    {
        [Header("Refs")]
        [Tooltip("복제할 카드 프리팹(MessageCardView).")]
        [SerializeField] MessageCardView cardPrefab;
        [Tooltip("카드가 생성될 부모 RectTransform(스택 컨테이너).")]
        [SerializeField] RectTransform cardParent;
        [Tooltip("재생할 대사 시퀀스 SO.")]
        [SerializeField] MessageSequenceSO sequence;

        [Header("Layout")]
        [Tooltip("슬롯0(최전면 카드)의 기준 anchoredPosition.")]
        [SerializeField] Vector2 anchorPos = Vector2.zero;
        [Tooltip("동시에 겹쳐 보일 최대 카드 수.")]
        [SerializeField] int maxVisible = 4;
        [Tooltip("슬롯0(최전면 풀 카드)→슬롯1(첫 접힘 카드) 간격(px). 풀 카드 본문 위로 헤더가 떠야 하므로 stepY보다 크게.")]
        [SerializeField] float firstStepY = 56f;
        [Tooltip("접힘 카드끼리 한 슬롯 뒤로 갈 때마다 위로 올리는 픽셀. 작게 둘수록 오래된 헤더가 촘촘히 겹쳐 쌓인다.")]
        [SerializeField] float stepY = 28f;
        [Tooltip("슬롯별 알파(index0=최전면). 길이 maxVisible+1 권장 — 마지막은 퇴장 직전 값.")]
        [SerializeField] float[] alphaBySlot = { 1f, 0.55f, 0.3f, 0.15f, 0f };

        [Header("Timing")]
        [Tooltip("신규 카드가 화면 아래에서 앵커로 올라오는 시간(초).")]
        [SerializeField] float riseDuration = 0.35f;
        [Tooltip("기존 카드가 한 슬롯 뒤로 밀리는 시간(초).")]
        [SerializeField] float shiftDuration = 0.3f;
        [Tooltip("신규 카드 시작 위치(앵커 아래로 내려둘 픽셀).")]
        [SerializeField] float incomingDropY = 220f;

        [Header("Playback")]
        [Tooltip("Start에서 자동 재생할지 여부.")]
        [SerializeField] bool playOnStart = true;

        readonly List<MessageCardView> _active = new(); // index0 = 최신/최전면 = 슬롯0

        /// <summary>카드 1장이 스폰될 때마다 발화(메시지 도착 신호 — SFX 후크 등).</summary>
        public event Action MessageSpawned;
        /// <summary>시퀀스 전체가 끝났을 때 1회 발화(연출 종료 핸드오프 신호).</summary>
        public event Action Completed;

        Coroutine _play;

        void Start()
        {
            if (playOnStart) Play();
        }

        void OnDisable()
        {
            if (_play != null) { StopCoroutine(_play); _play = null; }
        }

        /// <summary>시퀀스를 처음부터 자동 재생.</summary>
        public void Play()
        {
            if (sequence == null || cardPrefab == null || cardParent == null)
            {
                Log.Warn("[MsgStack] 참조 누락(sequence/cardPrefab/cardParent) — 재생 생략.");
                return;
            }
            if (_play != null) StopCoroutine(_play);
            _play = StartCoroutine(PlayRoutine());
        }

        IEnumerator PlayRoutine()
        {
            if (sequence.StartDelay > 0f) yield return new WaitForSeconds(sequence.StartDelay);
            foreach (var line in sequence.Lines)
            {
                if (line == null) continue;
                if (line.delay > 0f) yield return new WaitForSeconds(line.delay);
                Spawn(line.text);
            }
            _play = null;
            Completed?.Invoke();
        }

        void Spawn(string text)
        {
            var card = Instantiate(cardPrefab, cardParent);
            card.SetContent(sequence.SenderName, text);
            card.SetPoseInstant(IncomingPose());
            _active.Insert(0, card);

            // 생존 카드(0..maxVisible-1) 슬롯 재배정: 신규는 아래→슬롯0 상승, 기존은 한 슬롯 뒤로.
            // 슬롯0만 풀 카드, 나머지는 접힘(헤더만) — 오래될수록 촘촘+연하게.
            int survivors = Mathf.Min(_active.Count, maxVisible);
            for (int i = 0; i < survivors; i++)
            {
                _active[i].SetCollapsed(i != 0);
                _active[i].AnimateTo(PoseForSlot(i), i == 0 ? riseDuration : shiftDuration);
            }

            // 렌더 순서: 최신이 위로 그려지도록 뒤→앞 SetAsLastSibling(index0이 마지막=최상단).
            for (int i = survivors - 1; i >= 0; i--)
                _active[i].transform.SetAsLastSibling();

            // 초과분(가장 오래된) 퇴장 + 파괴.
            while (_active.Count > maxVisible)
            {
                var old = _active[_active.Count - 1];
                _active.RemoveAt(_active.Count - 1);
                old.AnimateTo(ExitPose(), shiftDuration);
                StartCoroutine(DestroyAfter(old, shiftDuration));
            }

            Log.Info($"[MsgStack] spawn '{text}' active={_active.Count}");
            MessageSpawned?.Invoke();
        }

        IEnumerator DestroyAfter(MessageCardView card, float delay)
        {
            if (delay > 0f) yield return new WaitForSeconds(delay);
            if (card != null) Destroy(card.gameObject);
        }

        MessageCardView.Pose PoseForSlot(int slot)
        {
            // 크기 변화 없음(scale 고정 1) — 위로 이동 + 알파 감소만으로 스택.
            // 슬롯0=기준, 슬롯1=firstStepY(풀 카드 위), 이후 접힘 카드끼리는 stepY로 촘촘히.
            float y = slot <= 0 ? 0f : firstStepY + (slot - 1) * stepY;
            Vector2 pos = anchorPos + new Vector2(0f, y);
            return new MessageCardView.Pose(pos, 1f, AlphaForSlot(slot));
        }

        /// <summary>신규 카드 시작 자세: 앵커 아래·완전 투명(여기서 슬롯0으로 상승).</summary>
        MessageCardView.Pose IncomingPose()
            => new MessageCardView.Pose(anchorPos + new Vector2(0f, -incomingDropY), 1f, 0f);

        /// <summary>퇴장 자세: 마지막 슬롯 위치로 더 밀려나며 완전 투명.</summary>
        MessageCardView.Pose ExitPose()
        {
            var p = PoseForSlot(maxVisible);
            p.alpha = 0f;
            return p;
        }

        float AlphaForSlot(int slot)
        {
            if (alphaBySlot == null || alphaBySlot.Length == 0) return slot == 0 ? 1f : 0f;
            return alphaBySlot[Mathf.Clamp(slot, 0, alphaBySlot.Length - 1)];
        }
    }
}
