using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using LoveAlgo.Core;
using LoveAlgo.Data;

namespace LoveAlgo.Story
{
    /// <summary>
    /// 배경 전환 타입
    /// </summary>
    public enum BGTransition
    {
        Cut,    // 즉시 교체
        Fade,   // 페이드 (검은색 경유)
        Cross   // 크로스페이드
    }

    /// <summary>
    /// 배경 레이어 - 크로스페이드 지원
    /// </summary>
    public class BackgroundLayer : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Image imageFront;
        [SerializeField] CanvasGroup frontCanvasGroup;
        [SerializeField] Image imageBack;
        [SerializeField] CanvasGroup backCanvasGroup;

        [Header("설정")]
        [SerializeField] float defaultDuration = 0.5f;

        string currentBackground;

        public string CurrentBackground => currentBackground;

        void Awake()
        {
            // 초기 상태
            SetImageAlpha(frontCanvasGroup, 1f);
            SetImageAlpha(backCanvasGroup, 0f);
            if (imageBack != null) imageBack.enabled = false;
        }

        /// <summary>
        /// BG 명령 실행
        /// Value 형식: 배경이름[:전환타입:시간]
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            string bgName = parts[0];

            // 전환 타입 파싱 (기본값: Fade)
            BGTransition transition = BGTransition.Fade;
            float duration = defaultDuration;

            if (parts.Length >= 2)
            {
                transition = ParseTransition(parts[1]);
            }
            if (parts.Length >= 3 && float.TryParse(parts[2], out float d))
            {
                duration = d;
            }

            await ChangeBackgroundAsync(bgName, transition, duration, ct);
        }

        /// <summary>
        /// 배경 전환
        /// </summary>
        public async UniTask ChangeBackgroundAsync(string bgName, BGTransition transition, float duration, CancellationToken ct = default)
        {
            // 스프라이트 로드
            var sprite = LoadSprite(bgName);
            if (sprite == null)
            {
                Debug.LogWarning($"[BackgroundLayer] 스프라이트 없음: {bgName}");
                return;
            }

            currentBackground = bgName;

            switch (transition)
            {
                case BGTransition.Cut:
                    await CutAsync(sprite, ct);
                    break;
                case BGTransition.Fade:
                    await FadeAsync(sprite, duration, ct);
                    break;
                case BGTransition.Cross:
                    await CrossFadeAsync(sprite, duration, ct);
                    break;
            }
        }

        /// <summary>
        /// 즉시 교체
        /// </summary>
        async UniTask CutAsync(Sprite sprite, CancellationToken ct)
        {
            imageFront.sprite = sprite;
            imageFront.enabled = true;
            SetImageAlpha(frontCanvasGroup, 1f);
            await UniTask.CompletedTask;
        }

        /// <summary>
        /// 페이드 (검은 화면 경유: FadeOut → 배경 교체 → FadeIn)
        /// </summary>
        async UniTask FadeAsync(Sprite sprite, float duration, CancellationToken ct)
        {
            var screenFX = ScreenFX.Instance;
            float halfDuration = duration * 0.5f;
            
            // 화면 어둡게
            if (screenFX != null)
            {
                await screenFX.FadeOutAsync(halfDuration, ct);
            }
            
            // 배경 즉시 교체
            imageFront.sprite = sprite;
            imageFront.enabled = true;
            SetImageAlpha(frontCanvasGroup, 1f);
            
            // 화면 밝게
            if (screenFX != null)
            {
                await screenFX.FadeInAsync(halfDuration, ct);
            }
        }

        /// <summary>
        /// 크로스페이드
        /// </summary>
        async UniTask CrossFadeAsync(Sprite sprite, float duration, CancellationToken ct)
        {
            // Back 이미지에 새 배경 설정
            imageBack.sprite = sprite;
            imageBack.enabled = true;
            SetImageAlpha(backCanvasGroup, 0f);

            // 크로스페이드: Back 페이드인 + Front 페이드아웃
            var sequence = DOTween.Sequence();
            _ = sequence.Join(backCanvasGroup.DOFade(1f, duration).SetEase(Ease.InOutSine));
            _ = sequence.Join(frontCanvasGroup.DOFade(0f, duration).SetEase(Ease.InOutSine));

            try
            {
                await sequence.ToUniTask(cancellationToken: ct);
            }
            finally
            {
                // 취소되더라도 반드시 스왑 완료 (일관된 상태 보장)
                sequence.Kill();
                imageFront.sprite = imageBack.sprite;
                SetImageAlpha(frontCanvasGroup, 1f);
                SetImageAlpha(backCanvasGroup, 0f);
                imageBack.enabled = false;
            }
        }

        /// <summary>
        /// 즉시 클리어
        /// </summary>
        public void Clear()
        {
            imageFront.sprite = null;
            imageFront.enabled = false;
            imageBack.sprite = null;
            imageBack.enabled = false;
            currentBackground = null;
        }

        /// <summary>
        /// 스프라이트 로드 (BgPathMapping 데이터 사용)
        /// </summary>
        Sprite LoadSprite(string bgName)
        {
            var candidates = BgPathResolver.ResolvePaths(bgName);
            for (int i = 0; i < candidates.Count; i++)
            {
                var sprite = Resources.Load<Sprite>(candidates[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }

            return null;
        }

        /// <summary>
        /// 전환 타입 파싱
        /// </summary>
        BGTransition ParseTransition(string str)
        {
            switch (str.ToLower())
            {
                case "cut": return BGTransition.Cut;
                case "fade": return BGTransition.Fade;
                case "cross": return BGTransition.Cross;
                default: return BGTransition.Cut;
            }
        }

        void SetImageAlpha(CanvasGroup cg, float alpha)
        {
            if (cg != null)
            {
                cg.alpha = alpha;
            }
        }
    }
}
