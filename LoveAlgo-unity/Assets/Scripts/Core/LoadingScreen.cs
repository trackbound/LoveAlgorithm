using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 로딩 화면 (날짜 전환 / 자동저장 시 캐릭터 일러스트 표시)
    /// Canvas_ScreenFX 위에 배치, ScreenFX FadeOverlay 위 레이어
    /// </summary>
    public class LoadingScreen : SingletonMonoBehaviour<LoadingScreen>
    {
        [Header("바인딩")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image characterImage;

        [Header("설정")]
        [SerializeField] float fadeInDuration = 0.4f;
        [SerializeField] float fadeOutDuration = 0.3f;
        [SerializeField] float minDisplayTime = 1.0f;

        /// <summary>현재 표시 중인지</summary>
        public bool IsShowing { get; private set; }

        /// <summary>
        /// Resources/UI/Loading/ 폴더의 로딩 이미지 목록
        /// 캐릭터별 2장씩, 총 10장
        /// </summary>
        static readonly string[] LoadingImagePaths =
        {
            "UI/Loading/Load_Bom_01",
            "UI/Loading/Load_Bom_02",
            "UI/Loading/Load_Daeun_01",
            "UI/Loading/Load_Daeun_02",
            "UI/Loading/Load_Heewon_01",
            "UI/Loading/Load_Heewon_02",
            "UI/Loading/Load_Roa_01",
            "UI/Loading/Load_Roa_02",
            "UI/Loading/Load_Yeun_01",
            "UI/Loading/Load_Yeun_02",
        };

        int lastIndex = -1;

        protected override void OnSingletonAwake()
        {
            // 초기 상태: 숨김
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.gameObject.SetActive(false);
            }
        }

        /// <summary>
        /// 로딩 화면 표시 (랜덤 캐릭터 이미지)
        /// </summary>
        public async UniTask ShowAsync(CancellationToken ct = default)
        {
            if (canvasGroup == null || characterImage == null)
            {
                Debug.LogWarning("[LoadingScreen] 바인딩 누락");
                return;
            }

            IsShowing = true;

            // 랜덤 이미지 선택 (직전과 겹치지 않게)
            int index;
            do
            {
                index = Random.Range(0, LoadingImagePaths.Length);
            } while (index == lastIndex && LoadingImagePaths.Length > 1);
            lastIndex = index;

            var sprite = Resources.Load<Sprite>(LoadingImagePaths[index]);
            if (sprite != null)
            {
                characterImage.sprite = sprite;
                characterImage.preserveAspect = true;
            }
            else
            {
                Debug.LogWarning($"[LoadingScreen] 이미지 로드 실패: {LoadingImagePaths[index]}");
            }

            // 페이드인
            canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = 0f;
            await canvasGroup.DOFade(1f, fadeInDuration)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: ct);

            Debug.Log($"[LoadingScreen] 표시: {LoadingImagePaths[index]}");
        }

        /// <summary>
        /// 로딩 화면 숨기기
        /// </summary>
        public async UniTask HideAsync(CancellationToken ct = default)
        {
            if (canvasGroup == null || !IsShowing) return;

            await canvasGroup.DOFade(0f, fadeOutDuration)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: ct);

            canvasGroup.gameObject.SetActive(false);
            IsShowing = false;

            Debug.Log("[LoadingScreen] 숨김 완료");
        }

        /// <summary>
        /// 로딩 표시 → 최소 시간 보장 → 숨기기 (일체형)
        /// 자동저장 + 로딩을 한 번에 처리할 때 사용
        /// </summary>
        public async UniTask ShowForAsync(float duration, CancellationToken ct = default)
        {
            await ShowAsync(ct);

            float wait = Mathf.Max(duration, minDisplayTime) - fadeInDuration;
            if (wait > 0f)
                await UniTask.Delay(
                    System.TimeSpan.FromSeconds(wait),
                    cancellationToken: ct
                );

            await HideAsync(ct);
        }

        /// <summary>
        /// 즉시 숨기기 (애니메이션 없이)
        /// </summary>
        public void HideImmediate()
        {
            if (canvasGroup == null) return;
            DOTween.Kill(canvasGroup);
            canvasGroup.alpha = 0f;
            canvasGroup.gameObject.SetActive(false);
            IsShowing = false;
        }
    }
}
