using System.Collections;
using TMPro;
using UnityEngine;

namespace LoveAlgo.MessageStack
{
    /// <summary>
    /// 메시지 스택의 카드 1장(*View). 위치/스케일/알파를 코루틴 lerp로 옮기는 수동 뷰 — 슬롯 배정·생성/파괴는
    /// <see cref="MessageStackController"/>가 주도한다. DOTween 미사용(ScreenFadeView와 동일 관례), 알파는 CanvasGroup.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class MessageCardView : MonoBehaviour
    {
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] RectTransform rect;
        [Tooltip("'Message from {sender}' 표시 라벨.")]
        [SerializeField] TMP_Text senderLabel;
        [Tooltip("대사 본문 라벨.")]
        [SerializeField] TMP_Text messageLabel;
        [Tooltip("접힘(collapsed) 시 숨길 파츠들(예: Label, Text). Header는 항상 표시 — 오래된 메시지는 헤더만 남는다.")]
        [SerializeField] GameObject[] collapsibleParts;

        [Header("Slide Feel")]
        [Tooltip("이동 보간 오버슈트(EaseOutBack) 강도. 0=오버슈트 없는 부드러운 감속, 클수록 끝에서 톡 튀는 손맛.")]
        [SerializeField] float overshoot = 1.4f;

        Coroutine _move;
        bool _collapsed;

        /// <summary>카드의 목표 자세: anchoredPosition / 균일 스케일 / CanvasGroup 알파.</summary>
        public struct Pose
        {
            public Vector2 pos;
            public float scale;
            public float alpha;

            public Pose(Vector2 pos, float scale, float alpha)
            {
                this.pos = pos;
                this.scale = scale;
                this.alpha = alpha;
            }
        }

        void Reset()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            rect = (RectTransform)transform;
        }

        void Awake()
        {
            if (canvasGroup == null) canvasGroup = GetComponent<CanvasGroup>();
            if (rect == null) rect = (RectTransform)transform;
        }

        public void SetContent(string sender, string message)
        {
            if (senderLabel != null) senderLabel.text = $"Message from {sender}";
            if (messageLabel != null) messageLabel.text = message;
        }

        /// <summary>접힘 토글. 접히면 collapsibleParts(Label/Text 등)를 끄고 Header만 남긴다(오래된 메시지 표현).</summary>
        public void SetCollapsed(bool collapsed)
        {
            if (_collapsed == collapsed) return;
            _collapsed = collapsed;
            if (collapsibleParts == null) return;
            foreach (var go in collapsibleParts)
                if (go != null) go.SetActive(!collapsed);
        }

        /// <summary>등장 직전 시작 자세를 즉시 적용(진행 중 이동은 중단).</summary>
        public void SetPoseInstant(Pose p)
        {
            if (_move != null) { StopCoroutine(_move); _move = null; }
            ApplyPose(p);
        }

        /// <summary>현재 자세에서 target까지 코루틴 lerp. 진행 중이면 재타게팅(부드럽게 이어짐).</summary>
        public void AnimateTo(Pose target, float duration)
        {
            if (_move != null) StopCoroutine(_move);
            if (duration <= 0f || !isActiveAndEnabled)
            {
                ApplyPose(target);
                _move = null;
                return;
            }
            _move = StartCoroutine(MoveRoutine(target, duration));
        }

        IEnumerator MoveRoutine(Pose target, float duration)
        {
            Pose from = CurrentPose();
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float lin = Mathf.Clamp01(t / duration);
                // 위치/스케일은 EaseOutBack로 끝에서 살짝 넘었다 정착(손맛), 알파는 SmoothStep로 깜빡임 없이 감쇠.
                float kPos = EaseOutBack(lin, overshoot);
                float kFade = Mathf.SmoothStep(0f, 1f, lin);
                ApplyPose(new Pose(
                    Vector2.LerpUnclamped(from.pos, target.pos, kPos),
                    Mathf.LerpUnclamped(from.scale, target.scale, kPos),
                    Mathf.Lerp(from.alpha, target.alpha, kFade)));
                yield return null;
            }
            ApplyPose(target);
            _move = null;
        }

        /// <summary>EaseOutBack: x∈[0,1]에서 끝부분이 1을 살짝 넘었다가 되돌아온다. s=오버슈트 강도(0이면 오버슈트 없음).</summary>
        static float EaseOutBack(float x, float s)
        {
            float c3 = s + 1f;
            float xm = x - 1f;
            return 1f + c3 * xm * xm * xm + s * xm * xm;
        }

        Pose CurrentPose() => new Pose(rect.anchoredPosition, rect.localScale.x, canvasGroup.alpha);

        void ApplyPose(Pose p)
        {
            rect.anchoredPosition = p.pos;
            rect.localScale = new Vector3(p.scale, p.scale, 1f);
            canvasGroup.alpha = p.alpha;
        }
    }
}
