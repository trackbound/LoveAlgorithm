using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using LoveAlgo.Stage;
using LoveAlgo.Story;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace LoveAlgo.Core
{
    /// <summary>
    /// 화면 효과 (FadeIn/Out, Flash 등)
    /// 별도 캔버스(Canvas_ScreenFX)에 배치, 전역 싱글톤
    /// </summary>
    public class ScreenFX : SingletonMonoBehaviour<ScreenFX>
    {

        [Header("바인딩")]
        [SerializeField] Image fadeOverlay;         // 검은색 오버레이
        [SerializeField] Image flashOverlay;        // 흰색 플래시용 (선택)
        
        // Eye Open/Close 용 검은 바 — Stage 하위의 EyeMask에서 런타임 resolve.
        // (국렦 감은 상태에서도 대화창이 보이도록 Stage 캔버스에 배치, 이 화면 전역 FX와는 분리)
        Image eyeTop;
        Image eyeBottom;
        
        [Header("Camera Shake")]
        [Tooltip("Stage Canvas (Screen Space - Camera/Overlay). 바인딩 시 Camera/Transform 자동 추출")]
        [SerializeField] Canvas stageCanvas;
        [Tooltip("Camera Canvas 사용 시 Main Camera 바인딩")]
        [SerializeField] Camera stageCamera;        // Screen Space - Camera 모드용
        [Tooltip("Overlay Canvas 사용 시 Stage RectTransform 바인딩 (폴백)")]
        [SerializeField] RectTransform stageTransform; // 폴백용

        // dialogueUITransform은 DialogueUI가 lazy spawn/destroy 되므로 캐싱하지 않고 매번 조회 (싱글톤 1단계)
        RectTransform DialogueUITransform
        {
            get
            {
                var ui = LoveAlgo.UI.UIManager.Instance?.DialogueUI;
                return ui != null ? ui.transform as RectTransform : null;
            }
        }

        [Header("Color Tint")]
        [Tooltip("색상 오버레이용 Image (fadeOverlay 공용 또는 별도)")]
        [SerializeField] Image tintOverlay;

        [Header("설정")]
        [Tooltip("기본 페이드 지속 — 0.8~1.0s가 시네마틱 VN 표준")]
        [SerializeField] float defaultFadeDuration = 0.9f;
        [Tooltip("기본 플래시 지속 — 0.12~0.18s가 자연스러운 섬광")]
        [SerializeField] float defaultFlashDuration = 0.14f;
        [Tooltip("FadeOut 시 끝에서 살짝 머무는 시간(s) — 검은 화면 안착감")]
        [SerializeField] float fadeOutHoldTail = 0.05f;

        [Header("Shake 설정")]
        [Tooltip("흔들림 주파수 (높을수록 빠르게 진동)")]
        [SerializeField] float shakeFrequency = 25f;
        [Tooltip("Perlin Noise 시드 오프셋 (다양한 흔들림 패턴)")]
        [SerializeField] float noiseSeed = 0f;
        [Tooltip("초반 가속 구간 비율 (0~1)")]
        [Range(0f, 1f)]
        [SerializeField] float shakeAttackRatio = 0.12f;
        [Tooltip("감쇠 곡선 지수 (클수록 끝에서 빠르게 잦아듦)")]
        [SerializeField] float shakeDecayPower = 2.2f;
        [Tooltip("카메라 흔들림 위치 변환 스케일 (UI 픽셀 -> 월드)")]
        [SerializeField] float cameraPositionScale = 0.01f;

        [Header("Shake 프리셋")]
        [SerializeField] float shakePresetWeak = 10f;
        [SerializeField] float shakePresetMedium = 25f;
        [SerializeField] float shakePresetStrong = 50f;

        [Header("Stage Shake 튜닝")]
        [SerializeField] float stageShakeXMultiplier = 1.0f;
        [SerializeField] float stageShakeYMultiplier = 0.35f;
        [SerializeField] float stageShakeRotationMultiplier = 0.06f;
        [SerializeField] float stageShakeFrequencyMultiplier = 1.0f;
        [Tooltip("스테이지 임팩트 흔들림의 기본 진동수(Hz)")]
        [SerializeField] float stageImpactFrequencyHz = 5.0f;
        [Tooltip("스테이지 임팩트 흔들림의 감쇠 계수")]
        [SerializeField] float stageImpactDamping = 5.2f;

        [Header("Dialogue Shake 튜닝")]
        [SerializeField] float dialogueShakeXMultiplier = 1.0f;
        [SerializeField] float dialogueShakeYMultiplier = 0.12f;
        [SerializeField] float dialogueShakeRotationMultiplier = 0.02f;
        [SerializeField] float dialogueShakeFrequencyMultiplier = 1.0f;
        [Tooltip("대사창 임팩트 흔들림의 기본 진동수(Hz). 높을수록 진동 느낌이 강해짐")]
        [SerializeField] float dialogueImpactFrequencyHz = 6.0f;
        [Tooltip("대사창 임팩트 흔들림의 감쇠 계수. 높을수록 빨리 잦아듦")]
        [SerializeField] float dialogueImpactDamping = 6.5f;

        [Header("Impact 공통 튜닝")]
        [Tooltip("충격 직후 초기 변위를 유지하는 시간(s) — Hitlag / 프리즈 프레임 효과")]
        [SerializeField] float shakeHitlagSeconds = 0.025f;
        [Tooltip("이 강도 이상일 때 임팩트 플래시 추가 (DialogueShake / 어두운 CamShake)")]
        [SerializeField] float impactFlashStrengthThreshold = 20f;
        [Tooltip("임팩트 플래시 최대 알파")]
        [Range(0f, 1f)]
        [SerializeField] float impactFlashAlpha = 0.30f;
        [Tooltip("임팩트 플래시 지속 시간(s)")]
        [SerializeField] float impactFlashDuration = 0.06f;

        /// <summary>페이드 오버레이가 검은 상태인지 (세이브용)</summary>
        public bool IsFadeBlack => fadeOverlay != null && fadeOverlay.color.a >= 0.95f;

        /// <summary>눈 감기 효과가 활성 상태인지 (세이브용)</summary>
        public bool IsEyeClosed => StageModule.Instance?.EyeMask?.IsClosed ?? false;

        protected override void OnSingletonAwake()
        {
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

            // Eye 효과는 Stage(EyeMask)에서 관리. ScreenFX는 명령 위임만.

            EnsureBindings();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (fadeOverlay != null) DOTween.Kill(fadeOverlay);
            if (flashOverlay != null) DOTween.Kill(flashOverlay);
            if (tintOverlay != null) DOTween.Kill(tintOverlay);
            if (stageTransform != null) DOTween.Kill(stageTransform);
        }

        /// <summary>
        /// 외부 바인딩(stageCanvas/stageCamera/stageTransform) 런타임 자동 resolve.
        /// 프리합이라 인스펙터로 못 묶으므로 StageManager를 통해 보강.
        /// dialogueUITransform은 별도로 매번 즉시 조회 (DialogueUITransform 프로퍼티).
        /// </summary>
        void EnsureBindings()
        {
            if (stageCanvas == null)
            {
                stageCanvas = StageModule.Instance?.StageCanvas;
            }
            if (stageCanvas == null) return;

            if (stageTransform == null)
            {
                stageTransform = stageCanvas.transform as RectTransform;
            }

            if (stageCamera == null && stageCanvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                stageCamera = stageCanvas.worldCamera;
            }

            // 카메라 셰이크 시 배경 가장자리 노출 방지 — 기본 파란색 대신 검정
            if (stageCamera != null && stageCamera.backgroundColor != Color.black)
            {
                stageCamera.backgroundColor = Color.black;
            }
        }

        /// <summary>
        /// FX 명령 실행
        /// Value 형식: 효과명[:인자1:인자2:...]
        /// </summary>
        public async UniTask ExecuteAsync(string value, CancellationToken ct = default)
        {
            var parts = value.Split(':');
            // FXLineExecutor가 진입 시 alias/case 정규화하므로 ScreenFX는 PascalCase canonical 토큰만 받음.
            string effect = parts[0];

            // SO 기본값 (없으면 코드 기본값 폴백)
            var cfg = FXDefaultsConfig.Instance;
            float dFade   = cfg != null ? cfg.fadeDuration       : defaultFadeDuration;
            float dFlash  = cfg != null ? cfg.flashDuration      : defaultFlashDuration;
            float dZoom   = cfg != null ? cfg.camZoomDuration    : 0.5f;
            float dPan    = cfg != null ? cfg.camPanDuration     : 0.5f;
            float dReset  = cfg != null ? cfg.camResetDuration   : 0.4f;
            float dTintA  = cfg != null ? cfg.tintAlpha          : 0.25f;
            float dTintD  = cfg != null ? cfg.tintDuration       : 0.5f;
            float dEyeO   = cfg != null ? cfg.eyeOpenDuration    : 0.8f;
            float dEyeC   = cfg != null ? cfg.eyeCloseDuration   : 0.8f;
            float dBlinkC = cfg != null ? cfg.eyeBlinkClose      : 0.1f;
            float dBlinkO = cfg != null ? cfg.eyeBlinkOpen       : 0.15f;
            float dBlinkH = cfg != null ? cfg.eyeBlinkHold       : 0.05f;

            // 2. 기본 내장 효과 (DOTween 기반)
            switch (effect)
            {
                case "FadeOut":
                    float fadeOutDuration = parts.Length > 1 && float.TryParse(parts[1], out float fo) ? fo : dFade;
                    await FadeOutAsync(fadeOutDuration, ct);
                    break;

                case "FadeIn":
                    float fadeInDuration = parts.Length > 1 && float.TryParse(parts[1], out float fi) ? fi : dFade;
                    await FadeInAsync(fadeInDuration, ct);
                    break;

                case "Flash":
                    float flashDuration = parts.Length > 1 && float.TryParse(parts[1], out float fl) ? fl : dFlash;
                    await FlashAsync(flashDuration, ct);
                    break;

                case "CamShake":
                    ParseShakeArgs(parts, out float camShakeDuration, out float camShakeStrength);
                    await CamShakeAsync(camShakeDuration, camShakeStrength, ct);
                    break;

                case "StageShake":
                    ParseShakeArgs(parts, out float stageShakeDuration, out float stageShakeStrength);
                    await StageShakeAsync(stageShakeDuration, stageShakeStrength, ct);
                    break;

                case "DialogueShake":
                    ParseShakeArgs(parts, out float dialogueShakeDuration, out float dialogueShakeStrength);
                    await DialogueShakeAsync(dialogueShakeDuration, dialogueShakeStrength, ct);
                    break;

                case "CamZoom":
                    // CSV: CamZoom[:zoomLevel[:duration]] (zoomLevel 1.0=기본, 1.5=확대)
                    float zoomLevel = parts.Length > 1 && float.TryParse(parts[1], out float zl) ? zl : 1f;
                    float zoomDuration = parts.Length > 2 && float.TryParse(parts[2], out float zd) ? zd : dZoom;
                    await CamZoomAsync(zoomLevel, zoomDuration, ct);
                    break;

                case "CamPan":
                    // CSV: CamPan:x:y[:duration]  (x,y = 픽셀 오프셋, 0:0=원점 복귀)
                    float panX = parts.Length > 1 && float.TryParse(parts[1], out float px) ? px : 0f;
                    float panY = parts.Length > 2 && float.TryParse(parts[2], out float py) ? py : 0f;
                    float panDuration = parts.Length > 3 && float.TryParse(parts[3], out float pd) ? pd : dPan;
                    await CamPanAsync(panX, panY, panDuration, ct);
                    break;

                case "CamReset":
                    // CSV: CamReset[:duration]  줌+팬 동시 원점 복귀
                    float resetDur = parts.Length > 1 && float.TryParse(parts[1], out float rd) ? rd : dReset;
                    await CamResetAsync(resetDur, ct);
                    break;

                case "ColorTint":
                    // CSV: ColorTint:색상프리셋[:alpha[:duration]]  (Clear=해제)
                    string tintName = parts.Length > 1 ? parts[1] : "Clear";
                    float tintAlpha = parts.Length > 2 && float.TryParse(parts[2], out float ta) ? ta : dTintA;
                    float tintDur = parts.Length > 3 && float.TryParse(parts[3], out float td) ? td : dTintD;
                    await ColorTintAsync(tintName, tintAlpha, tintDur, ct);
                    break;

                case "EyeOpen":
                    // 눈 뜨는 효과: EyeOpen[:duration]
                    float eyeOpenDuration = parts.Length > 1 && float.TryParse(parts[1], out float eod) ? eod : dEyeO;
                    await EyeOpenAsync(eyeOpenDuration, ct);
                    break;

                case "EyeClose":
                    // 눈 감는 효과: EyeClose[:duration]
                    float eyeCloseDuration = parts.Length > 1 && float.TryParse(parts[1], out float ecd) ? ecd : dEyeC;
                    await EyeCloseAsync(eyeCloseDuration, ct);
                    break;

                case "EyeCloseImmediate":
                    // 눈 즉시 닫기 (애니메이션 없이)
                    EyeCloseImmediate();
                    break;

                case "EyeBlink":
                    // 눈 깜빡임: EyeBlink[:closeDuration:openDuration[:holdTime]]
                    float blinkClose = parts.Length > 1 && float.TryParse(parts[1], out float bc) ? bc : dBlinkC;
                    float blinkOpen = parts.Length > 2 && float.TryParse(parts[2], out float bo) ? bo : dBlinkO;
                    float blinkHold = parts.Length > 3 && float.TryParse(parts[3], out float bh) ? bh : dBlinkH;
                    await EyeBlinkAsync(blinkClose, blinkOpen, blinkHold, ct);
                    break;

                case "CharShake":
                case "CharJump":
                case "CharDim":
                    // 캐릭터 효과는 CharacterLayer를 통해 처리
                    var charLayer = StageModule.Instance?.Character;
                    if (charLayer != null)
                    {
                        await charLayer.ExecuteCharFXAsync(effect, parts, ct);
                    }
                    else
                    {
                        Debug.LogWarning($"[ScreenFX] CharacterLayer를 찾을 수 없음: {value}");
                    }
                    break;

                default:
                    Debug.LogWarning($"[ScreenFX] 알 수 없는 효과: {effect}");
                    break;
            }
        }

        #region Fade

        /// <summary>
        /// 화면 어둡게 (검은색으로 페이드).
        /// 부드러운 InOutCubic + 끝에서 살짝 머묾(hold tail) — VN 시네마틱 표준.
        /// </summary>
        public async UniTask FadeOutAsync(float duration, CancellationToken ct = default)
        {
            if (fadeOverlay == null)
            {
                Debug.LogWarning("[ScreenFX] fadeOverlay가 없습니다.");
                return;
            }

            DOTween.Kill(fadeOverlay);
            fadeOverlay.raycastTarget = true;

            // 색상이 흰색(Flash 직후 등)이면 검정으로 복원
            var c = fadeOverlay.color;
            if (c.r > 0.05f || c.g > 0.05f || c.b > 0.05f)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, c.a);
            }

            await fadeOverlay.DOFade(1f, duration)
                .SetEase(Ease.InOutCubic)
                .ToUniTask(cancellationToken: ct);

            // 검은 화면 안착감 — 다음 명령으로 넘어가기 전 살짝 머묾
            var cfgFade = FXDefaultsConfig.Instance;
            float hold = cfgFade != null ? cfgFade.fadeOutHoldTail : fadeOutHoldTail;
            if (hold > 0f)
                await UniTask.Delay(TimeSpan.FromSeconds(hold), cancellationToken: ct);
        }

        /// <summary>
        /// 화면 밝게 (페이드 복귀). OutQuart로 처음엔 빠르게 밝아지다 끝에서 부드럽게 안착.
        /// </summary>
        public async UniTask FadeInAsync(float duration, CancellationToken ct = default)
        {
            if (fadeOverlay == null)
            {
                Debug.LogWarning("[ScreenFX] fadeOverlay가 없습니다.");
                return;
            }

            DOTween.Kill(fadeOverlay);
            await fadeOverlay.DOFade(0f, duration)
                .SetEase(Ease.OutQuart)
                .ToUniTask(cancellationToken: ct);
            fadeOverlay.raycastTarget = false;
        }

        /// <summary>
        /// FadeOut → LoadingScreen 표시 → FadeIn 을 한 호출로. 호출자는 이후 작업(phase 전환·
        /// 데이터 갱신 등)을 수행하고, 마무리는 <see cref="ExitLoadingAsync"/>로.
        /// </summary>
        public async UniTask EnterLoadingAsync(float fadeOutDuration, float fadeInDuration, CancellationToken ct = default)
        {
            await FadeOutAsync(fadeOutDuration, ct);
            var loading = LoadingScreen.Instance;
            if (loading != null) await loading.ShowAsync(ct);
            if (fadeInDuration > 0f) await FadeInAsync(fadeInDuration, ct);
        }

        /// <summary>
        /// FadeOut → LoadingScreen 즉시 제거 → FadeIn 을 한 호출로. <see cref="EnterLoadingAsync"/>의 짝.
        /// fadeInDuration이 0이면 마지막 페이드 인 생략 (호출자가 별도 등장 연출을 가질 때).
        /// </summary>
        public async UniTask ExitLoadingAsync(float fadeOutDuration, float fadeInDuration, CancellationToken ct = default)
        {
            await FadeOutAsync(fadeOutDuration, ct);
            LoadingScreen.Instance?.HideImmediate();
            if (fadeInDuration > 0f) await FadeInAsync(fadeInDuration, ct);
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

            // 틴트 오버레이 초기화
            if (tintOverlay != null)
            {
                DOTween.Kill(tintOverlay);
                SetOverlayAlpha(tintOverlay, 0f);
                tintOverlay.raycastTarget = false;
            }

            // 줌/팬 원점 복귀
            if (stageTransform != null)
            {
                stageTransform.DOKill();
                stageTransform.localScale = Vector3.one;
                stageTransform.anchoredPosition = Vector2.zero;
            }

            // Eye 바도 비활성화 (DayEnd 후 잔존 방지)
            EyeOpenImmediate();
        }

        #endregion

        #region Flash

        /// <summary>
        /// 화면 번쩍임 — 빠른 ramp-up + Expo 감쇠로 자연스러운 섬광.
        /// </summary>
        public async UniTask FlashAsync(float duration, CancellationToken ct = default)
        {
            Image overlay = flashOverlay != null ? flashOverlay : fadeOverlay;

            if (overlay == null)
            {
                Debug.LogWarning("[ScreenFX] 오버레이가 없습니다.");
                return;
            }

            DOTween.Kill(overlay);

            // 흰색으로 설정 (flashOverlay가 없으면 fadeOverlay 사용)
            Color originalColor = overlay.color;
            if (flashOverlay == null)
            {
                overlay.color = Color.white;
            }

            // Ramp-up: 약 20%는 빠르게 밝아짐 → 나머지 80%는 OutExpo로 자연 감쇠
            float rampIn = Mathf.Min(0.05f, duration * 0.2f);
            float rampOut = Mathf.Max(0.01f, duration - rampIn);

            SetOverlayAlpha(overlay, 0f);
            overlay.raycastTarget = false;

            try
            {
                await overlay.DOFade(1f, rampIn)
                    .SetEase(Ease.OutQuad)
                    .ToUniTask(cancellationToken: ct);
                await overlay.DOFade(0f, rampOut)
                    .SetEase(Ease.OutExpo)
                    .ToUniTask(cancellationToken: ct);
            }
            finally
            {
                // 원래 색상 복원 (fadeOverlay 공용일 때 검정으로 복귀)
                if (overlay != null && flashOverlay == null)
                {
                    overlay.color = originalColor;
                    SetOverlayAlpha(overlay, 0f);
                }
            }
        }

        #endregion

        #region Shake

        struct ShakeProfile
        {
            public float XMultiplier;
            public float YMultiplier;
            public float RotationMultiplier;
            public float FrequencyMultiplier;
        }

        /// <summary>
        /// 흔들림 인자 파싱
        /// CSV 형식:
        ///   Effect                       → duration=0.3, strength=Medium
        ///   Effect:Weak/Medium/Strong     → duration=0.3, strength=프리셋
        ///   Effect:duration               → duration=값,  strength=Medium
        ///   Effect:duration:Weak/Medium/Strong → duration=값, strength=프리셋
        ///   Effect:duration:숫자값         → duration=값,  strength=숫자값
        /// </summary>
        void ParseShakeArgs(string[] parts, out float duration, out float strength)
        {
            var cfg = FXDefaultsConfig.Instance;
            duration = cfg != null ? cfg.shakeDuration : 0.3f;
            strength = cfg != null ? cfg.shakeMedium   : shakePresetMedium;

            if (parts.Length <= 1) return;

            string token1 = parts[1].Trim();

            // parts[1]이 프리셋 이름인 경우: Effect:Strong (2-part)
            if (TryParseShakePreset(token1, out strength))
            {
                // duration은 기본값 유지
                return;
            }

            // parts[1]이 숫자인 경우: duration
            if (float.TryParse(token1, out float parsedDuration))
                duration = parsedDuration;

            // parts[2]가 있으면 strength (프리셋 또는 숫자)
            if (parts.Length > 2)
            {
                string token2 = parts[2].Trim();
                if (TryParseShakePreset(token2, out strength))
                    return;
                if (float.TryParse(token2, out float parsedStrength))
                    strength = parsedStrength;
            }
        }

        /// <summary>
        /// 프리셋 이름 → 강도 값 변환
        /// </summary>
        bool TryParseShakePreset(string token, out float strength)
        {
            var cfg = FXDefaultsConfig.Instance;
            float weak   = cfg != null ? cfg.shakeWeak   : shakePresetWeak;
            float medium = cfg != null ? cfg.shakeMedium : shakePresetMedium;
            float strong = cfg != null ? cfg.shakeStrong : shakePresetStrong;

            if (token.Equals("weak",   StringComparison.OrdinalIgnoreCase)) { strength = weak;   return true; }
            if (token.Equals("medium", StringComparison.OrdinalIgnoreCase)) { strength = medium; return true; }
            if (token.Equals("strong", StringComparison.OrdinalIgnoreCase)) { strength = strong; return true; }

            strength = medium;
            return false;
        }

        /// <summary>
        /// 하위 호환 흔들림
        /// 기존 CamShake는 화면이 어두울 때 Dialogue 흔들림으로 자동 폴백
        /// </summary>
        public async UniTask CamShakeAsync(float duration, float strength, CancellationToken ct = default)
        {
            EnsureBindings();
            if ((IsEyeClosed || IsFadeBlack) && DialogueUITransform != null)
            {
                await DialogueShakeAsync(duration, strength, ct);
                return;
            }

            await StageShakeAsync(duration, strength, ct);
        }

        /// <summary>
        /// 스테이지 흔들림 (배경/캐릭터 레이어)
        /// </summary>
        public async UniTask StageShakeAsync(float duration, float strength, CancellationToken ct = default)
        {
            EnsureBindings();
            var profile = new ShakeProfile
            {
                XMultiplier = stageShakeXMultiplier,
                YMultiplier = stageShakeYMultiplier,
                RotationMultiplier = stageShakeRotationMultiplier,
                FrequencyMultiplier = stageShakeFrequencyMultiplier
            };

            if (stageCanvas != null && stageCanvas.renderMode == RenderMode.ScreenSpaceCamera && stageTransform != null)
            {
                await ShakeRectTransformImpactAsync(stageTransform, duration, strength, profile, stageImpactFrequencyHz, stageImpactDamping, ct);
                return;
            }

            if (stageCamera != null)
            {
                await ShakeCameraImpactAsync(stageCamera, duration, strength, profile, stageImpactFrequencyHz, stageImpactDamping, ct);
                return;
            }

            if (stageTransform != null)
            {
                await ShakeRectTransformImpactAsync(stageTransform, duration, strength, profile, stageImpactFrequencyHz, stageImpactDamping, ct);
                return;
            }

            var mainCam = Camera.main;
            if (mainCam != null)
            {
                Debug.Log("[ScreenFX] StageShake: 바인딩 없음 → Main Camera 폴백 사용");
                await ShakeCameraImpactAsync(mainCam, duration, strength, profile, stageImpactFrequencyHz, stageImpactDamping, ct);
                return;
            }

            Debug.LogWarning("[ScreenFX] StageShake: 대상이 없습니다. stageCanvas/stageCamera/stageTransform을 설정하세요.");
        }

        /// <summary>
        /// 대사창 흔들림 (UI)
        /// </summary>
        public async UniTask DialogueShakeAsync(float duration, float strength, CancellationToken ct = default)
        {
            var target = DialogueUITransform;
            if (target == null)
            {
                Debug.LogWarning("[ScreenFX] DialogueShake: DialogueUI가 아직 존재하지 않음");
                return;
            }

            var profile = new ShakeProfile
            {
                XMultiplier = dialogueShakeXMultiplier,
                YMultiplier = dialogueShakeYMultiplier,
                RotationMultiplier = dialogueShakeRotationMultiplier,
                FrequencyMultiplier = dialogueShakeFrequencyMultiplier
            };

            // 강한 충격 시 플래시 동시 재생 (shake와 병행, 완료 대기 안 함)
            if (strength >= impactFlashStrengthThreshold)
                ImpactFlashAsync(ct).Forget();

            await ShakeRectTransformImpactAsync(
                target, duration, strength, profile,
                dialogueImpactFrequencyHz, dialogueImpactDamping, ct);
        }

        /// <summary>
        /// 임팩트 플래시 — flashOverlay 또는 fadeOverlay로 순간 백색 섬광
        /// </summary>
        async UniTaskVoid ImpactFlashAsync(CancellationToken ct)
        {
            Image overlay = flashOverlay != null ? flashOverlay : fadeOverlay;
            if (overlay == null) return;

            Color saved = overlay.color;
            // 흰색으로 덮고 빠르게 페이드아웃
            overlay.color = Color.white;
            overlay.raycastTarget = false;
            SetOverlayAlpha(overlay, impactFlashAlpha);
            try
            {
                await overlay.DOFade(0f, impactFlashDuration)
                    .SetEase(Ease.OutExpo)
                    .ToUniTask(cancellationToken: ct);
            }
            finally
            {
                if (overlay != null)
                    overlay.color = saved;
            }
        }

        /// <summary>
        /// 카메라 흔들기 - 엔벨로프 + Perlin 기반
        /// </summary>
        async UniTask ShakeCameraAsync(Camera cam, float duration, float strength, ShakeProfile profile, CancellationToken ct)
        {
            if (cam == null) return;

            Vector3 originalPos = cam.transform.localPosition;
            Quaternion originalRot = cam.transform.localRotation;
            float elapsed = 0f;

            float seedBase = noiseSeed + Time.unscaledTime * 11.7f;
            float seedX = seedBase + 13.1f;
            float seedY = seedBase + 57.3f;

            try
            {
                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();

                    float amp = EvaluateShakeEnvelope(elapsed, duration);
                    float sampleT = elapsed * shakeFrequency * profile.FrequencyMultiplier;
                    float noiseX = SampleShakeNoise(sampleT, seedX);
                    float noiseY = SampleShakeNoise(sampleT, seedY);

                    float worldStrength = strength * cameraPositionScale * amp;
                    Vector3 offset = new Vector3(
                        noiseX * worldStrength * profile.XMultiplier,
                        noiseY * worldStrength * profile.YMultiplier,
                        0f);

                    float zRot = noiseX * strength * profile.RotationMultiplier * amp;

                    cam.transform.localPosition = originalPos + offset;
                    cam.transform.localRotation = Quaternion.Euler(0f, 0f, zRot) * originalRot;

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(ct);
                }
            }
            finally
            {
                if (cam != null)
                {
                    cam.transform.localPosition = originalPos;
                    cam.transform.localRotation = originalRot;
                }
            }
        }

        /// <summary>
        /// RectTransform 흔들기 - 엔벨로프 + Perlin 기반
        /// </summary>
        async UniTask ShakeRectTransformAsync(RectTransform rt, float duration, float strength, ShakeProfile profile, CancellationToken ct)
        {
            if (rt == null) return;

            Vector2 originalPos = rt.anchoredPosition;
            Quaternion originalRot = rt.localRotation;
            float elapsed = 0f;

            float seedBase = noiseSeed + Time.unscaledTime * 13.3f;
            float seedX = seedBase + 31.7f;
            float seedY = seedBase + 89.9f;

            try
            {
                while (elapsed < duration)
                {
                    ct.ThrowIfCancellationRequested();

                    float amp = EvaluateShakeEnvelope(elapsed, duration);
                    float sampleT = elapsed * shakeFrequency * profile.FrequencyMultiplier;
                    float noiseX = SampleShakeNoise(sampleT, seedX);
                    float noiseY = SampleShakeNoise(sampleT, seedY);

                    Vector2 offset = new Vector2(
                        noiseX * strength * profile.XMultiplier,
                        noiseY * strength * profile.YMultiplier) * amp;

                    float zRot = noiseX * strength * profile.RotationMultiplier * amp;

                    rt.anchoredPosition = originalPos + offset;
                    rt.localRotation = Quaternion.Euler(0f, 0f, zRot) * originalRot;

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(ct);
                }
            }
            finally
            {
                // 취소되더라도 반드시 원래 위치로 복원
                if (rt != null)
                {
                    rt.anchoredPosition = originalPos;
                    rt.localRotation = originalRot;
                }
            }
        }

        /// <summary>
        /// RectTransform 임팩트 흔들기 - VN 스타일(쾅 맞고 감쇠)
        /// 대사창에 주로 사용.
        /// </summary>
        async UniTask ShakeRectTransformImpactAsync(
            RectTransform rt, float duration, float strength, ShakeProfile profile,
            float frequencyHz, float damping, CancellationToken ct)
        {
            if (rt == null) return;

            Vector2 originalPos = rt.anchoredPosition;
            Quaternion originalRot = rt.localRotation;
            float elapsed = 0f;

            float safeDuration = Mathf.Max(0.05f, duration);
            float omega = 2f * Mathf.PI * Mathf.Max(1f, frequencyHz);
            float safeDamping = Mathf.Max(0.1f, damping);

            // 2D 방향 벡터 — 좌/우만 아니라 완전한 랜덤 방향
            float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            float dirX = Mathf.Cos(angle);
            float dirY = Mathf.Sin(angle);

            // Hitlag: 전체 duration의 최대 10% 또는 절대 상한값
            float effectiveHitlag = Mathf.Min(shakeHitlagSeconds, safeDuration * 0.10f);

            try
            {
                while (elapsed < safeDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    float x, y, zRot;

                    if (elapsed < effectiveHitlag)
                    {
                        // Phase 1 — Hitlag: 충격 방향으로 변위 고정 (프리즈 프레임)
                        x = dirX * strength * profile.XMultiplier;
                        y = dirY * strength * profile.YMultiplier;
                        zRot = dirX * strength * profile.RotationMultiplier * 0.4f;
                    }
                    else
                    {
                        // Phase 2 — 감쇠 진동: hitlag 이후 경과 시간 기준
                        // 시간 기반 감쇠 → duration에 관계없이 일정한 체감 강도
                        float t2 = elapsed - effectiveHitlag;
                        float decay = Mathf.Exp(-safeDamping * t2);
                        float wave = Mathf.Sin(t2 * omega * profile.FrequencyMultiplier);

                        x = dirX * wave * strength * profile.XMultiplier * decay;
                        y = dirY * wave * strength * profile.YMultiplier * decay;
                        zRot = wave * strength * profile.RotationMultiplier * decay;
                    }

                    rt.anchoredPosition = originalPos + new Vector2(x, y);
                    rt.localRotation = Quaternion.Euler(0f, 0f, zRot) * originalRot;

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(ct);
                }
            }
            finally
            {
                if (rt != null)
                {
                    rt.anchoredPosition = originalPos;
                    rt.localRotation = originalRot;
                }
            }
        }

        /// <summary>
        /// Camera 임팩트 흔들기 - VN 스타일(쾅 맞고 감쇠)
        /// </summary>
        async UniTask ShakeCameraImpactAsync(
            Camera cam, float duration, float strength, ShakeProfile profile,
            float frequencyHz, float damping, CancellationToken ct)
        {
            if (cam == null) return;

            Vector3 originalPos = cam.transform.localPosition;
            Quaternion originalRot = cam.transform.localRotation;
            float elapsed = 0f;

            float safeDuration = Mathf.Max(0.05f, duration);
            float omega = 2f * Mathf.PI * Mathf.Max(1f, frequencyHz);
            float safeDamping = Mathf.Max(0.1f, damping);
            float worldStrength = strength * cameraPositionScale;

            float angle = UnityEngine.Random.Range(0f, 2f * Mathf.PI);
            float dirX = Mathf.Cos(angle);
            float dirY = Mathf.Sin(angle);

            float effectiveHitlag = Mathf.Min(shakeHitlagSeconds, safeDuration * 0.10f);

            try
            {
                while (elapsed < safeDuration)
                {
                    ct.ThrowIfCancellationRequested();

                    Vector3 offset;
                    float zRot;

                    if (elapsed < effectiveHitlag)
                    {
                        offset = new Vector3(
                            dirX * worldStrength * profile.XMultiplier,
                            dirY * worldStrength * profile.YMultiplier,
                            0f);
                        zRot = dirX * strength * profile.RotationMultiplier * 0.4f;
                    }
                    else
                    {
                        float t2 = elapsed - effectiveHitlag;
                        float decay = Mathf.Exp(-safeDamping * t2);
                        float wave = Mathf.Sin(t2 * omega * profile.FrequencyMultiplier);

                        offset = new Vector3(
                            dirX * wave * worldStrength * profile.XMultiplier * decay,
                            dirY * wave * worldStrength * profile.YMultiplier * decay,
                            0f);
                        zRot = wave * strength * profile.RotationMultiplier * decay;
                    }

                    cam.transform.localPosition = originalPos + offset;
                    cam.transform.localRotation = Quaternion.Euler(0f, 0f, zRot) * originalRot;

                    elapsed += Time.deltaTime;
                    await UniTask.Yield(ct);
                }
            }
            finally
            {
                if (cam != null)
                {
                    cam.transform.localPosition = originalPos;
                    cam.transform.localRotation = originalRot;
                }
            }
        }

        float EvaluateShakeEnvelope(float elapsed, float duration)
        {
            if (duration <= 0f) return 0f;

            float attackTime = Mathf.Max(duration * shakeAttackRatio, 0.0001f);
            if (elapsed < attackTime)
            {
                return Mathf.Clamp01(elapsed / attackTime);
            }

            float decayTime = Mathf.Max(duration - attackTime, 0.0001f);
            float decayT = Mathf.Clamp01((elapsed - attackTime) / decayTime);
            return Mathf.Pow(1f - decayT, shakeDecayPower);
        }

        float SampleShakeNoise(float t, float seed)
        {
            // 2옥타브 블렌딩으로 단조로운 패턴 완화
            float n1 = Mathf.PerlinNoise(t, seed) * 2f - 1f;
            float n2 = Mathf.PerlinNoise(t * 2.17f, seed + 37.2f) * 2f - 1f;
            return (n1 + (n2 * 0.5f)) / 1.5f;
        }

        #endregion

        #region Camera Zoom

        /// <summary>
        /// 카메라 줌 — Stage RectTransform의 스케일 조절
        /// CSV: FX,,CamZoom:1.3:0.5,await  (1.3배 확대, 0.5초)
        /// CSV: FX,,CamZoom:1.0:0.3,await  (원래 크기로 복귀)
        /// </summary>
        public async UniTask CamZoomAsync(float targetScale, float duration, CancellationToken ct = default)
        {
            if (stageTransform == null)
            {
                Debug.LogWarning("[ScreenFX] CamZoom: stageTransform이 바인딩되지 않음");
                return;
            }

            stageTransform.DOKill();
            await stageTransform
                .DOScale(Vector3.one * targetScale, duration)
                .SetEase(Ease.InOutCubic)
                .ToUniTask(cancellationToken: ct);
        }

        #endregion

        #region Camera Pan

        /// <summary>
        /// 카메라 팬 — Stage RectTransform 위치 이동
        /// CSV: FX,,CamPan:100:0:0.5,await   (오른쪽으로 100px, 0.5초)
        /// CSV: FX,,CamPan:0:0:0.3,await     (원점 복귀)
        /// </summary>
        public async UniTask CamPanAsync(float x, float y, float duration, CancellationToken ct = default)
        {
            if (stageTransform == null)
            {
                Debug.LogWarning("[ScreenFX] CamPan: stageTransform이 바인딩되지 않음");
                return;
            }

            stageTransform.DOKill();
            await stageTransform
                .DOAnchorPos(new Vector2(x, y), duration)
                .SetEase(Ease.InOutCubic)
                .ToUniTask(cancellationToken: ct);
        }

        /// <summary>
        /// 줌 + 팬 동시 원점 복귀 — 부드러운 OutCubic으로 자연스러운 안착
        /// CSV: FX,,CamReset:0.4,await
        /// </summary>
        public async UniTask CamResetAsync(float duration, CancellationToken ct = default)
        {
            if (stageTransform == null) return;

            stageTransform.DOKill();
            var scaleTween = stageTransform.DOScale(Vector3.one, duration).SetEase(Ease.OutCubic);
            var posTween = stageTransform.DOAnchorPos(Vector2.zero, duration).SetEase(Ease.OutCubic);

            var seq = DOTween.Sequence();
            _ = seq.Join(scaleTween);
            _ = seq.Join(posTween);
            await seq.ToUniTask(cancellationToken: ct);
        }

        #endregion

        #region Color Tint

        /// <summary>
        /// 색상 틴트 오버레이
        /// CSV: FX,,ColorTint:Sepia:0.3:0.5,await   (세피아 30%, 0.5초)
        /// CSV: FX,,ColorTint:Clear::0.3,await       (해제)
        /// 프리셋: Sepia, Blue, Red, Pink, Green, Clear
        /// </summary>
        public async UniTask ColorTintAsync(string preset, float alpha, float duration, CancellationToken ct = default)
        {
            // tintOverlay가 없으면 fadeOverlay 공용 (색상 변경)
            var overlay = tintOverlay != null ? tintOverlay : fadeOverlay;
            if (overlay == null)
            {
                Debug.LogWarning("[ScreenFX] ColorTint: 오버레이 Image가 없음");
                return;
            }

            Color targetColor = ParseTintColor(preset);

            DOTween.Kill(overlay);

            if (preset == "Clear" || alpha <= 0f)
            {
                // 해제: 현재 색상 유지하며 알파만 0으로 (OutCubic — 부드러운 안착)
                await overlay.DOFade(0f, duration)
                    .SetEase(Ease.OutCubic)
                    .ToUniTask(cancellationToken: ct);
                overlay.raycastTarget = false;
                return;
            }

            targetColor.a = alpha;
            overlay.raycastTarget = false; // 틴트는 입력 차단하지 않음
            await overlay.DOColor(targetColor, duration)
                .SetEase(Ease.InOutCubic)
                .ToUniTask(cancellationToken: ct);
        }

        /// <summary>프리셋 색상 파싱</summary>
        static Color ParseTintColor(string preset)
        {
            return preset switch
            {
                "Sepia" => new Color(0.44f, 0.26f, 0.08f),   // 따뜻한 갈색
                "Blue" => new Color(0.1f, 0.15f, 0.4f),       // 차가운 파랑 (꿈/회상)
                "Red" => new Color(0.5f, 0.05f, 0.05f),       // 충격/위기
                "Pink" => new Color(0.6f, 0.2f, 0.35f),       // 로맨틱/설렘
                "Green" => new Color(0.1f, 0.3f, 0.1f),       // 자연/평화
                "Sunset" => new Color(0.6f, 0.25f, 0.1f),     // 석양/노을
                _ => Color.clear,
            };
        }

        #endregion

        #region Eye Open/Close (눈 뜨기/감기 효과 — EyeMask로 위임)

        // 모든 Eye 트윈/상태는 Stage 하위 EyeMask가 소유. ScreenFX는 명령만 위임.
        // EyeMask가 없으면(Stage 미스폰 등) FadeIn/Out으로 폴백.

        public async UniTask EyeOpenAsync(float duration = 1f, CancellationToken ct = default)
        {
            var mask = StageModule.Instance?.EyeMask;
            if (mask == null)
            {
                Debug.LogWarning("[ScreenFX] EyeMask가 없어 FadeIn으로 폴백");
                await FadeInAsync(duration, ct);
                return;
            }
            await mask.OpenAsync(duration, ct);
        }

        public async UniTask EyeCloseAsync(float duration = 1f, CancellationToken ct = default)
        {
            var mask = StageModule.Instance?.EyeMask;
            if (mask == null)
            {
                Debug.LogWarning("[ScreenFX] EyeMask가 없어 FadeOut으로 폴백");
                await FadeOutAsync(duration, ct);
                return;
            }
            await mask.CloseAsync(duration, ct);
        }

        public async UniTask EyeBlinkAsync(float closeDuration = 0.1f, float openDuration = 0.15f,
            float holdTime = 0.05f, CancellationToken ct = default)
        {
            var mask = StageModule.Instance?.EyeMask;
            if (mask == null)
            {
                await FadeOutAsync(closeDuration, ct);
                await FadeInAsync(openDuration, ct);
                return;
            }
            await mask.BlinkAsync(closeDuration, openDuration, holdTime, ct);
        }

        public void EyeCloseImmediate() => StageModule.Instance?.EyeMask?.CloseImmediate();
        public void EyeOpenImmediate() => StageModule.Instance?.EyeMask?.OpenImmediate();

        /// <summary>
        /// 매크로 진입 시 안전망 — 잔존 eye/tint 상태를 즉시 초기화.
        /// fade alpha는 호출자가 의도적으로 유지/변경하므로 건드리지 않음.
        /// </summary>
        public void ResetAll()
        {
            EyeOpenImmediate();
            if (tintOverlay != null)
            {
                DOTween.Kill(tintOverlay);
                SetOverlayAlpha(tintOverlay, 0f);
                tintOverlay.raycastTarget = false;
            }
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
