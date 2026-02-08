using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 화면 효과 (FadeIn/Out, Flash 등)
    /// 별도 캔버스(Canvas_ScreenFX)에 배치, 전역 싱글톤
    /// </summary>
    public class ScreenFX : MonoBehaviour
    {
        public static ScreenFX Instance { get; private set; }

        [Header("바인딩")]
        [SerializeField] Image fadeOverlay;         // 검은색 오버레이
        [SerializeField] Image flashOverlay;        // 흰색 플래시용 (선택)
        
        [Header("Eye Open/Close 효과")]
        [SerializeField] Image eyeTop;              // 위쪽 검은 바 (Image)
        [SerializeField] Image eyeBottom;           // 아래쪽 검은 바 (Image)
        
        [Header("Camera Shake")]
        [Tooltip("Stage Canvas (Screen Space - Camera/Overlay). 바인딩 시 Camera/Transform 자동 추출")]
        [SerializeField] Canvas stageCanvas;
        [Tooltip("Camera Canvas 사용 시 Main Camera 바인딩")]
        [SerializeField] Camera stageCamera;        // Screen Space - Camera 모드용
        [Tooltip("Overlay Canvas 사용 시 Stage RectTransform 바인딩 (폴백)")]
        [SerializeField] RectTransform stageTransform; // 폴백용

        [Header("설정")]
        [SerializeField] float defaultFadeDuration = 1f;
        [SerializeField] float defaultFlashDuration = 0.1f;

        [Header("Shake 설정")]
        [Tooltip("흔들림 주파수 (높을수록 빠르게 진동)")]
        [SerializeField] float shakeFrequency = 25f;
        [Tooltip("Perlin Noise 시드 오프셋 (다양한 흔들림 패턴)")]
        [SerializeField] float noiseSeed = 0f;

        [Header("Shake 프리셋")]
        [SerializeField] float shakePresetWeak = 10f;
        [SerializeField] float shakePresetMedium = 25f;
        [SerializeField] float shakePresetStrong = 50f;

        /// <summary>페이드 오버레이가 검은 상태인지 (세이브용)</summary>
        public bool IsFadeBlack => fadeOverlay != null && fadeOverlay.color.a >= 0.95f;

        /// <summary>눈 감기 효과가 활성 상태인지 (세이브용)</summary>
        public bool IsEyeClosed => eyeTop != null && eyeTop.gameObject.activeSelf;

        void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                // DontDestroyOnLoad(gameObject);  // 데모: 단일 씬
            }
            else
            {
                Destroy(gameObject);
                return;
            }

            // 초기 상태: 투명
            if (fadeOverlay != null)
            {
                SetOverlayAlpha(fadeOverlay, 0f);
                fadeOverlay.raycastTarget = false;
            }
            if (flashOverlay != null)
            {
                SetOverlayAlpha(flashOverlay, 0f);
                flashOverlay.raycastTarget = false;
            }

            EnsureBindings();
        }

        void OnValidate()
        {
            EnsureBindings();
        }

        void OnDestroy()
        {
            KillEyeSequence();
        }

        void EnsureBindings()
        {
            if (stageCanvas == null) return;

            if (stageTransform == null)
            {
                stageTransform = stageCanvas.transform as RectTransform;
            }

            if (stageCamera == null && stageCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                stageCamera = stageCanvas.worldCamera;
            }
        }

        /// <summary>
        /// FX 명령 실행
        /// Value 형식: 효과명[:인자1:인자2:...]
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            string effect = parts[0];

        

            // 2. 기본 내장 효과 (DOTween 기반)
            switch (effect)
            {
                case "FadeOut":
                    float fadeOutDuration = parts.Length > 1 && float.TryParse(parts[1], out float fo) ? fo : defaultFadeDuration;
                    await FadeOutAsync(fadeOutDuration, ct);
                    break;

                case "FadeIn":
                    float fadeInDuration = parts.Length > 1 && float.TryParse(parts[1], out float fi) ? fi : defaultFadeDuration;
                    await FadeInAsync(fadeInDuration, ct);
                    break;

                case "Flash":
                    float flashDuration = parts.Length > 1 && float.TryParse(parts[1], out float fl) ? fl : defaultFlashDuration;
                    await FlashAsync(flashDuration, ct);
                    break;

                case "CamShake":
                    // CSV: CamShake:0.5:30 또는 CamShake:0.5:Weak/Medium/Strong
                    float shakeDuration = parts.Length > 1 && float.TryParse(parts[1], out float sd) ? sd : 0.3f;
                    float shakeStrength;
                    if (parts.Length > 2)
                    {
                        switch (parts[2].ToLower())
                        {
                            case "weak": shakeStrength = shakePresetWeak; break;
                            case "medium": shakeStrength = shakePresetMedium; break;
                            case "strong": shakeStrength = shakePresetStrong; break;
                            default:
                                shakeStrength = float.TryParse(parts[2], out float ss) ? ss : shakePresetMedium;
                                break;
                        }
                    }
                    else
                    {
                        shakeStrength = shakePresetMedium;
                    }
                    await CamShakeAsync(shakeDuration, shakeStrength, ct);
                    break;

                case "CamZoom":
                    // TODO: 카메라 줌
                    Debug.Log($"[ScreenFX] CamZoom: {value}");
                    break;

                case "EyeOpen":
                    // 눈 뜨는 효과: EyeOpen[:duration]
                    float eyeOpenDuration = parts.Length > 1 && float.TryParse(parts[1], out float eod) ? eod : 1f;
                    await EyeOpenAsync(eyeOpenDuration, ct);
                    break;

                case "EyeClose":
                    // 눈 감는 효과: EyeClose[:duration]
                    float eyeCloseDuration = parts.Length > 1 && float.TryParse(parts[1], out float ecd) ? ecd : 1f;
                    await EyeCloseAsync(eyeCloseDuration, ct);
                    break;

                case "EyeCloseImmediate":
                    // 눈 즉시 닫기 (애니메이션 없이)
                    EyeCloseImmediate();
                    break;

                case "EyeBlink":
                    // 눈 깜빡임: EyeBlink[:closeDuration:openDuration[:holdTime]]
                    float blinkClose = parts.Length > 1 && float.TryParse(parts[1], out float bc) ? bc : 0.1f;
                    float blinkOpen = parts.Length > 2 && float.TryParse(parts[2], out float bo) ? bo : 0.15f;
                    float blinkHold = parts.Length > 3 && float.TryParse(parts[3], out float bh) ? bh : 0.05f;
                    await EyeBlinkAsync(blinkClose, blinkOpen, blinkHold, ct);
                    break;

                case "CharShake":
                case "CharJump":
                case "CharDim":
                    // 캐릭터 효과는 CharacterLayer에서 처리하도록 전달 필요
                    Debug.Log($"[ScreenFX] 캐릭터 효과: {value}");
                    break;

                default:
                    Debug.LogWarning($"[ScreenFX] 알 수 없는 효과: {effect}");
                    break;
            }
        }

        #region Fade

        /// <summary>
        /// 화면 어둡게 (검은색으로 페이드)
        /// </summary>
        public async UniTask FadeOutAsync(float duration, CancellationToken ct = default)
        {
            if (fadeOverlay == null)
            {
                Debug.LogWarning("[ScreenFX] fadeOverlay가 없습니다.");
                return;
            }

            fadeOverlay.raycastTarget = true;
            await fadeOverlay.DOFade(1f, duration).SetEase(Ease.InQuad).ToUniTask(cancellationToken: ct);
        }

        /// <summary>
        /// 화면 밝게 (페이드 복귀)
        /// </summary>
        public async UniTask FadeInAsync(float duration, CancellationToken ct = default)
        {
            if (fadeOverlay == null)
            {
                Debug.LogWarning("[ScreenFX] fadeOverlay가 없습니다.");
                return;
            }

            await fadeOverlay.DOFade(0f, duration).SetEase(Ease.OutCubic).ToUniTask(cancellationToken: ct);
            fadeOverlay.raycastTarget = false;
        }

        /// <summary>
        /// 즉시 검은 화면
        /// </summary>
        public void SetBlack()
        {
            if (fadeOverlay != null)
            {
                SetOverlayAlpha(fadeOverlay, 1f);
                fadeOverlay.raycastTarget = true;
            }
        }

        /// <summary>
        /// 즉시 투명 — fadeOverlay + Eye 바 모두 초기화
        /// </summary>
        public void SetClear()
        {
            if (fadeOverlay != null)
            {
                SetOverlayAlpha(fadeOverlay, 0f);
                fadeOverlay.raycastTarget = false;
            }

            // Eye 바도 비활성화 (DayEnd 후 잔존 방지)
            EyeOpenImmediate();
        }

        #endregion

        #region Flash

        /// <summary>
        /// 화면 번쩍임
        /// </summary>
        public async UniTask FlashAsync(float duration, CancellationToken ct = default)
        {
            Image overlay = flashOverlay != null ? flashOverlay : fadeOverlay;

            if (overlay == null)
            {
                Debug.LogWarning("[ScreenFX] 오버레이가 없습니다.");
                return;
            }

            // 흰색으로 설정 (flashOverlay가 없으면 fadeOverlay 사용)
            Color originalColor = overlay.color;
            if (flashOverlay == null)
            {
                overlay.color = Color.white;
            }

            // 즉시 나타났다가 페이드아웃
            SetOverlayAlpha(overlay, 1f);
            await overlay.DOFade(0f, duration).ToUniTask(cancellationToken: ct);

            // 원래 색상 복원
            if (flashOverlay == null)
            {
                overlay.color = originalColor;
            }
        }

        #endregion

        #region Shake

        /// <summary>
        /// 카메라/Stage 흔들림 (duration, strength 직접 지정)
        /// CSV에서: FX,,CamShake:0.5:30 (0.5초, 강도 30)
        /// </summary>

        /// <summary>
        /// 카메라/Stage 흔들림 (커스텀 값)
        /// 우선순위: FEEL 피드백 > Stage RectTransform (Screen Space Camera) > World Camera
        /// </summary>
        public async UniTask CamShakeAsync(float duration, float strength, CancellationToken ct = default)
        {
            // 2. Screen Space - Camera 모드일 때는 RectTransform을 흔들어야 효과가 보임
            //    (카메라를 흔들면 Canvas가 따라가서 시각적 효과 없음)
            if (stageCanvas != null && stageCanvas.renderMode == RenderMode.ScreenSpaceCamera && stageTransform != null)
            {
                await ShakeRectTransformAsync(stageTransform, duration, strength, ct);
                return;
            }

            // 3. World Space나 Overlay 모드에서 카메라 바인딩 시 카메라 흔들기
            if (stageCamera != null)
            {
                await ShakeCameraAsync(stageCamera, duration, strength, ct);
                return;
            }

            // 4. 폴백: Stage RectTransform 흔들기
            if (stageTransform != null)
            {
                await ShakeRectTransformAsync(stageTransform, duration, strength, ct);
                return;
            }

            Debug.LogWarning("[ScreenFX] Camera Shake: 바인딩된 대상이 없습니다. stageCanvas 또는 stageCamera/stageTransform을 설정하세요.");
        }

        /// <summary>
        /// 카메라 흔들기 (DOTween)
        /// </summary>
        async UniTask ShakeCameraAsync(Camera cam, float duration, float strength, CancellationToken ct)
        {
            Vector3 originalPos = cam.transform.localPosition;
            
            // strength를 카메라 단위로 변환 (UI 픽셀 → 월드 단위)
            float worldStrength = strength * 0.01f;
            
            await cam.transform
                .DOShakePosition(duration, worldStrength, 20, 90, false, true)
                .ToUniTask(cancellationToken: ct);
            
            cam.transform.localPosition = originalPos;
        }

        /// <summary>
        /// RectTransform 흔들기 - FEEL 스타일 Perlin Noise 기반
        /// 부드럽고 자연스러운 흔들림 + 감쇠(Falloff) 적용
        /// </summary>
        async UniTask ShakeRectTransformAsync(RectTransform rt, float duration, float strength, CancellationToken ct)
        {
            Vector2 originalPos = rt.anchoredPosition;
            float elapsed = 0f;
            
            // 각 축에 다른 시드를 사용해서 자연스러운 2D 흔들림
            float seedX = noiseSeed;
            float seedY = noiseSeed + 100f;
            
            try
            {
                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();
                    
                    float t = elapsed / duration;
                    // 감쇠 커브: 시작은 강하고 끝으로 갈수록 약해짐
                    float falloff = 1f - t;
                    falloff = falloff * falloff; // 이차 감쇠 (더 자연스러움)
                    
                    // Perlin Noise로 부드러운 랜덤 오프셋 생성 (-1 ~ 1 범위)
                    float noiseX = (Mathf.PerlinNoise(shakeFrequency * elapsed, seedX) * 2f - 1f);
                    float noiseY = (Mathf.PerlinNoise(shakeFrequency * elapsed, seedY) * 2f - 1f);
                    
                    // 강도와 감쇠 적용
                    Vector2 offset = new Vector2(noiseX, noiseY) * strength * falloff;
                    rt.anchoredPosition = originalPos + offset;
                    
                    elapsed += Time.deltaTime;
                    await UniTask.Yield(ct);
                }
            }
            finally
            {
                // 취소되더라도 반드시 원래 위치로 복원
                if (rt != null)
                    rt.anchoredPosition = originalPos;
            }
        }

        #endregion

        #region Eye Open/Close (눈 뜨기/감기 효과)

        // Eye 바 캐싱
        RectTransform rtEyeTop;
        RectTransform rtEyeBottom;
        float eyeHalfHeight;
        bool eyeInitialized;
        Sequence eyeSequence;

        /// <summary>
        /// Eye 바 초기화 (최초 1회)
        /// </summary>
        void EnsureEyeSetup()
        {
            if (eyeInitialized) return;
            if (eyeTop == null || eyeBottom == null) return;

            rtEyeTop = eyeTop.rectTransform;
            rtEyeBottom = eyeBottom.rectTransform;

            var parentRect = (RectTransform)rtEyeTop.parent;
            float screenHeight = parentRect.rect.height;
            float screenWidth = parentRect.rect.width;
            eyeHalfHeight = screenHeight / 2f;

            // eyeTop: 상단 앵커, pivot 하단
            rtEyeTop.anchorMin = new Vector2(0.5f, 1f);
            rtEyeTop.anchorMax = new Vector2(0.5f, 1f);
            rtEyeTop.pivot = new Vector2(0.5f, 0f);
            rtEyeTop.sizeDelta = new Vector2(screenWidth + 100f, eyeHalfHeight + 50f);

            // eyeBottom: 하단 앵커, pivot 상단
            rtEyeBottom.anchorMin = new Vector2(0.5f, 0f);
            rtEyeBottom.anchorMax = new Vector2(0.5f, 0f);
            rtEyeBottom.pivot = new Vector2(0.5f, 1f);
            rtEyeBottom.sizeDelta = new Vector2(screenWidth + 100f, eyeHalfHeight + 50f);

            eyeInitialized = true;
        }

        /// <summary>
        /// 진행 중인 Eye 트윈 정리
        /// </summary>
        void KillEyeSequence()
        {
            if (eyeSequence != null && eyeSequence.IsActive())
            {
                eyeSequence.Kill();
            }
            eyeSequence = null;
        }

        /// <summary>
        /// 눈 뜨는 효과 — 2단계: 살짝 틈 → 확 열림
        /// EyeOpen[:totalDuration]
        /// Phase 1 (30%): 살짝 열림 — 눈부심으로 멈칫 (OutSine)
        /// Phase 2 (70%): 부드럽게 완전히 열림 (OutCubic)
        /// </summary>
        public async UniTask EyeOpenAsync(float duration = 1f, CancellationToken ct = default)
        {
            if (eyeTop == null || eyeBottom == null)
            {
                Debug.LogWarning("[ScreenFX] Eye 바인딩이 없습니다.");
                await FadeInAsync(duration, ct);
                return;
            }

            EnsureEyeSetup();
            KillEyeSequence();

            // 시작: 눈 감은 상태
            rtEyeTop.anchoredPosition = new Vector2(0, -eyeHalfHeight);
            rtEyeBottom.anchoredPosition = new Vector2(0, eyeHalfHeight);
            eyeTop.gameObject.SetActive(true);
            eyeBottom.gameObject.SetActive(true);

            float peekRatio = 0.15f;  // 살짝 열리는 양 (15%)
            float peekY = eyeHalfHeight * (1f - peekRatio);
            float phase1 = duration * 0.3f;
            float phase2 = duration * 0.7f;

            eyeSequence = DOTween.Sequence()
                // Phase 1: 살짝 열림 (눈부심 멈칫)
                .Append(rtEyeTop.DOAnchorPosY(-peekY, phase1).SetEase(Ease.OutSine))
                .Join(rtEyeBottom.DOAnchorPosY(peekY, phase1).SetEase(Ease.OutSine))
                // Phase 2: 완전히 열림
                .Append(rtEyeTop.DOAnchorPosY(0, phase2).SetEase(Ease.OutCubic))
                .Join(rtEyeBottom.DOAnchorPosY(0, phase2).SetEase(Ease.OutCubic));

            await eyeSequence.ToUniTask(cancellationToken: ct);

            eyeTop.gameObject.SetActive(false);
            eyeBottom.gameObject.SetActive(false);
            eyeSequence = null;
        }

        /// <summary>
        /// 눈 감는 효과 — 서서히 → 끝에서 가속 (눈꺼풀 무게감)
        /// EyeClose[:duration]
        /// </summary>
        public async UniTask EyeCloseAsync(float duration = 1f, CancellationToken ct = default)
        {
            if (eyeTop == null || eyeBottom == null)
            {
                Debug.LogWarning("[ScreenFX] Eye 바인딩이 없습니다.");
                await FadeOutAsync(duration, ct);
                return;
            }

            EnsureEyeSetup();
            KillEyeSequence();

            // 시작: 눈 뜬 상태
            rtEyeTop.anchoredPosition = new Vector2(0, 0);
            rtEyeBottom.anchoredPosition = new Vector2(0, 0);
            eyeTop.gameObject.SetActive(true);
            eyeBottom.gameObject.SetActive(true);

            eyeSequence = DOTween.Sequence()
                .Append(rtEyeTop.DOAnchorPosY(-eyeHalfHeight, duration).SetEase(Ease.InCubic))
                .Join(rtEyeBottom.DOAnchorPosY(eyeHalfHeight, duration).SetEase(Ease.InCubic));

            await eyeSequence.ToUniTask(cancellationToken: ct);
            eyeSequence = null;
        }

        /// <summary>
        /// 눈 깜빡임 — 닫기 → 잠깐 유지(hold) → 열기
        /// EyeBlink[:closeDuration:openDuration[:holdTime]]
        /// hold가 없으면 기본 0.05초 (자연스러운 멈춤)
        /// </summary>
        public async UniTask EyeBlinkAsync(float closeDuration = 0.1f, float openDuration = 0.15f,
            float holdTime = 0.05f, CancellationToken ct = default)
        {
            if (eyeTop == null || eyeBottom == null)
            {
                await FadeOutAsync(closeDuration, ct);
                await FadeInAsync(openDuration, ct);
                return;
            }

            EnsureEyeSetup();
            KillEyeSequence();

            // 시작: 눈 뜬 상태
            rtEyeTop.anchoredPosition = new Vector2(0, 0);
            rtEyeBottom.anchoredPosition = new Vector2(0, 0);
            eyeTop.gameObject.SetActive(true);
            eyeBottom.gameObject.SetActive(true);

            eyeSequence = DOTween.Sequence()
                // 닫기 (빠르게, 가속)
                .Append(rtEyeTop.DOAnchorPosY(-eyeHalfHeight, closeDuration).SetEase(Ease.InQuad))
                .Join(rtEyeBottom.DOAnchorPosY(eyeHalfHeight, closeDuration).SetEase(Ease.InQuad))
                // 닫힌 상태 유지
                .AppendInterval(holdTime)
                // 열기 (부드럽게, 감속)
                .Append(rtEyeTop.DOAnchorPosY(0, openDuration).SetEase(Ease.OutCubic))
                .Join(rtEyeBottom.DOAnchorPosY(0, openDuration).SetEase(Ease.OutCubic));

            await eyeSequence.ToUniTask(cancellationToken: ct);

            eyeTop.gameObject.SetActive(false);
            eyeBottom.gameObject.SetActive(false);
            eyeSequence = null;
        }

        /// <summary>
        /// 눈 즉시 닫힘 (애니메이션 없이)
        /// </summary>
        public void EyeCloseImmediate()
        {
            if (eyeTop == null || eyeBottom == null) return;

            EnsureEyeSetup();
            KillEyeSequence();

            rtEyeTop.anchoredPosition = new Vector2(0, -eyeHalfHeight);
            rtEyeBottom.anchoredPosition = new Vector2(0, eyeHalfHeight);

            eyeTop.gameObject.SetActive(true);
            eyeBottom.gameObject.SetActive(true);
        }

        /// <summary>
        /// 눈 즉시 열림 (애니메이션 없이, Eye 바 비활성화)
        /// </summary>
        public void EyeOpenImmediate()
        {
            KillEyeSequence();
            if (eyeTop != null) eyeTop.gameObject.SetActive(false);
            if (eyeBottom != null) eyeBottom.gameObject.SetActive(false);
        }

        #endregion

        void SetOverlayAlpha(Image img, float alpha)
        {
            if (img == null) return;
            var color = img.color;
            color.a = alpha;
            img.color = color;
        }
    }
}
