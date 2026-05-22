using LoveAlgo.Contracts;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace LoveAlgo.LockScreen.UI
{
    /// <summary>
    /// 로아 메시지 위젯 (기획서 §구성: 메시지 4개 순차 출력).
    ///
    /// 시퀀스 (PlaySequence 호출 시):
    ///   - 첫 메시지: 가장 아래 슬롯에 슬라이드인(0→1, 아래→원위치) + 효과음 콜백
    ///   - 다음 메시지: queue가 한 칸씩 위로 밀림. 위쪽일수록 alpha 낮음(baseAlpha - i*alphaDecay).
    ///
    /// 슬롯 풀: 인스펙터에서 4개 RectTransform/CanvasGroup/TMP_Text 미리 배치.
    /// 슬롯[0]=가장 위(가장 오래된), 슬롯[N-1]=가장 아래(최신).
    /// </summary>
    public class RoaMessageWidget : MonoBehaviour
    {
        [Header("Slots (위에서 아래 순서)")]
        [Tooltip("기획서 권장: 4개. 슬롯[0]=가장 위(오래됨), 마지막=가장 아래(최신).")]
        [SerializeField] List<MessageSlot> slots = new List<MessageSlot>();

        [Header("Animation")]
        [Tooltip("메시지 1개 출력 후 다음까지 대기 (초)")]
        [SerializeField] float intervalSec = 1.0f;
        [SerializeField] float slideInDuration = 0.35f;
        [Tooltip("슬라이드인 시작 Y 오프셋 (px, 음수=아래에서)")]
        [SerializeField] float slideFromOffsetY = -60f;
        [Tooltip("슬롯[i]의 alpha = baseAlpha - i * alphaDecay")]
        [SerializeField] float baseAlpha = 1.0f;
        [SerializeField] float alphaDecay = 0.25f;

        [Header("Hide Animation")]
        [SerializeField] float hideSlideDownDistance = 200f;
        [SerializeField] float hideDuration = 0.4f;

        [Serializable]
        public class MessageSlot
        {
            public CanvasGroup group;
            public RectTransform rect;
            public TMP_Text text;
            [HideInInspector] public Vector2 originalAnchoredPos;
        }

        Coroutine seqCo;

        /// <summary>메시지 1개 출력 시 호출 (효과음 트리거).</summary>
        public event Action OnMessageShown;

        void Awake()
        {
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].rect != null)
                    slots[i].originalAnchoredPos = slots[i].rect.anchoredPosition;
            }
            HideAllImmediate();
        }

        public void HideAllImmediate()
        {
            foreach (var s in slots)
            {
                if (s == null) continue;
                if (s.group != null) s.group.alpha = 0f;
                if (s.text != null) s.text.text = "";
            }
        }

        /// <summary>
        /// 메시지 시퀀스 재생. 외부 코루틴에서 yield return 가능.
        /// </summary>
        public IEnumerator PlaySequence(IList<string> messages)
        {
            if (messages == null || messages.Count == 0) yield break;
            if (seqCo != null) StopCoroutine(seqCo);

            HideAllImmediate();

            var queue = new List<string>(slots.Count);

            for (int i = 0; i < messages.Count; i++)
            {
                queue.Add(messages[i]);
                if (queue.Count > slots.Count) queue.RemoveAt(0);

                int n = slots.Count;
                for (int s = 0; s < n; s++)
                {
                    var slot = slots[s];
                    if (slot == null) continue;

                    int qIdx = queue.Count - (n - s);
                    if (qIdx < 0)
                    {
                        if (slot.group != null) slot.group.alpha = 0f;
                        if (slot.text != null) slot.text.text = "";
                        continue;
                    }

                    if (slot.text != null) slot.text.text = queue[qIdx];

                    // 위쪽일수록(인덱스 작을수록) 더 흐림
                    float a = baseAlpha - (n - 1 - s) * alphaDecay;
                    if (a < 0f) a = 0f;
                    if (slot.group != null) slot.group.alpha = a;

                    if (slot.rect != null) slot.rect.anchoredPosition = slot.originalAnchoredPos;
                }

                int lastIdx = slots.Count - 1;
                if (lastIdx >= 0 && slots[lastIdx] != null)
                    yield return SlideInRoutine(slots[lastIdx]);

                OnMessageShown?.Invoke();

                if (i < messages.Count - 1)
                    yield return new WaitForSecondsRealtime(intervalSec);
            }
        }

        public IEnumerator HideRoutine()
        {
            float t = 0f;
            var startPos = new Vector2[slots.Count];
            var startAlpha = new float[slots.Count];
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] == null) continue;
                startPos[i] = slots[i].rect != null ? slots[i].rect.anchoredPosition : Vector2.zero;
                startAlpha[i] = slots[i].group != null ? slots[i].group.alpha : 1f;
            }

            while (t < hideDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / hideDuration);
                float ease = p * p;
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] == null) continue;
                    if (slots[i].rect != null)
                        slots[i].rect.anchoredPosition = startPos[i] + Vector2.down * hideSlideDownDistance * ease;
                    if (slots[i].group != null)
                        slots[i].group.alpha = Mathf.Lerp(startAlpha[i], 0f, p);
                }
                yield return null;
            }

            HideAllImmediate();
            for (int i = 0; i < slots.Count; i++)
            {
                if (slots[i] != null && slots[i].rect != null)
                    slots[i].rect.anchoredPosition = slots[i].originalAnchoredPos;
            }
        }

        // ── 정적 단일 표시 (호환) ──
        public void ShowIndex(ILockScreen lockScreen, int index)
        {
            if (lockScreen == null) return;
            ShowText(lockScreen.GetRoaMessage(index));
        }

        public void ShowText(string text)
        {
            int lastIdx = slots.Count - 1;
            if (lastIdx < 0 || slots[lastIdx] == null) return;
            if (slots[lastIdx].text != null) slots[lastIdx].text.text = text ?? "";
            if (slots[lastIdx].group != null) slots[lastIdx].group.alpha = 1f;
        }

        IEnumerator SlideInRoutine(MessageSlot slot)
        {
            if (slot == null) yield break;
            Vector2 dest = slot.originalAnchoredPos;
            Vector2 from = dest + new Vector2(0f, slideFromOffsetY);

            if (slot.rect != null) slot.rect.anchoredPosition = from;
            if (slot.group != null) slot.group.alpha = 0f;

            float t = 0f;
            while (t < slideInDuration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / slideInDuration);
                float ease = 1f - (1f - p) * (1f - p);
                if (slot.rect != null) slot.rect.anchoredPosition = Vector2.Lerp(from, dest, ease);
                if (slot.group != null) slot.group.alpha = Mathf.Lerp(0f, baseAlpha, ease);
                yield return null;
            }

            if (slot.rect != null) slot.rect.anchoredPosition = dest;
            if (slot.group != null) slot.group.alpha = baseAlpha;
        }
    }
}
