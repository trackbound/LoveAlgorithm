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
            sb.AppendLine($"| `DayStart` | `[bgPath][:Wake\\|Cut\\|Reveal][:actionCount]` | mode=Wake |");
            sb.AppendLine($"| `DayEnd` | `[fadeDuration][:Wake\\|Cut]` | fadeOut={F(cfg?.dayEndFadeOut, 0.8f)}s, fadeIn={F(cfg?.dayEndFadeIn, 0.3f)}s, mode=Wake |");
            sb.AppendLine($"| `NextDay` | `[Wake\\|Cut][:bgPath][:actionCount]` | DayEnd + DayStart 일괄 |");
            sb.AppendLine($"| `SceneStart` | `[bgPath[:EyeClose]]` | eyeOpen={F(cfg?.sceneStartFadeEyeOpen, 0.6f)}s, pauseAfter={F(cfg?.sceneStartPauseAfterFadeIn, 0.4f)}s |");
            sb.AppendLine($"| `SceneEnd` | `[fadeDuration]` | {F(cfg?.sceneEndFadeOut, 0.5f)}s |");
            sb.AppendLine($"| `LoadingScene` (alias `Loading`) | `[displayTime]` | 2.0s |");
            sb.AppendLine($"| `Setup` | `BG=...\\|BGM=...\\|Char=...[:slot]\\|Overlay=...` | 즉시(Cut) 전환 |");
            sb.AppendLine($"| `Wait` | `[seconds]` | 1.0s |");
            sb.AppendLine($"| `DialogueHide` | — | — |");
            sb.AppendLine($"| `DialogueShow` | — | — |");
            sb.AppendLine();

            // ── 씬 전환 패턴 ──
            sb.AppendLine("## 씬 전환 패턴");
            sb.AppendLine();
            sb.AppendLine("> **핵심 차이**:");
            sb.AppendLine("> - **Wake**: 검은 화면에서 EyeMask로만 가린 상태 → **대사창은 보임** (눈 감고 모놀로그 가능)");
            sb.AppendLine("> - **Cut**: ScreenFX가 풀 암전 → **대사 불가**, 다음 씬은 페이드인으로 reveal");
            sb.AppendLine();

            sb.AppendLine("### Pattern A — Wake (잠들기 → 다음날 아침)");
            sb.AppendLine("```");
            sb.AppendLine("FX,,DayEnd,await                              # = DayEnd:Wake (기본)");
            sb.AppendLine("Text,,(잠이 들었다...),click");
            sb.AppendLine("FX,,DayStart:BG_Room_Morning,await            # 눈 감은 상태 유지");
            sb.AppendLine("Text,로아,(어... 벌써 아침인가),click          # 눈 감은 채 모놀로그");
            sb.AppendLine("FX,,Open:0.8,await                            # 눈 뜨기");
            sb.AppendLine("Char,,Enter:로아:Default,await");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("### Pattern B — Cut (다른 장소로 즉시 전환)");
            sb.AppendLine("```");
            sb.AppendLine("FX,,DayEnd:Cut,await");
            sb.AppendLine("FX,,DayStart:BG_Cafe_Day:Cut,await            # 풀 암전 → BG 페이드인");
            sb.AppendLine("Char,,Enter:로아:Default,await");
            sb.AppendLine("Text,로아,왔어?,click");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("### Pattern C — NextDay sugar (한 줄)");
            sb.AppendLine("```");
            sb.AppendLine("FX,,NextDay:Wake:BG_Room_Morning,await        # Pattern A 한 줄");
            sb.AppendLine("FX,,NextDay:Cut:BG_Cafe_Day,await             # Pattern B 한 줄");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("### Pattern D — Loading scene 거쳐서 전환");
            sb.AppendLine("```");
            sb.AppendLine("FX,,DayEnd:Cut,await");
            sb.AppendLine("FX,,Loading:2.0,await                         # = Flow,,LoadingScene:2.0");
            sb.AppendLine("FX,,DayStart:BG_Cafe_Day:Cut,await");
            sb.AppendLine("```");
            sb.AppendLine();

            // ── PC 잠금 (LockScreen) ──
            sb.AppendLine("## PC 잠금 화면 (LockScreen)");
            sb.AppendLine();
            sb.AppendLine("기획서 §진입 정보: 게임 첫 시작 시 타이틀 대신 잠금 화면이 5초 페이드인으로 등장.");
            sb.AppendLine("그 후 스토리 진행 중에도 비번 입력 연출이 필요할 때 호출.");
            sb.AppendLine();
            sb.AppendLine("### Flow 명령 시그니처");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("Flow,,LockScreen:<mode>[:Time=HH:mm][:FadeOut|NoFadeOut],await");
            sb.AppendLine("```");
            sb.AppendLine();
            sb.AppendLine("| 모드 | 설명 |");
            sb.AppendLine("|---|---|");
            sb.AppendLine("| `FirstSetup` | 비번 첫 설정 (이름 입력 직후) — 평문 입력, 마스킹 없음 |");
            sb.AppendLine("| `Normal` | 평상 잠금화면 — 저장된 비번 검증, * 마스킹 |");
            sb.AppendLine("| `Reset` | 재설정 — 기존 비번 확인 없이 새 비번 입력 (FirstSetup 흐름) |");
            sb.AppendLine("| `Auto` | 비번 설정 여부 자동 판별 — 있으면 Normal, 없으면 FirstSetup |");
            sb.AppendLine("| `GameStart` | 게임 첫 시작 sugar — 5초 페이드인 강제 + Auto |");
            sb.AppendLine();
            sb.AppendLine("**옵션 토큰** (순서 자유, 케이스 무시):");
            sb.AppendLine("- `Time=HH:mm` — 시계 1회 오버라이드");
            sb.AppendLine("- `FadeOut` — Outro에 페이드아웃(black→0)까지 포함 → 완료 시 화면 노출됨");
            sb.AppendLine("- `NoFadeOut` — 페이드아웃 생략 (기본 — 검은 화면으로 끝남, 다음 라인이 FadeIn 처리)");
            sb.AppendLine();

            sb.AppendLine("### 사용 패턴");
            sb.AppendLine();
            sb.AppendLine("#### A — 게임 첫 시작 (EntryRouter가 자동 호출, 보통 CSV 불필요)");
            sb.AppendLine("```");
            sb.AppendLine("# 거의 사용 안 함 — EntryRouter가 GameStart를 자동 실행");
            sb.AppendLine("Flow,,LockScreen:GameStart:FadeOut,await");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("#### B — 이름 입력 직후 비번 설정 (튜토리얼)");
            sb.AppendLine("```");
            sb.AppendLine("Text,로아,이름이 뭐야?,click");
            sb.AppendLine("Flow,,Username,await                          # 이름 입력");
            sb.AppendLine("Text,로아,이제 비밀번호도 설정하자.,click");
            sb.AppendLine("Flow,,LockScreen:FirstSetup,await             # 비번 첫 설정 — 다음 라인이 검은 상태에서 시작");
            sb.AppendLine("FX,,FadeIn:3,await                             # 검은 화면 페이드아웃");
            sb.AppendLine("Text,로아,비밀번호 잘 기억해.,click");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("#### C — 스토리 중 재로그인 (다른 날 시작 등)");
            sb.AppendLine("```");
            sb.AppendLine("Flow,,LockScreen:Normal:Time=07:30:FadeOut,await");
            sb.AppendLine("Text,로아,일어났어?,click");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("#### D — 비번 자동 판별 (이미 설정됐는지 모를 때)");
            sb.AppendLine("```");
            sb.AppendLine("Flow,,LockScreen:Auto:Time=23:58,await        # IsPasswordSet ? Normal : FirstSetup");
            sb.AppendLine("```");
            sb.AppendLine();

            sb.AppendLine("### 분기 흐름 요약");
            sb.AppendLine();
            sb.AppendLine("```");
            sb.AppendLine("게임 실행");
            sb.AppendLine("├─ 비번 없음 (첫 실행) ──→ EntryRouter → LockScreen:GameStart (5s 페이드인) → FirstSetup → 비번 저장 → Title");
            sb.AppendLine("└─ 비번 있음 (재실행)   ──→ EntryRouter → Title (LockScreen 우회)");
            sb.AppendLine("");
            sb.AppendLine("스토리 진행 중");
            sb.AppendLine("├─ 이름 입력 직후      ──→ Flow,,LockScreen:FirstSetup,await");
            sb.AppendLine("├─ 하루 시작 연출      ──→ Flow,,LockScreen:Normal:Time=07:30:FadeOut,await");
            sb.AppendLine("└─ 비번 분실 흐름       ──→ Flow,,LockScreen:Reset,await");
            sb.AppendLine("```");
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
