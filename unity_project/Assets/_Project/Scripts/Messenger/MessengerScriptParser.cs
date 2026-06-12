using System;
using LoveAlgo.Core; // CsvUtility

namespace LoveAlgo.Messenger
{
    /// <summary>
    /// 메신저 시퀀스 CSV 순수 파서(EventBus·UnityEngine 비의존, EditMode 테스트).
    /// 1파일 = 1시퀀스(시퀀스 id/방은 카탈로그가 보유 — 파일은 내용만).
    ///
    /// 행 문법 (열: Type,Speaker,Value — 스토리 CSV와 동일하게 #주석/빈 줄/헤더 스킵, "\n" 치환):
    /// <code>
    /// Msg,로아,안녕! 내일 시간 있어?      ← 상대 말풍선(Speaker=발신자)
    /// Me,,그럼 내일 봐!                  ← 주인공 말풍선
    /// Option,,있지! 같이 가자|Love:Roa:1  ← 선택지(연속 Option 행 = 그룹 1개, Love는 호감도 정본 id)
    /// </code>
    /// 옵션 셀은 스토리 Option에서 점프 슬롯만 뺀 <c>텍스트|효과...|if:조건</c> — 메신저는 분기 없는 선형.
    /// </summary>
    public static class MessengerScriptParser
    {
        public static MessengerParseResult Parse(string csv)
        {
            var result = new MessengerParseResult();
            if (string.IsNullOrEmpty(csv)) return result;

            var rows = CsvUtility.SplitRecords(csv);
            for (int i = 0; i < rows.Count; i++)
            {
                string row = rows[i].Text.Trim();
                int lineNumber = rows[i].StartLine;

                if (string.IsNullOrEmpty(row)) continue;          // 빈 줄
                if (row.StartsWith("#")) continue;                 // 주석
                if (row.StartsWith("Type,", StringComparison.OrdinalIgnoreCase)) continue; // 헤더

                ParseRow(row, lineNumber, result);
            }
            return result;
        }

        static void ParseRow(string row, int lineNumber, MessengerParseResult result)
        {
            var columns = CsvUtility.SplitCsv(row);
            if (columns.Length < 3)
            {
                result.Errors.Add($"Line {lineNumber}: 컬럼 부족 ({columns.Length}/3) — Type,Speaker,Value");
                return;
            }

            string type = columns[0].Trim();
            string speaker = columns[1].Trim();
            // 스토리 CSV와 동일: 리터럴 \n → 실제 줄바꿈.
            string value = columns[2].Trim().Replace("\\n", "\n");

            switch (type)
            {
                case "Msg":
                    if (speaker.Length == 0)
                        result.Errors.Add($"Line {lineNumber}: Msg 행에 발신자(Speaker)가 없습니다");
                    else if (value.Length == 0)
                        result.Errors.Add($"Line {lineNumber}: Msg 행에 텍스트가 없습니다");
                    else
                        result.Lines.Add(new MessengerLine { Kind = MessengerLineKind.Message, SenderId = speaker, Text = value });
                    break;

                case "Me":
                    if (value.Length == 0)
                        result.Errors.Add($"Line {lineNumber}: Me 행에 텍스트가 없습니다");
                    else
                        result.Lines.Add(new MessengerLine { Kind = MessengerLineKind.MyMessage, Text = value });
                    break;

                case "Option":
                    var option = ParseOption(value);
                    if (string.IsNullOrEmpty(option.Text))
                    {
                        result.Errors.Add($"Line {lineNumber}: Option 행에 버튼 텍스트가 없습니다");
                        break;
                    }
                    AppendOption(result, option);
                    break;

                default:
                    result.Errors.Add($"Line {lineNumber}: 알 수 없는 Type '{type}' (Msg/Me/Option)");
                    break;
            }
        }

        /// <summary>직전 줄이 선택지 그룹이면 그 그룹에 합류, 아니면 새 그룹 시작(연속 Option = 그룹 1개).</summary>
        static void AppendOption(MessengerParseResult result, MessengerOption option)
        {
            var lines = result.Lines;
            if (lines.Count > 0 && lines[lines.Count - 1].Kind == MessengerLineKind.Choice)
            {
                lines[lines.Count - 1].Options.Add(option);
                return;
            }
            lines.Add(new MessengerLine
            {
                Kind = MessengerLineKind.Choice,
                Options = new System.Collections.Generic.List<MessengerOption> { option }
            });
        }

        /// <summary>옵션 셀 파싱: <c>텍스트|효과1|...|if:조건</c> (스토리 ChoiceParser 문법에서 점프 슬롯 제거형).</summary>
        public static MessengerOption ParseOption(string value)
        {
            var option = new MessengerOption();
            if (string.IsNullOrEmpty(value)) return option;

            var parts = value.Split('|');
            option.Text = parts[0].Trim();

            for (int i = 1; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                if (part.Length == 0) continue;
                if (part.StartsWith("if:", StringComparison.OrdinalIgnoreCase))
                    option.Condition = part.Substring(3);
                else
                    option.Effects.Add(part);
            }
            return option;
        }
    }
}
