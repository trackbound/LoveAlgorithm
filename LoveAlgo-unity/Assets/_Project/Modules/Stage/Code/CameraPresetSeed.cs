using System.Collections.Generic;

namespace LoveAlgo.Stage
{
    /// <summary>
    /// 카메라 프리셋 시드 콘텐츠 정의 (Phase D14).
    /// 기획자/디자이너가 SO를 처음 만들 때 이 목록이 자동으로 채워짐.
    ///
    /// CameraPresetTable의 내장 폴백과 동기 유지 — D5 폴백 6종 + D14 추가 3종.
    /// 폴백 항목과 별도로 정의해 두면, 디자이너가 자산을 만든 후 폴백을 코드에서 조정해도
    /// 자산은 자동 반영되지 않음 (의도된 분리 — 자산 우선 정책 [D5]).
    ///
    /// 사용:
    ///   Editor 메뉴 'Tools/Camera/Generate Default Presets'에서 호출 → SO 인스턴스 채움.
    ///   런타임에선 호출 안 함 — CameraPresetTable이 SO를 직접 로드.
    /// </summary>
    public static class CameraPresetSeed
    {
        /// <summary>D14 시드 — D5 폴백 6종 + 추가 3종.</summary>
        public static List<CameraPresetSO.Entry> BuildSeedEntries()
        {
            return new List<CameraPresetSO.Entry>
            {
                // ── D5 폴백과 동일한 6종 (개념적 동기) ───────────────────
                new CameraPresetSO.Entry
                {
                    id = "ZoomIn-Soft",
                    notes = "느린 줌인 — 대사 강조용",
                    steps = { new CameraPresetSO.Step { command = "CamZoom:1.1:0.6" } }
                },
                new CameraPresetSO.Entry
                {
                    id = "ZoomIn-Hard",
                    notes = "빠르고 강한 줌인 — 충격 컷",
                    steps =
                    {
                        new CameraPresetSO.Step { command = "CamZoom:1.25:0.18" },
                        new CameraPresetSO.Step { command = "CamShake:0.18:medium", delaySec = 0f },
                    }
                },
                new CameraPresetSO.Entry
                {
                    id = "ZoomOut",
                    notes = "원래 위치로 복귀",
                    steps = { new CameraPresetSO.Step { command = "CamReset:0.5" } }
                },
                new CameraPresetSO.Entry
                {
                    id = "PunchHit",
                    notes = "타격감 — 짧고 강한 흔들림 + 플래시",
                    steps =
                    {
                        new CameraPresetSO.Step { command = "Flash:0.12", waitForCompletion = false },
                        new CameraPresetSO.Step { command = "CamShake:0.25:strong" },
                    }
                },
                new CameraPresetSO.Entry
                {
                    id = "DramaticReveal",
                    notes = "큰 전환 — 페이드아웃 → 페이드인",
                    steps =
                    {
                        new CameraPresetSO.Step { command = "FadeOut:0.6" },
                        new CameraPresetSO.Step { command = "FadeIn:0.8" },
                    }
                },
                new CameraPresetSO.Entry
                {
                    id = "Heartbeat",
                    notes = "약한 반복 진동 — 긴장 분위기",
                    steps =
                    {
                        new CameraPresetSO.Step { command = "CamShake:0.15:weak" },
                        new CameraPresetSO.Step { command = "CamShake:0.15:weak", delaySec = 0.2f },
                    }
                },

                // ── D14 추가 3종 (시드 전용) ───────────────────────────
                new CameraPresetSO.Entry
                {
                    id = "DialogueImpact",
                    notes = "대사 한 줄 강조 — 대사창만 떨림",
                    steps = { new CameraPresetSO.Step { command = "DialogueShake:0.2:medium" } }
                },
                new CameraPresetSO.Entry
                {
                    id = "ConfessionMoment",
                    notes = "고백 직전 — 느린 줌인 + 약한 흔들림",
                    steps =
                    {
                        new CameraPresetSO.Step { command = "CamZoom:1.08:1.2" },
                        new CameraPresetSO.Step { command = "CamShake:0.4:weak", delaySec = 0.3f, waitForCompletion = false },
                    }
                },
                new CameraPresetSO.Entry
                {
                    id = "WhiteFlash",
                    notes = "회상/플래시백 진입 — 흰 깜빡임 + 줌아웃",
                    steps =
                    {
                        new CameraPresetSO.Step { command = "Flash:0.2", waitForCompletion = false },
                        new CameraPresetSO.Step { command = "CamReset:0.6", delaySec = 0.1f },
                    }
                },
            };
        }

        /// <summary>D5 폴백과 D14 시드가 다루는 id 합집합 — 폴백에 있는 id가 시드에서 빠지면 안 됨.</summary>
        public static readonly string[] FallbackIds = {
            "ZoomIn-Soft", "ZoomIn-Hard", "ZoomOut", "PunchHit", "DramaticReveal", "Heartbeat"
        };
    }
}
