using System.Collections.Generic;
using LoveAlgo.Contracts;
using System.Text;
using UnityEngine;

namespace LoveAlgo.Story.StoryEngine
{
    /// <summary>
    /// 파싱된 ScriptLine 리스트에 대해 Preflight 검증 수행.
    /// 알 수 없는 FX/매크로 명령, 인자 갯수 부족·과다, 잘못된 NextType, D9/D13 태그 균형 등을
    /// 잡아 Violation 리스트 반환.
    /// Editor 메뉴 `Tools/Story/Validate Story CSV`에서 호출하거나, 런타임 디버그 시 사용.
    /// </summary>
    public static class ScriptValidator
    {
        /// <summary>
        /// D18: Strict 모드. true면 D9/D13 태그 검증 결과의 Warning을 Error로 격상.
        /// 빌드 preflight에서 켜고 작가가 깨뜨린 채로 출시되지 않도록 게이팅.
        /// </summary>
        public static bool Strict { get; set; }

        /// <summary>
        /// D18: color palette — &lt;color=name&gt; 검증용. null이면 palette 검증 스킵 (hex만 통과).
        /// Editor preflight가 SO 로드해서 주입.
        /// </summary>
        public static IDictionary<string, Color> ColorPalette { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStaticStateOnLoad()
        {
            Strict = false;
            ColorPalette = null;
        }

        public class Violation
        {
            public int LineNumber;     // 1-base CSV 줄 (ScriptLine.SourceLine 기반)
            public string LineID;
            public LineType Type;
            public string Value;
            public string Severity;    // "Error" / "Warning"
            public string Message;

            public override string ToString() =>
                $"[{Severity}] L{LineNumber} ({Type} {LineID}): {Message} — '{Value}'";
        }

        /// <summary>스크립트 라인 리스트 검증 — Violation 컬렉션 반환.</summary>
        public static List<Violation> Validate(IList<ScriptLine> lines)
        {
            var result = new List<Violation>();
            if (lines == null) return result;

            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                ValidateLine(line, result);
            }
            return result;
        }

