using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 로딩 화면 (날짜 전환 / 자동저장 시 캐릭터 일러스트 표시)
    /// 2레이어 구조: 검은 배경(blackOverlay) + 캐릭터 일러스트(characterImage)
    /// Canvas_ScreenFX 위에 배치, ScreenFX FadeOverlay 위 레이어
    /// </summary>
    public class LoadingScreen : SingletonMonoBehaviour<LoadingScreen>
    {
        [Header("바인딩")]
        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Image characterImage;
        [SerializeField] Image blackOverlay;

        // ── Timing (FXDefaultsConfig SO 단일 정전) ──
        // 인스펙터 SerializedField 제거 — 사용자가 값 조정은 Resources/Data/FXDefaultsConfig.asset에서만.
        static float FadeIn  => FXDefaultsConfig.Instance != null
            ? FXDefaultsConfig.Instance.loadingScreenFadeIn  : 0.4f;
        static float FadeOut => FXDefaultsConfig.Instance != null
            ? FXDefaultsConfig.Instance.loadingScreenFadeOut : 0.3f;
        static float MinHold => FXDefaultsConfig.Instance != null
            ? FXDefaultsConfig.Instance.loadingScreenMinHold : 1.5f;

        /// <summary>현재 표시 중인지</summary>
        public bool IsShowing { get; private set; }

        /// <summary>페이드인 소요 시간 (외부 참조용)</summary>
        public float FadeInDuration => FadeIn;

        /// <summary>최소 표시 시간 (외부 참조용)</summary>
        public float MinDisplayTime => MinHold;

        /// <summary>
        /// Resources/UI/Loading/ 폴더의 로딩 이미지 목록
        /// 캐릭터별 2장씩, 총 10장
        /// </summary>
        static readonly string[] LoadingImagePaths =
        {
            "UI/Loading/Load_LeeBom_01",
            "UI/Loading/Load_LeeBom_02",
            "UI/Loading/Load_SeoDaEun_01",
            "UI/Loading/Load_SeoDaEun_02",
            "UI/Loading/Load_DoHeewon_01",
            "UI/Loading/Load_DoHeewon_02",
            "UI/Loading/Load_Roa_01",
            "UI/Loading/Load_Roa_02",
            "UI/Loading/Load_HaYeEun_01",
            "UI/Loading/Load_HaYeEun_02",
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
            EnsureBlackOverlay();
        }

        /// <summary>blackOverlay가 없으면 자동 생성 (프리팹에 미바인딩 시 폴백)</summary>
        void EnsureBlackOverlay()
        {
            if (blackOverlay != null) return;
            var go = new GameObject("BlackOverlay", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            go.transform.SetAsFirstSibling();
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            blackOverlay = go.GetComponent<Image>();
            blackOverlay.color = Color.black;
            blackOverlay.raycastTarget = true;
        }

        /// <summary>
        /// 로딩 화면 표시 (검은 배경 즉시 + 일러스트 페이드인)
        /// </summary>
        public async UniTask ShowAsync(CancellationToken ct = default)
        {
            if (canvasGroup == null || characterImage == null)
            {
                Debug.LogWarning("[LoadingScreen] 바인딩 누락");
                return;
            }

            IsShowing = true;
            EnsureBlackOverlay();

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

            // 검은 배경 즉시 표시, 일러스트 투명 상태로 시작
            blackOverlay.color = Color.black;
            characterImage.color = new Color(1f, 1f, 1f, 0f);

            canvasGroup.gameObject.SetActive(true);
            canvasGroup.alpha = 1f;

            // 일러스트만 페이드인
            await characterImage.DOFade(1f, FadeIn)
                .SetEase(Ease.OutQuad)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: ct);

            Debug.Log($"[LoadingScreen] 표시: {LoadingImagePaths[index]}");
        }

        /// <summary>
        /// 일러스트만 페이드아웃 (검은 배경은 유지 — 화면 노출 방지)
        /// </summary>
        public async UniTask HideIllustrationAsync(CancellationToken ct = default)
        {
            if (characterImage == null || !IsShowing) return;

            DOTween.Kill(characterImage);
            await characterImage.DOFade(0f, FadeOut)
                .SetEase(Ease.InQuad)
                .SetUpdate(true)
                .ToUniTask(cancellationToken: ct);

            Debug.Log("[LoadingScreen] 일러스트 숨김 (검은 배경 유지)");
        }

        /// <summary>
        /// 로딩 화면 전체 숨기기 (일러스트 + 검은 배경 모두 페이드아웃)
        /// DayLoopController 등 ScreenFX로 감싸는 호출자용
        /// </summary>
        public async UniTask HideAsync(CancellationToken ct = default)
        {
            if (canvasGroup == null || !IsShowing) return;

            DOTween.Kill(characterImage);
            await canvasGroup.DOFade(0f, FadeOut)
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

            float wait = Mathf.Max(duration, MinHold) - FadeIn;
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
            DOTween.Kill(characterImage);
            characterImage.color = new Color(1f, 1f, 1f, 0f);
            canvasGroup.alpha = 0f;
            canvasGroup.gameObject.SetActive(false);
            IsShowing = false;
        }
    }
}
