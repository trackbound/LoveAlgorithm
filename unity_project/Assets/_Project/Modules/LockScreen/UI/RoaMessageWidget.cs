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
    /// 동작 (PlaySequence):
    ///   - baseline = 슬롯[N-1].originalAnchoredPos (가장 아래 = 최신 메시지 자리).
    ///   - 슬롯[s]의 표시 위치 = baseline + Vector2.up * (N-1-s) * stackOffsetY.
    ///     즉, prefab에서 슬롯을 어디 배치하든 코드가 가장 아래 슬롯 위치 기준으로 강제 정렬.
    ///   - 슬롯[s]의 alpha = baseAlpha - (N-1-s)*alphaDecay  → 위로 갈수록 흐림.
    ///   - 새 메시지: 큐 한 칸 shift. 기존 슬롯들은 자기 자리에서 한 단계 위 자리로
    ///     부드럽게 lerp 이동(잔상이 위로 떠오르는 느낌), 가장 아래 슬롯은 새 텍스트로
    ///     아래에서 슬라이드인.
    ///
    /// prefab 배치: 슬롯들을 prefab에서 일렬로 배치해도 OK. 코드가 baseline 기준으로
    /// 모두 같은 X·시작Y에서 stackOffsetY 간격으로 자동 stack-up. 안정성을 위해
    /// 슬롯들의 anchor·pivot은 동일하게 두는 것을 권장.
    /// </summary>
    public class RoaMessageWidget : MonoBehaviour
    {
        [Header("Slots (위에서 아래 순서)")]
        [Tooltip("기획서 권장: 4개. 슬롯[0]=가장 위(오래됨), 마지막=가장 아래(최신).")]
        [SerializeField] List<MessageSlot> slots = new List<MessageSlot>();

        [Header("Animation")]
        [Tooltip("메시지 1개 출력 후 다음까지 대기 (초)")]
        [SerializeField] float intervalSec = 1.0f;
        [Tooltip("새 메시지 슬라이드인 + 기존 슬롯들의 stack-up 이동 시간 (초)")]
        [SerializeField] float slideInDuration = 0.35f;
        [Tooltip("슬라이드인 시작 Y 오프셋 (px, 음수=아래에서)")]
        [SerializeField] float slideFromOffsetY = -60f;
        [Tooltip("한 단계 위로 갈수록 누적되는 Y 오프셋 (px, 양수=위로). 같은 위치에 배치된 슬롯들을 stack-up.")]
        [SerializeField] float stackOffsetY = 36f;
        [Tooltip("슬롯[i]의 alpha = baseAlpha - (N-1-i) * alphaDecay")]
        [SerializeField] float baseAlpha = 1.0f;
        [SerializeField] float alphaDecay = 0.3f;

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

            // 슬롯을 Y 내림차순으로 자동 정렬 → 코드 가정(slots[0]=가장 위, slots[N-1]=가장 아래) 보장.
            // prefab/Builder에서 어떤 순서로 추가됐든 baseline·depth·큐 매핑이 일관되게 동작.
            // null 슬롯은 뒤로 밀어 깨끗한 인덱스 유지.
            slots.Sort((a, b) =>
            {
                if (a == null && b == null) return 0;
                if (a == null) return 1;
                if (b == null) return -1;
                return b.originalAnchoredPos.y.CompareTo(a.originalAnchoredPos.y);
            });

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

            int n = slots.Count;
            var queue = new List<string>(n);

            for (int i = 0; i < messages.Count; i++)
            {
                queue.Add(messages[i]);
                if (queue.Count > n) queue.RemoveAt(0);

                // 1) 직전 상태(prev) 저장 — transition lerp의 시작점
                var prevPos = new Vector2[n];
                var prevAlpha = new float[n];
                for (int s = 0; s < n; s++)
                {
                    if (slots[s] == null) continue;
                    prevPos[s] = slots[s].rect != null ? slots[s].rect.anchoredPosition : Vector2.zero;
                    prevAlpha[s] = slots[s].group != null ? slots[s].group.alpha : 0f;
                }

                // 2) 새 텍스트 / target 위치·알파 계산
                //   baseline = 가장 아래 슬롯(=최신 자리)의 originalAnchoredPos.
                //   prefab에서 슬롯들이 일렬로 떨어져 배치되어도, 코드가 baseline 기준으로
                //   모두 같은 시작 위치 + stackOffsetY 간격으로 강제 정렬.
                Vector2 baseline = (n > 0 && slots[n - 1] != null)
                    ? slots[n - 1].originalAnchoredPos
                    : Vector2.zero;

                var targetPos = new Vector2[n];
                var targetAlpha = new float[n];
                for (int s = 0; s < n; s++)
                {
                    var slot = slots[s];
                    if (slot == null) continue;

                    int qIdx = queue.Count - (n - s);
                    int depth = n - 1 - s;  // 0=baseline(가장 아래), n-1=가장 위
                    targetPos[s] = baseline + Vector2.up * (depth * stackOffsetY);

                    if (qIdx < 0)
                    {
                        if (slot.text != null) slot.text.text = "";
                        targetAlpha[s] = 0f;
                        continue;
                    }

                    if (slot.text != null) slot.text.text = queue[qIdx];

                    float a = baseAlpha - depth * alphaDecay;
                    targetAlpha[s] = a < 0f ? 0f : a;
                }

                // 3) 가장 아래 슬롯(최신)은 아래에서 슬라이드인 (prev를 override)
                int lastIdx = n - 1;
                if (lastIdx >= 0 && slots[lastIdx] != null)
                {
                    prevPos[lastIdx] = targetPos[lastIdx] + new Vector2(0f, slideFromOffsetY);
                    prevAlpha[lastIdx] = 0f;
                }

                // 4) 모든 슬롯 동시 transition (잔상 위로 stack-up + 신규 슬라이드인)
                yield return TransitionRoutine(prevPos, prevAlpha, targetPos, targetAlpha, slideInDuration);

                OnMessageShown?.Invoke();

                if (i < messages.Count - 1)
                    yield return new WaitForSecondsRealtime(intervalSec);
            }
        }

        IEnumerator TransitionRoutine(Vector2[] fromPos, float[] fromAlpha, Vector2[] toPos, float[] toAlpha, float duration)
        {
            int n = slots.Count;

            for (int s = 0; s < n; s++)
            {
                if (slots[s] == null) continue;
                if (slots[s].rect != null) slots[s].rect.anchoredPosition = fromPos[s];
                if (slots[s].group != null) slots[s].group.alpha = fromAlpha[s];
            }

            if (duration <= 0f)
            {
                for (int s = 0; s < n; s++)
                {
                    if (slots[s] == null) continue;
                    if (slots[s].rect != null) slots[s].rect.anchoredPosition = toPos[s];
                    if (slots[s].group != null) slots[s].group.alpha = toAlpha[s];
                }
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(t / duration);
                float ease = 1f - (1f - p) * (1f - p);
                for (int s = 0; s < n; s++)
                {
                    if (slots[s] == null) continue;
                    if (slots[s].rect != null) slots[s].rect.anchoredPosition = Vector2.Lerp(fromPos[s], toPos[s], ease);
                    if (slots[s].group != null) slots[s].group.alpha = Mathf.Lerp(fromAlpha[s], toAlpha[s], ease);
                }
                yield return null;
            }

            for (int s = 0; s < n; s++)
            {
                if (slots[s] == null) continue;
                if (slots[s].rect != null) slots[s].rect.anchoredPosition = toPos[s];
                if (slots[s].group != null) slots[s].group.alpha = toAlpha[s];
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
            if (slots[lastIdx].rect != null) slots[lastIdx].rect.anchoredPosition = slots[lastIdx].originalAnchoredPos;
        }
    }
}
