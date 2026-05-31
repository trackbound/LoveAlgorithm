using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Story
{
    /// <summary>
    /// SD 컷씬 레이어 - 배경/캐릭터 위에 부분 영역으로 표시되는 SD 이미지
    /// 대사창은 유지되며, 캐릭터 자동 숨김/복원은 ScriptRunner에서 제어
    /// 
    /// Stage 계층: Background → VirtualBG → Character → SDCutscene → MonologueDim → CG → EyeEffect
    /// 
    /// CSV 사용법:
    ///   ,SD,,SD_Roa_Chibi_01:FadeIn:0.5,await     — SD 이미지 표시
    ///   ,SD,,SD_Roa_Chibi_02:Cross:0.3,await       — 다른 SD로 크로스페이드 전환
    ///   ,SD,,Exit:0.5,await                        — SD 숨기기
    /// </summary>
    public class SDCutsceneLayer : MonoBehaviour
    {
        [Header("바인딩")]
        [SerializeField] Image sdImageFront;
        [SerializeField] CanvasGroup frontCanvasGroup;
        [SerializeField] Image sdImageBack;
        [SerializeField] CanvasGroup backCanvasGroup;
        [SerializeField] CanvasGroup layerCanvasGroup;

        [Header("설정")]
        [SerializeField] float defaultDuration = 0.5f;

        string currentSD;
        bool isShowing;

        /// <summary>현재 표시 중인 SD 이름</summary>
        public string CurrentSD => currentSD;

        /// <summary>SD 표시 중 여부</summary>
        public bool IsShowing => isShowing;

        void OnValidate()
        {
            AutoBind();
        }

        void Awake()
        {
            AutoBind();

            // 초기 상태: 숨김
            if (layerCanvasGroup != null)
                layerCanvasGroup.alpha = 0f;
            if (sdImageFront != null)
                sdImageFront.enabled = false;
            if (sdImageBack != null)
                sdImageBack.enabled = false;
        }

        void AutoBind()
        {
            if (layerCanvasGroup == null)
                layerCanvasGroup = GetComponent<CanvasGroup>();

            // Front/Back 이미지는 자식에서 수동 바인딩 필요
        }

        /// <summary>
        /// SD 명령 실행
        /// Value 형식:
        ///   SD이름:FadeIn[:시간]   — 페이드인 표시
        ///   SD이름:Cross[:시간]    — 크로스페이드 전환
        ///   Exit[:시간]            — 페이드아웃 숨기기
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            string command = parts[0];

            // Exit / Close 명령
            // 형식: Close[:duration] 또는 Close:Fade:duration
            if (command.Equals("Exit", System.StringComparison.OrdinalIgnoreCase)
                || command.Equals("Close", System.StringComparison.OrdinalIgnoreCase))
            {
                float duration = defaultDuration;
                // Close:Fade:4 → parts[1]="Fade", parts[2]="4"
                // Close:2       → parts[1]="2"
                if (parts.Length >= 3 && float.TryParse(parts[2], out float d3))
                    duration = d3;
                else if (parts.Length >= 2 && float.TryParse(parts[1], out float d1))
                    duration = d1;

                await HideAsync(duration, ct);
                return;
            }

            // SD 표시/전환 명령
            string sdName = command;
            string transition = parts.Length >= 2 ? parts[1] : "FadeIn";
            float showDuration = defaultDuration;

            if (parts.Length >= 3 && float.TryParse(parts[2], out float parsedDuration))
                showDuration = parsedDuration;

            if (transition.Equals("Cross", System.StringComparison.OrdinalIgnoreCase) && isShowing)
                await CrossfadeAsync(sdName, showDuration, ct);
            else
                await ShowAsync(sdName, showDuration, ct);
        }

        /// <summary>
        /// SD 표시 (페이드인)
        /// </summary>
        public async UniTask ShowAsync(string sdName, float duration = 0.5f, CancellationToken ct = default)
        {
            var sprite = LoadSprite(sdName);
            if (sprite == null)
            {
                Debug.LogWarning($"[SDCutscene] 스프라이트 없음: {sdName}");
                return;
            }

            currentSD = sdName;
            isShowing = true;

            gameObject.SetActive(true);

            // Front 이미지에 설정
            sdImageFront.sprite = sprite;
            sdImageFront.enabled = true;
            sdImageFront.preserveAspect = true;

            if (frontCanvasGroup != null)
                frontCanvasGroup.alpha = 1f;

            // 레이어 페이드인
            if (layerCanvasGroup != null && duration > 0f)
            {
                layerCanvasGroup.alpha = 0f;
                await layerCanvasGroup.DOFade(1f, duration)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            else if (layerCanvasGroup != null)
            {
                layerCanvasGroup.alpha = 1f;
            }

            Debug.Log($"[SDCutscene] SD 표시: {sdName}");
        }

        /// <summary>
        /// SD 크로스페이드 전환 (현재 SD → 새 SD)
        /// </summary>
        public async UniTask CrossfadeAsync(string sdName, float duration = 0.5f, CancellationToken ct = default)
        {
            var sprite = LoadSprite(sdName);
            if (sprite == null)
            {
                Debug.LogWarning($"[SDCutscene] 스프라이트 없음: {sdName}");
                return;
            }

            currentSD = sdName;

            // Back에 새 이미지 설정
            sdImageBack.sprite = sdImageFront.sprite;
            sdImageBack.enabled = true;
            sdImageBack.preserveAspect = true;
            if (backCanvasGroup != null)
                backCanvasGroup.alpha = 1f;

            // Front에 새 이미지
            sdImageFront.sprite = sprite;
            sdImageFront.enabled = true;
            sdImageFront.preserveAspect = true;
            if (frontCanvasGroup != null)
                frontCanvasGroup.alpha = 0f;

            // 크로스페이드
            if (duration > 0f)
            {
                var seq = DOTween.Sequence();
                if (frontCanvasGroup != null)
                    _ = seq.Join(frontCanvasGroup.DOFade(1f, duration));
                if (backCanvasGroup != null)
                    _ = seq.Join(backCanvasGroup.DOFade(0f, duration));
                _ = seq.SetEase(Ease.Linear);

                await seq.ToUniTask(cancellationToken: ct);
            }
            else
            {
                if (frontCanvasGroup != null) frontCanvasGroup.alpha = 1f;
                if (backCanvasGroup != null) backCanvasGroup.alpha = 0f;
            }

            // Back 정리
            sdImageBack.enabled = false;
            sdImageBack.sprite = null;

            Debug.Log($"[SDCutscene] SD 전환: {sdName}");
        }

        /// <summary>
        /// SD 숨기기 (페이드아웃)
        /// </summary>
        public async UniTask HideAsync(float duration = 0.5f, CancellationToken ct = default)
        {
            if (!isShowing) return;

            DOTween.Kill(layerCanvasGroup);
            DOTween.Kill(frontCanvasGroup);
            DOTween.Kill(backCanvasGroup);

            if (layerCanvasGroup != null && duration > 0f)
            {
                await layerCanvasGroup.DOFade(0f, duration)
                    .SetEase(Ease.InQuad)
                    .ToUniTask(cancellationToken: ct);
            }
            else if (layerCanvasGroup != null)
            {
                layerCanvasGroup.alpha = 0f;
            }

            // 에셋 해제
            var oldSprite = sdImageFront.sprite;

            sdImageFront.enabled = false;
            sdImageFront.sprite = null;
            sdImageBack.enabled = false;
            sdImageBack.sprite = null;
            currentSD = null;
            isShowing = false;
            gameObject.SetActive(false);

            if (oldSprite != null)
                Resources.UnloadAsset(oldSprite.texture);

            Debug.Log("[SDCutscene] SD 숨김 (에셋 해제됨)");
        }

        /// <summary>
        /// 즉시 클리어 (애니메이션 없이)
        /// </summary>
        public void Clear()
        {
            DOTween.Kill(layerCanvasGroup);
            DOTween.Kill(frontCanvasGroup);
            DOTween.Kill(backCanvasGroup);

            if (layerCanvasGroup != null)
                layerCanvasGroup.alpha = 0f;

            Sprite oldSprite = null;
            if (sdImageFront != null)
            {
                oldSprite = sdImageFront.sprite;
                sdImageFront.enabled = false;
                sdImageFront.sprite = null;
            }
            if (sdImageBack != null)
            {
                sdImageBack.enabled = false;
                sdImageBack.sprite = null;
            }
            currentSD = null;
            isShowing = false;
            gameObject.SetActive(false);

            if (oldSprite != null)
                Resources.UnloadAsset(oldSprite.texture);
        }

        /// <summary>
        /// 즉시 숨김 (애니메이션 없이, 에셋 유지)
        /// </summary>
        public void HideImmediate()
        {
            DOTween.Kill(layerCanvasGroup);
            DOTween.Kill(frontCanvasGroup);
            DOTween.Kill(backCanvasGroup);

            if (layerCanvasGroup != null)
                layerCanvasGroup.alpha = 0f;
            if (sdImageFront != null)
                sdImageFront.enabled = false;
            if (sdImageBack != null)
                sdImageBack.enabled = false;
            currentSD = null;
            isShowing = false;
        }

        /// <summary>
        /// 스프라이트 로드 — SD/{sdName} 경로로 직접 로드
        /// </summary>
        Sprite LoadSprite(string sdName)
        {
            if (string.IsNullOrEmpty(sdName)) return null;
            if (sdName.StartsWith("SD/", System.StringComparison.OrdinalIgnoreCase))
                sdName = sdName.Substring(3);

            // 1. ResourceCatalogSO 우선
            var catalog = LoveAlgo.Story.Data.ResourceCatalogSO.Instance;
            if (catalog != null && catalog.TryGetSd(sdName, out var byCatalog))
                return byCatalog;

            // 2. StoryMappings 한글→영문 폴백
            if (StoryMappings.TryResolveSd(sdName, out var resolved))
                sdName = resolved;

            // 3. Resources.Load 폴백
            return Resources.Load<Sprite>($"SD/{sdName}");
        }

        void OnDestroy()
        {
            DOTween.Kill(layerCanvasGroup);
            DOTween.Kill(frontCanvasGroup);
            DOTween.Kill(backCanvasGroup);
        }
    }
}
