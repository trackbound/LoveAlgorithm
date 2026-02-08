using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 가상 배경 오버레이 - 배경 위에 옅은 보조 배경 표시
    /// 로아의 가상공간 등 캐릭터별 테마 배경에 사용
    /// </summary>
    public class VirtualBGOverlay : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Image overlayImage;
        [SerializeField] CanvasGroup canvasGroup;

        [Header("설정")]
        [SerializeField] float defaultDuration = 0.5f;
        [SerializeField] float defaultAlpha = 0.7f;  // 기본 투명도 (70%)

        string currentOverlay;
        bool isShowing;

        /// <summary>
        /// 현재 오버레이 이름
        /// </summary>
        public string CurrentOverlay => currentOverlay;

        /// <summary>
        /// 오버레이 표시 중 여부
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
            if (overlayImage != null)
            {
                overlayImage.enabled = false;
            }
        }

        void AutoBind()
        {
            if (overlayImage == null)
            {
                overlayImage = GetComponentInChildren<Image>(true);
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
        /// 오버레이 명령 실행
        /// Value 형식: 
        ///   이름:FadeIn[:시간[:투명도]]  - 표시
        ///   FadeOut[:시간]              - 숨김
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            string first = parts[0];

            // FadeOut 명령
            if (first.Equals("FadeOut", System.StringComparison.OrdinalIgnoreCase))
            {
                float duration = defaultDuration;
                if (parts.Length >= 2 && float.TryParse(parts[1], out float d))
                {
                    duration = d;
                }
                await HideAsync(duration, ct);
                return;
            }

            // FadeIn 명령: 이름:FadeIn[:시간[:투명도]]
            string overlayName = first;
            float showDuration = defaultDuration;
            float targetAlpha = defaultAlpha;

            if (parts.Length >= 3 && float.TryParse(parts[2], out float parsedDuration))
            {
                showDuration = parsedDuration;
            }
            if (parts.Length >= 4 && float.TryParse(parts[3], out float parsedAlpha))
            {
                targetAlpha = parsedAlpha;
            }

            await ShowAsync(overlayName, showDuration, targetAlpha, ct);
        }

        /// <summary>
        /// 오버레이 표시 (페이드인)
        /// </summary>
        public async UniTask ShowAsync(string overlayName, float duration = 0.5f, float targetAlpha = 0.7f, CancellationToken ct = default)
        {
            // 스프라이트 로드
            var sprite = LoadSprite(overlayName);
            if (sprite == null)
            {
                Debug.LogWarning($"[VirtualBGOverlay] 스프라이트 없음: {overlayName}");
                return;
            }

            currentOverlay = overlayName;
            isShowing = true;

            // 이미지 설정
            overlayImage.sprite = sprite;
            overlayImage.enabled = true;
            overlayImage.preserveAspect = true;

            // 페이드인
            if (duration > 0f)
            {
                await canvasGroup.DOFade(targetAlpha, duration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            else
            {
                canvasGroup.alpha = targetAlpha;
            }
        }

        /// <summary>
        /// 오버레이 숨김 (페이드아웃)
        /// </summary>
        public async UniTask HideAsync(float duration = 0.5f, CancellationToken ct = default)
        {
            if (!isShowing) return;

            // 페이드아웃
            if (duration > 0f)
            {
                await canvasGroup.DOFade(0f, duration)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            else
            {
                canvasGroup.alpha = 0f;
            }

            // 정리
            overlayImage.enabled = false;
            currentOverlay = null;
            isShowing = false;
        }

        /// <summary>
        /// 스프라이트 로드 (Resources/Overlays/에서)
        /// </summary>
        Sprite LoadSprite(string name)
        {
            // Resources/Overlays/이름 에서 로드
            var sprite = Resources.Load<Sprite>($"Overlays/{name}");
            if (sprite != null) return sprite;

            // 없으면 Resources/Backgrounds/에서 시도
            sprite = Resources.Load<Sprite>($"Backgrounds/{name}");
            return sprite;
        }

        /// <summary>
        /// 즉시 숨김 (애니메이션 없이)
        /// </summary>
        public void HideImmediate()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
            }
            if (overlayImage != null)
            {
                overlayImage.enabled = false;
            }
            currentOverlay = null;
            isShowing = false;
        }
    }
}
