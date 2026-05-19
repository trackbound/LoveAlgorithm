#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using LoveAlgo.Core;
using LoveAlgo.Story.StoryEngine;

namespace LoveAlgo.StoryEditor
{
    /// <summary>
    /// Story CSV 작성용 명령 치트시트 자동 생성.
    /// FXCommandSignatures + FXDefaultsConfig를 기반으로 항상 최신 reference를 docs로 출력.
    /// </summary>
    public static class FXReferenceGenerator
    {
        const string OutputPath = "docs/STORY_COMMANDS.md";

        [MenuItem("Tools/Story/Generate FX Reference (Markdown)")]
        public static void Generate()
        {
            var cfg = FXDefaultsConfig.Instance;
            var sb = new StringBuilder();

            sb.AppendLine("# Story CSV Commands Reference");
            sb.AppendLine();
            sb.AppendLine("자동 생성 파일 — `Tools/Story/Generate FX Reference` 메뉴로 갱신.");
            sb.AppendLine($"기본값 출처: `Assets/Resources/Data/FXDefaultsConfig.asset` " +
                          (cfg != null ? "(로드 성공)" : "(asset 없음 → 코드 기본값)"));
            sb.AppendLine();

            // ── 컬럼 + Next 타입 ──
            sb.AppendLine("## 컬럼");
            sb.AppendLine();
            sb.AppendLine("`LineID, Type, Speaker, Value, Next`");
            sb.AppendLine();
            sb.AppendLine("- **LineID**: 점프 anchor (선택). 비워두면 순차 실행.");
            sb.AppendLine("- **Type**: `Text` / `Char` / `BG` / `CG` / `SD` / `Overlay` / `Sound` / `FX` / `Flow` / `Choice` / `Option` / `Place`");
            sb.AppendLine("- **Speaker**: 화자(캐릭터 ID 또는 displayName). 비워두면 나레이션.");
            sb.AppendLine("- **Value**: 명령별 페이로드 (콜론 구분).");
            sb.AppendLine("- **Next**: `>` (즉시) / `click` (클릭 대기) / `await` (액션 완료까지) / 숫자 (초 단위 대기)");
            sb.AppendLine();

            // ── FX 명령 표 ──
            sb.AppendLine("## FX 명령");
            sb.AppendLine();
            sb.AppendLine("| 명령 | 시그니처 | 기본값 | 별칭 |");
            sb.AppendLine("|---|---|---|---|");

            AppendFX(sb, "FadeOut",       $"duration={F(cfg?.fadeDuration, 0.9f)}s",     "—");
            AppendFX(sb, "FadeIn",        $"duration={F(cfg?.fadeDuration, 0.9f)}s",     "—");
            AppendFX(sb, "Flash",         $"duration={F(cfg?.flashDuration, 0.14f)}s",   "—");
            AppendFX(sb, "CamShake",      $"duration={F(cfg?.shakeDuration, 0.3f)}s, strength=Medium({F(cfg?.shakeMedium, 25f)})", "Shake");
            AppendFX(sb, "StageShake",    $"duration={F(cfg?.shakeDuration, 0.3f)}s, strength=Medium", "—");
            AppendFX(sb, "DialogueShake", $"duration={F(cfg?.shakeDuration, 0.3f)}s, strength=Medium", "—");
            AppendFX(sb, "CamZoom",       $"zoom=1.0, duration={F(cfg?.camZoomDuration, 0.5f)}s",    "Zoom");
            AppendFX(sb, "CamPan",        $"x=0, y=0, duration={F(cfg?.camPanDuration, 0.5f)}s",     "Pan");
            AppendFX(sb, "CamReset",      $"duration={F(cfg?.camResetDuration, 0.4f)}s",            "Reset");
            AppendFX(sb, "ColorTint",     $"preset, alpha={F(cfg?.tintAlpha, 0.25f)}, duration={F(cfg?.tintDuration, 0.5f)}s", "Tint");
            AppendFX(sb, "EyeOpen",       $"duration={F(cfg?.eyeOpenDuration, 0.8f)}s",  "Open");
            AppendFX(sb, "EyeClose",      $"duration={F(cfg?.eyeCloseDuration, 0.8f)}s", "Close");
            AppendFX(sb, "EyeCloseImmediate", "—", "—");
            AppendFX(sb, "EyeBlink",      $"close={F(cfg?.eyeBlinkClose, 0.1f)}s, open={F(cfg?.eyeBlinkOpen, 0.15f)}s, hold={F(cfg?.eyeBlinkHold, 0.05f)}s", "Blink");
            AppendFX(sb, "CharShake",     $"slot, strength={F(cfg?.charShakeStrength, 18f)}, duration={F(cfg?.charShakeDuration, 0.3f)}s", "—");
            AppendFX(sb, "CharJump",      $"slot, height={F(cfg?.charJumpHeight, 35f)}, duration={F(cfg?.charJumpDuration, 0.3f)}s", "—");
            AppendFX(sb, "CharDim",       $"slot, alpha={F(cfg?.charDimAlpha, 0.4f)}, duration={F(cfg?.charDimDuration, 0.3f)}s", "—");
            AppendFX(sb, "CharGlitch",    $"slot, strength={F(cfg?.charGlitchStrength, 1.0f)}, duration={F(cfg?.charGlitchDuration, 0.6f)}s", "—");

            sb.AppendLine();
            sb.AppendLine("> Shake 강도는 숫자(`30`) 또는 프리셋(`Weak`/`Medium`/`Strong`) 모두 가능. 케이스 무시.");
            sb.AppendLine();

            // ── 매크로 ──
            sb.AppendLine("## 매크로");
            sb.AppendLine();
            sb.AppendLine("| 매크로 | 인자 | 기본값 |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| `DayStart` | `[bgPath[:actionCount]]` | — |");
            sb.AppendLine($"| `DayEnd` | `[fadeDuration]` | fadeOut={F(cfg?.dayEndFadeOut, 0.8f)}s, fadeIn={F(cfg?.dayEndFadeIn, 0.3f)}s |");
            sb.AppendLine($"| `SceneStart` | `[fadeDuration]` | eyeClosed={F(cfg?.sceneStartFadeEyeClosed, 0.3f)}s, eyeOpen={F(cfg?.sceneStartFadeEyeOpen, 0.6f)}s, pauseAfter={F(cfg?.sceneStartPauseAfterFadeIn, 0.4f)}s |");
            sb.AppendLine($"| `SceneEnd` | `[fadeDuration]` | {F(cfg?.sceneEndFadeOut, 0.5f)}s |");
            sb.AppendLine($"| `Setup` | `BG=...|BGM=...|Char=...[:slot]|Overlay=...` | 즉시(Cut) 전환 |");
            sb.AppendLine($"| `Wait` | `[seconds]` | 1.0s |");
            sb.AppendLine($"| `DialogueHide` | — | — |");
            sb.AppendLine($"| `DialogueShow` | — | — |");
            sb.AppendLine();

            // ── Char 액션 ──
            sb.AppendLine("## Char 액션");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("Char,,[slot:]Enter:캐릭터[:표정[:오버레이]]    # 페이드 등장");
            sb.AppendLine("Char,,[slot:]EnterUp:캐릭터[:표정[:오버레이]]  # 아래→위 슬라이드 등장");
            sb.AppendLine("Char,,[slot:]Emote:표정                       # 표정만 변경");
            sb.AppendLine("Char,,[slot:]Exit                              # 페이드 퇴장");
            sb.AppendLine("Char,,[slot:]ExitDown                          # 아래로 슬라이드 퇴장");
            sb.AppendLine("Char,,[slot:]Clear                             # 즉시 숨김");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("- **slot 생략 시 자동으로 `C` (중앙)** — 신규 단축 문법.");
            sb.AppendLine("- slot: `L` / `C` / `R` (또는 `Left`/`Center`/`Right`).");
            sb.AppendLine();

            // ── BG transition ──
            sb.AppendLine("## BG 전환");
            sb.AppendLine();
            sb.AppendLine("| 토큰 | 동작 | 별칭 |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| `Cut` | 즉시 교체 | — |");
            sb.AppendLine($"| `Fade` | 페이드아웃→교체→페이드인 (기본 {F(cfg?.bgTransitionDuration, 0.5f)}s) | — |");
            sb.AppendLine($"| `CrossFade` | 크로스페이드 | `Cross` |");
            sb.AppendLine();

            // ── Next 타입 + 작성 팁 ──
            sb.AppendLine("## 작성 팁");
            sb.AppendLine();
            sb.AppendLine("1. **명령은 케이스 무시** — `FadeOut`, `fadeout`, `FADEOUT` 모두 동일.");
            sb.AppendLine("2. **별칭 활용** — `FX,,Shake,await` = `FX,,CamShake,await`.");
            sb.AppendLine("3. **Char 슬롯 생략** — `Char,,Enter:로아:Default,await` (슬롯 C 자동).");
            sb.AppendLine("4. **기본값 생략** — `FX,,FadeOut,await`만 써도 SO 기본값 적용.");
            sb.AppendLine("5. **검증** — `Tools/Story/Validate All Story CSV` 실행 시 오타·인자 갯수 오류 콘솔 출력.");
            sb.AppendLine();

            // ── 출력 ──
            string fullPath = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
            File.WriteAllText(fullPath, sb.ToString());
            Debug.Log($"[FXReference] 생성 완료: {fullPath}");
            EditorUtility.RevealInFinder(fullPath);
        }

        static void AppendFX(StringBuilder sb, string name, string defaults, string aliases)
        {
            if (!FXCommandSignatures.Map.TryGetValue(name, out var sig))
            {
                sb.AppendLine($"| `{name}` | (시그니처 미등록) | {defaults} | {aliases} |");
                return;
            }
            sb.AppendLine($"| `{name}` | `{name}{(string.IsNullOrEmpty(sig.Hint) ? "" : ":" + sig.Hint)}` | {defaults} | {aliases} |");
        }

        static string F(float? value, float fallback)
        {
            float v = value ?? fallback;
            return v.ToString("0.##");
        }
    }
}
#endif