        static void ValidateLine(ScriptLine line, List<Violation> result)
        {
            // FX 검증 — 매크로/효과 명령 화이트리스트 + 인자 갯수
            if (line.Type == LineType.FX)
            {
                if (string.IsNullOrEmpty(line.Value))
                {
                    Add(result, line, "Error", "FX 라인의 Value가 비어 있음");
                    return;
                }

                var parts = line.Value.Split(':');
                string canonical = CommandAliases.NormalizeFX(parts[0]);

                if (!CommandAliases.IsKnownFX(parts[0]))
                {
                    Add(result, line, "Error", $"알 수 없는 FX/매크로 명령: '{parts[0]}'. 오타이거나 등록 안 됨.");
                    return;
                }

                int argCount = parts.Length - 1;
                if (!FXCommandSignatures.TryValidate(canonical, argCount, out var sig, out var err))
                {
                    Add(result, line, "Error", err);
                }
            }

            // BG 검증 — transition 토큰 확인
            else if (line.Type == LineType.BG && !string.IsNullOrEmpty(line.Value))
            {
                var parts = line.Value.Split(':');
                if (parts.Length >= 2)
                {
                    string t = parts[1].Trim();
                    string canonical = CommandAliases.NormalizeBGTransition(t);
                    // canonical이 입력과 다르고 알려진 토큰이 아니면 알 수 없는 transition
                    bool isKnown = canonical == "Cut" || canonical == "Fade" || canonical == "CrossFade";
                    if (!isKnown)
                        Add(result, line, "Warning", $"알 수 없는 BG 전환: '{t}'. 사용 가능: Cut/Fade/Cross/CrossFade.");
                }
            }

            // Flow 검증 — LockScreen 서브명령 확인 (다른 Flow는 자유 형식)
            else if (line.Type == LineType.Flow && !string.IsNullOrEmpty(line.Value))
            {
                var parts = line.Value.Split(':');

                // Mark 라벨 필수 검증 — 자동 점프 메뉴·합성기 출처 추적용
                if (string.Equals(parts[0], "Mark", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Length < 2 || string.IsNullOrWhiteSpace(parts[1]))
                        Add(result, line, "Error",
                            "Mark에는 라벨이 필요합니다. 예: `Mark:school_morning` (씬 식별자, unique).");
                }

                if (string.Equals(parts[0], "LockScreen", System.StringComparison.OrdinalIgnoreCase))
                {
                    if (parts.Length < 2)
                    {
                        Add(result, line, "Error",
                            "LockScreen 서브명령 부족 — FirstSetup/Normal/Reset/Auto/GameStart 중 하나 필요");
                    }
                    else
                    {
                        string sub = parts[1];
                        bool known =
                            string.Equals(sub, "FirstSetup",     System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sub, "OpenFirstSetup", System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sub, "Normal",         System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sub, "OpenNormal",     System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sub, "Reset",          System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sub, "OpenReset",      System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sub, "Auto",           System.StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(sub, "GameStart",      System.StringComparison.OrdinalIgnoreCase);
                        if (!known)
                            Add(result, line, "Error",
                                $"LockScreen 서브명령 불명: '{sub}'. 사용 가능: FirstSetup/Normal/Reset/Auto/GameStart.");
                    }
                }
            }

            // Char 검증 — 슬롯/액션 위치 + 액션별 최소 인자 갯수
            else if (line.Type == LineType.Char && !string.IsNullOrEmpty(line.Value))
            {
                ValidateChar(line, result);
            }

            // D18: 대사/선택지/Place 본문의 D9/D13 태그 검증
            // 대사(Text) Value, 선택지(Option) Value의 '|' 앞 부분(ButtonText), Place Value 본문
            if (line.Type == LineType.Text && !string.IsNullOrEmpty(line.Value))
            {
                InspectDialogueBody(line, line.Value, result);
            }
            else if (line.Type == LineType.Option && !string.IsNullOrEmpty(line.Value))
            {
                int pipe = line.Value.IndexOf('|');
                string buttonText = pipe >= 0 ? line.Value.Substring(0, pipe) : line.Value;
                InspectDialogueBody(line, buttonText, result);
            }
            else if (line.Type == LineType.Place && !string.IsNullOrEmpty(line.Value))
            {
                InspectDialogueBody(line, line.Value, result);
            }
        }

        /// <summary>D18: 본문 한 줄의 D9/D13 태그를 inspector에 넘기고 issue를 Violation으로 변환.</summary>
        static void InspectDialogueBody(ScriptLine line, string body, List<Violation> result)
        {
            var issues = DialogueTagInspector.Inspect(body, ColorPalette);
            if (issues.Count == 0) return;

            string severity = Strict ? "Error" : "Warning";
            for (int i = 0; i < issues.Count; i++)
            {
                var iss = issues[i];
                Add(result, line, severity, $"태그 {iss.TagName} — {iss.Detail} ({iss.Kind})");
            }
        }

        /// <summary>
        /// Char 라인 문법:
        ///   [슬롯:]액션[:인자1[:인자2…]]
        /// 슬롯이 생략되면 C 자동 주입(CharLineExecutor.NormalizeCharValue).
        /// 액션별 최소 인자: Enter/EnterUp/Move는 1, Emote는 1, Exit/ExitDown/Clear는 0.
        /// </summary>
        static void ValidateChar(ScriptLine line, List<Violation> result)
        {
            var parts = line.Value.Split(':');
            string first = parts[0];
            bool firstIsSlot = CommandAliases.NormalizeSlot(first) != null;
            bool firstIsAction = CommandAliases.IsCharAction(first);

            if (!firstIsSlot && !firstIsAction)
            {
                Add(result, line, "Error", $"Char 라인 첫 토큰이 슬롯(L/C/R)도 액션(Enter/Exit/Emote/…)도 아님: '{first}'");
                return;
            }

            // 액션은 슬롯 다음(있다면) 또는 첫 토큰
            int actionIdx = firstIsSlot ? 1 : 0;
            if (actionIdx >= parts.Length)
            {
                Add(result, line, "Error", $"Char 라인에 액션이 없음 (슬롯만): '{line.Value}'");
                return;
            }

            string action = parts[actionIdx];
            if (!CommandAliases.IsCharAction(action))
            {
                Add(result, line, "Error", $"Char 라인 액션 위치에 알 수 없는 키워드: '{action}'. 사용 가능: Enter/Exit/Emote/EnterUp/ExitDown/Clear/Move");
                return;
            }

            int argCount = parts.Length - actionIdx - 1;
            string err = ValidateCharActionArgs(action, argCount);
            if (err != null)
                Add(result, line, "Error", err);
        }

        static string ValidateCharActionArgs(string action, int argCount)
        {
            // 케이스 무시 비교
            bool Is(string s) => string.Equals(action, s, System.StringComparison.OrdinalIgnoreCase);

            if (Is("Enter") || Is("EnterUp"))
                return argCount < 1 ? $"Char:{action}는 캐릭터 이름이 필요: {action}:<이름>[:<표정>]" : null;
            if (Is("Emote"))
                return argCount < 1 ? "Char:Emote는 표정 인자가 필요: Emote:<표정>" : null;
            if (Is("Move"))
                return argCount < 1 ? "Char:Move는 대상 슬롯이 필요: Move:<L|C|R>" : null;
            // Exit/ExitDown/Clear는 인자 없어도 OK
            return null;
        }

        static void Add(List<Violation> list, ScriptLine line, string severity, string msg)
        {
            list.Add(new Violation
            {
                LineNumber = line.SourceLine,
                LineID = line.LineID,
                Type = line.Type,
                Value = line.Value,
                Severity = severity,
                Message = msg,
            });
        }

        /// <summary>Violation 리스트를 사람-친화 텍스트로 포맷팅.</summary>
        public static string FormatReport(IList<Violation> violations)
        {
            if (violations == null || violations.Count == 0)
                return "OK — 위반 사항 없음.";

            var sb = new StringBuilder();
            int errors = 0, warnings = 0;
            for (int i = 0; i < violations.Count; i++)
            {
                var v = violations[i];
                if (v.Severity == "Error") errors++;
                else warnings++;
                sb.AppendLine(v.ToString());
            }
            sb.AppendLine();
            sb.AppendLine($"총 {violations.Count}건 (Error {errors} / Warning {warnings})");
            return sb.ToString();
        }
    }
}
