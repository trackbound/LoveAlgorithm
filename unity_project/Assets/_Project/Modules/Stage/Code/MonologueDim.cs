using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 독백 딤 레이어 - 독백 시 화면 외곽에 어두운 비네팅/테두리 표시
    /// Stage 계층: Background → VirtualBG → Character → MonologueDim → CG → EyeEffect
    /// </summary>
    public class MonologueDim : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Image dimImage;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("설정")]
        [SerializeField] float defaultDuration = 0.3f;
        [SerializeField] float defaultAlpha = 1f;

        bool isShowing;

        /// <summary>
        /// 딤 표시 중 여부
        /// </summary>
        public bool IsShowing => isShowing;

        void OnValidate()
        {
            AutoBind();
        }

        void Awake()
        {
            AutoBind();

            // 초기 상태: 숨김
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (dimImage != null)
            {
                dimImage.enabled = false;
            }
        }

        void AutoBind()
        {
            if (dimImage == null)
            {
                dimImage = GetComponentInChildren<Image>(true);
            }
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null)
                {
                    canvasGroup = GetComponentInChildren<CanvasGroup>(true);
                }
            }
        }

        /// <summary>
        /// 딤 표시 (페이드인)
        /// </summary>
        public async UniTask ShowAsync(float duration = -1f, float targetAlpha = -1f, CancellationToken ct = default)
        {
            if (isShowing) return;

            if (duration < 0f) duration = defaultDuration;
            if (targetAlpha < 0f) targetAlpha = defaultAlpha;

            isShowing = true;

            if (dimImage != null)
            {
                dimImage.enabled = true;
            }

            if (canvasGroup != null)
            {
                if (duration > 0f)
                {
                    await canvasGroup.DOFade(targetAlpha, duration)
                        .SetEase(Ease.OutCubic)
                        .ToUniTask(cancellationToken: ct);
                }
                else
                {
                    canvasGroup.alpha = targetAlpha;
                }
            }
        }

        /// <summary>
        /// 딤 숨김 (페이드아웃)
        /// </summary>
        public async UniTask HideAsync(float duration = -1f, CancellationToken ct = default)
        {
            if (!isShowing) return;

            if (duration < 0f) duration = defaultDuration;

            if (canvasGroup != null)
            {
                if (duration > 0f)
                {
                    await canvasGroup.DOFade(0f, duration)
                        .SetEase(Ease.InCubic)
                        .ToUniTask(cancellationToken: ct);
                }
                else
                {
                    canvasGroup.alpha = 0f;
                }
            }

            if (dimImage != null)
            {
                dimImage.enabled = false;
            }

            isShowing = false;
        }

        /// <summary>
        /// 즉시 숨김 (애니메이션 없이)
        /// </summary>
        public void HideImmediate()
        {
            DOTween.Kill(canvasGroup);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (dimImage != null)
            {
                dimImage.enabled = false;
            }
            isShowing = false;
        }

        /// <summary>
        /// 즉시 표시 (애니메이션 없이)
        /// </summary>
        public void ShowImmediate(float alpha = -1f)
        {
            DOTween.Kill(canvasGroup);

            if (alpha < 0f) alpha = defaultAlpha;

            if (dimImage != null)
            {
                dimImage.enabled = true;
            }
            if (canvasGroup != null)
            {
                canvasGroup.alpha = alpha;
            }
            isShowing = true;
        }
    }
}
