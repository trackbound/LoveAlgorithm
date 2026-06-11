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

        Coroutine _move;

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
                float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                ApplyPose(new Pose(
                    Vector2.Lerp(from.pos, target.pos, k),
                    Mathf.Lerp(from.scale, target.scale, k),
                    Mathf.Lerp(from.alpha, target.alpha, k)));
                yield return null;
            }
            ApplyPose(target);
            _move = null;
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
