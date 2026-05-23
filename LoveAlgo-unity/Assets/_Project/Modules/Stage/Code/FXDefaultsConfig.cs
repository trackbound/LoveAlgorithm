using UnityEngine;

namespace LoveAlgo.Core
{
    /// <summary>
    /// FX/캐릭터/배경 연출의 기본 지속시간·강도를 한곳에 모은 단일 진실 공급원(SoT).
    /// Resources/Data/FXDefaultsConfig 에 asset 배치 후 모든 시스템이 참조.
    /// asset이 없으면 코드 폴백(각 컴포넌트 SerializedField/하드코딩)이 동작 — 안전망.
    /// </summary>
    [CreateAssetMenu(fileName = "FXDefaultsConfig", menuName = "LoveAlgo/FX Defaults Config")]
    public class FXDefaultsConfig : ScriptableObject
    {
        static FXDefaultsConfig _instance;
        static bool _loaded;

        /// <summary>Resources/Data/FXDefaultsConfig 로드. 없으면 null — 호출 측에서 ?. 폴백.</summary>
        public static FXDefaultsConfig Instance
        {
            get
            {
                if (_loaded) return _instance;
                _instance = Resources.Load<FXDefaultsConfig>("Data/FXDefaultsConfig");
                _loaded = true;
                if (_instance == null)
                {
                    Debug.LogWarning("[FXDefaultsConfig] Resources/Data/FXDefaultsConfig.asset 없음 — 컴포넌트 기본값으로 폴백");
                }
                return _instance;
            }
        }

        [Header("Screen Fade / Flash")]
        public float fadeDuration = 0.9f;
        public float fadeOutHoldTail = 0.05f;
        public float flashDuration = 0.14f;

        [Header("Camera")]
        public float camZoomDuration = 0.5f;
        public float camPanDuration = 0.5f;
        public float camResetDuration = 0.4f;

        [Header("Shake (preset strengths)")]
        public float shakeWeak = 10f;
        public float shakeMedium = 25f;
        public float shakeStrong = 50f;
        public float shakeDuration = 0.3f;

        [Header("Color Tint")]
        public float tintAlpha = 0.25f;
        public float tintDuration = 0.5f;

        [Header("Eye")]
        public float eyeOpenDuration = 0.8f;
        public float eyeCloseDuration = 0.8f;
        public float eyeBlinkClose = 0.1f;
        public float eyeBlinkOpen = 0.15f;
        public float eyeBlinkHold = 0.05f;

        [Header("Character")]
        public float charEnterDuration = 0.5f;
        public float charExitDuration = 0.4f;
        public float charEmoteDuration = 0.25f;
        public float charShakeStrength = 18f;   // CharLayer 15 ↔ CharSlot 20 통일
        public float charShakeDuration = 0.3f;
        public float charJumpHeight = 35f;      // CharLayer 30 ↔ CharSlot 40 통일
        public float charJumpDuration = 0.3f;
        public float charDimAlpha = 0.4f;
        public float charDimDuration = 0.3f;
        public float charGlitchStrength = 1.0f;
        public float charGlitchDuration = 0.6f;

        [Header("Background / CG / Overlay / SD")]
        public float bgTransitionDuration = 0.5f;
        public float cgFadeDuration = 0.5f;
        public float overlayFadeDuration = 0.5f;
        public float monologueDimDuration = 0.3f;
        public float monologueDimAlpha = 1.0f;
        public float sdFadeDuration = 0.5f;

        [Header("Macros")]
        public float dayEndFadeOut = 0.8f;
        public float dayEndFadeIn = 0.3f;
        public float sceneStartPauseAfterFadeIn = 0.4f;
        public float sceneStartFadeEyeClosed = 0.3f;
        public float sceneStartFadeEyeOpen = 0.6f;
        public float sceneEndFadeOut = 0.5f;

        // ══════════════════════════════════════════════
        //  LockScreen
        // ══════════════════════════════════════════════
        [Header("LockScreen")]
        [Tooltip("게임 첫 시작 시 잠금화면 페이드인 (useFirstStartFadeIn=true일 때만).")]
        public float lockScreenFirstStartFadeIn = 0.8f;
        [Tooltip("Outro Phase 1 — 검정 오버레이가 panel을 덮으며 등장.")]
        public float lockScreenOutroToBlack = 0.5f;
        [Tooltip("Outro Phase 2 (옵션 :FadeOut) — 검정이 사라지며 다음 화면 reveal.")]
        public float lockScreenOutroFromBlack = 0.5f;
        [Tooltip("기획서: 잠금화면 표출 후 로아 메시지 시작까지 대기.")]
        public float lockScreenBeforeMessages = 5f;
        [Tooltip("기획서: 마지막 로아 메시지 후 클릭 가능까지 대기.")]
        public float lockScreenAfterLastMessage = 3f;

        // ══════════════════════════════════════════════
        //  Loading Screen
        // ══════════════════════════════════════════════
        [Header("Loading Screen")]
        [Tooltip("로딩 일러스트 페이드인.")]
        public float loadingScreenFadeIn = 0.3f;
        [Tooltip("로딩 일러스트/패널 페이드아웃.")]
        public float loadingScreenFadeOut = 0.3f;
        [Tooltip("로딩 화면 최소 유지 시간 (자동저장 등 짧은 작업도 사용자가 인식할 시간).")]
        public float loadingScreenMinHold = 0.4f;

        // ══════════════════════════════════════════════
        //  Phase Transition (Title ↔ Prologue 등 글로벌 전환)
        // ══════════════════════════════════════════════
        [Header("Phase Transition (Global)")]
        [Tooltip("Title ↔ Prologue, Prologue ↔ DayLoop 등 Phase 간 전환 페이드 — 글로벌 통일 값.")]
        public float phaseTransitionFade = 0.3f;
        [Tooltip("Phase 전환 끝, 인게임 reveal 페이드 인 (분위기 살리려 길게).")]
        public float phaseTransitionReveal = 0.8f;
    }
}
