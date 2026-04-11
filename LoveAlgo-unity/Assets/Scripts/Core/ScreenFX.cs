using System;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        [Tooltip("화면이 어두울 때(EyeClose/FadeOut) 대신 흔들 대화창 RectTransform")]
        [SerializeField] RectTransform dialogueUITransform;

        [Header("Color Tint")]
        [Tooltip("색상 오버레이용 Image (fadeOverlay 공용 또는 별도)")]
        [SerializeField] Image tintOverlay;

        [Header("설정")]
        [SerializeField] float defaultFadeDuration = 0.6f;
        [SerializeField] float defaultFlashDuration = 0.1f;

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
        public bool IsEyeClosed => eyeTop != null && eyeTop.gameObject.activeSelf;

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

            // Eye 바가 Inspector에서 미바인딩 시 자동 생성
            EnsureEyeBars();

            // dialogueUITransform 자동 바인딩 (별도 캔버스라 Inspector 연결 어려움)
            if (dialogueUITransform == null)
            {
                var dialogueUI = LoveAlgo.UI.UIManager.Instance?.DialogueUI;
                if (dialogueUI != null)
                    dialogueUITransform = dialogueUI.GetComponent<RectTransform>();
            }

            EnsureBindings();
        }

        void OnValidate()
        {
            EnsureBindings();
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            KillEyeSequence();

            if (fadeOverlay != null) DOTween.Kill(fadeOverlay);
            if (flashOverlay != null) DOTween.Kill(flashOverlay);
            if (tintOverlay != null) DOTween.Kill(tintOverlay);
            if (stageTransform != null) DOTween.Kill(stageTransform);
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
                    float zoomDuration = parts.Length > 2 && float.TryParse(parts[2], out float zd) ? zd : 0.5f;
                    await CamZoomAsync(zoomLevel, zoomDuration, ct);
                    break;

                case "CamPan":
                    // CSV: CamPan:x:y[:duration]  (x,y = 픽셀 오프셋, 0:0=원점 복귀)
                    float panX = parts.Length > 1 && float.TryParse(parts[1], out float px) ? px : 0f;
                    float panY = parts.Length > 2 && float.TryParse(parts[2], out float py) ? py : 0f;
                    float panDuration = parts.Length > 3 && float.TryParse(parts[3], out float pd) ? pd : 0.5f;
                    await CamPanAsync(panX, panY, panDuration, ct);
                    break;

                case "CamReset":
                    // CSV: CamReset[:duration]  줌+팬 동시 원점 복귀
                    float resetDur = parts.Length > 1 && float.TryParse(parts[1], out float rd) ? rd : 0.4f;
                    await CamResetAsync(resetDur, ct);
                    break;

                case "ColorTint":
                    // CSV: ColorTint:색상프리셋[:alpha[:duration]]  (Clear=해제)
                    string tintName = parts.Length > 1 ? parts[1] : "Clear";
                    float tintAlpha = parts.Length > 2 && float.TryParse(parts[2], out float ta) ? ta : 0.25f;
                    float tintDur = parts.Length > 3 && float.TryParse(parts[3], out float td) ? td : 0.5f;
                    await ColorTintAsync(tintName, tintAlpha, tintDur, ct);
                    break;

                case "EyeOpen":
                    // 눈 뜨는 효과: EyeOpen[:duration]
                    float eyeOpenDuration = parts.Length > 1 && float.TryParse(parts[1], out float eod) ? eod : 0.8f;
                    await EyeOpenAsync(eyeOpenDuration, ct);
                    break;

                case "EyeClose":
                    // 눈 감는 효과: EyeClose[:duration]
                    float eyeCloseDuration = parts.Length > 1 && float.TryParse(parts[1], out float ecd) ? ecd : 0.8f;
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
                    // 캐릭터 효과는 CharacterLayer를 통해 처리
                    var charLayer = StageManager.Instance?.Character;
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
            await fadeOverlay.DOFade(1f, duration).SetEase(Ease.InOutSine).ToUniTask(cancellationToken: ct);
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

            await fadeOverlay.DOFade(0f, duration).SetEase(Ease.OutSine).ToUniTask(cancellationToken: ct);
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
            await overlay.DOFade(0f, duration).SetEase(Ease.OutQuad).ToUniTask(cancellationToken: ct);

            // 원래 색상 복원
            if (flashOverlay == null)
            {
                overlay.color = originalColor;
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
            duration = 0.3f;
            strength = shakePresetMedium;

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
            if (token.Equals("weak", StringComparison.OrdinalIgnoreCase))
            {
                strength = shakePresetWeak;
                return true;
            }
            if (token.Equals("medium", StringComparison.OrdinalIgnoreCase))
            {
                strength = shakePresetMedium;
                return true;
            }
            if (token.Equals("strong", StringComparison.OrdinalIgnoreCase))
            {
                strength = shakePresetStrong;
                return true;
            }
            strength = shakePresetMedium;
            return false;
        }

        /// <summary>
        /// 하위 호환 흔들림
        /// 기존 CamShake는 화면이 어두울 때 Dialogue 흔들림으로 자동 폴백
        /// </summary>
        public async UniTask CamShakeAsync(float duration, float strength, CancellationToken ct = default)
        {
            if ((IsEyeClosed || IsFadeBlack) && dialogueUITransform != null)
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
            if (dialogueUITransform == null)
            {
                Debug.LogWarning("[ScreenFX] DialogueShake: dialogueUITransform이 바인딩되지 않음");
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
                dialogueUITransform, duration, strength, profile,
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
                    .SetEase(Ease.OutQuad)
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

            await stageTransform
                .DOScale(Vector3.one * targetScale, duration)
                .SetEase(Ease.InOutSine)
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

            await stageTransform
                .DOAnchorPos(new Vector2(x, y), duration)
                .SetEase(Ease.InOutSine)
                .ToUniTask(cancellationToken: ct);
        }

        /// <summary>
        /// 줌 + 팬 동시 원점 복귀
        /// CSV: FX,,CamReset:0.4,await
        /// </summary>
        public async UniTask CamResetAsync(float duration, CancellationToken ct = default)
        {
            if (stageTransform == null) return;

            var scaleTween = stageTransform.DOScale(Vector3.one, duration).SetEase(Ease.InOutSine);
            var posTween = stageTransform.DOAnchorPos(Vector2.zero, duration).SetEase(Ease.InOutSine);

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

            if (preset == "Clear" || alpha <= 0f)
            {
                // 해제: 현재 색상 유지하며 알파만 0으로
                await overlay.DOFade(0f, duration)
                    .SetEase(Ease.OutSine)
                    .ToUniTask(cancellationToken: ct);
                overlay.raycastTarget = false;
                return;
            }

            targetColor.a = alpha;
            overlay.raycastTarget = false; // 틴트는 입력 차단하지 않음
            await overlay.DOColor(targetColor, duration)
                .SetEase(Ease.InOutSine)
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

        #region Eye Open/Close (눈 뜨기/감기 효과)

        // Eye 바 캐싱
        RectTransform rtEyeTop;
        RectTransform rtEyeBottom;
        float eyeHalfHeight;
        bool eyeInitialized;
        Sequence eyeSequence;

        /// <summary>
        /// Eye 바가 없으면 런타임에 자동 생성 (Stage 캔버스 최상단 — BG/캐릭터를 가리되 대사창은 안 가림)
        /// </summary>
        void EnsureEyeBars()
        {
            if (eyeTop != null && eyeBottom != null) return;

            // Eye 바는 Stage 캔버스(sort 0)에 배치해야
            // Overlay 캔버스(Dialogue, sort 1)보다 아래에서 렌더됨
            Transform eyeParent = null;

            // 1. stageCanvas가 바인딩되어 있으면 그 아래에 생성
            if (stageCanvas != null)
            {
                eyeParent = stageCanvas.transform;
            }
            else
            {
                // 2. StageManager → StageRig → StageCanvas 탐색
                var stageRig = StageManager.Instance?.GetComponentInChildren<StageRig>(true);
                if (stageRig?.StageCanvas != null)
                    eyeParent = stageRig.StageCanvas.transform;
            }

            if (eyeParent == null)
            {
                Debug.LogWarning("[ScreenFX] EnsureEyeBars: Stage 캔버스를 찾을 수 없어 Eye 바 생성 불가");
                return;
            }

            if (eyeTop == null)
            {
                var goTop = new GameObject("EyeTop", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                goTop.layer = LayerMask.NameToLayer("UI");
                goTop.transform.SetParent(eyeParent, false);
                goTop.transform.SetAsLastSibling();  // Stage 내 최상단 (BG/캐릭터 위)
                eyeTop = goTop.GetComponent<Image>();
                eyeTop.color = Color.black;
                eyeTop.raycastTarget = false;
                goTop.SetActive(false);
            }

            if (eyeBottom == null)
            {
                var goBottom = new GameObject("EyeBottom", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                goBottom.layer = LayerMask.NameToLayer("UI");
                goBottom.transform.SetParent(eyeParent, false);
                goBottom.transform.SetAsLastSibling();
                eyeBottom = goBottom.GetComponent<Image>();
                eyeBottom.color = Color.black;
                eyeBottom.raycastTarget = false;
                goBottom.SetActive(false);
            }

            Debug.Log("[ScreenFX] Eye 바 자동 생성 완료 (Stage 캔버스)");
        }

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
