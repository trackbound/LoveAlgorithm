using System.Collections.Generic;
using System.Text;

namespace LoveAlgo.Core
{
    /// <summary>
    /// CSV 파싱 공통 유틸리티 (따옴표 내부 개행/쉼표/이스케이프 따옴표 지원).
    /// 스토리·메신저 등 CSV 포맷 피처들이 공유하는 인프라라 Core 소속(피처 간 직접 참조 금지, ADR-011).
    /// </summary>
    public static class CsvUtility
    {
        public readonly struct CsvRecord
        {
            public CsvRecord(string text, int startLine)
            {
                Text = text;
                StartLine = startLine;
            }

            public string Text { get; }
            public int StartLine { get; }
        }

        // 공유 StringBuilder — 재할당 방지 (SplitRecords/SplitCsv는 동시 호출되지 않음)
        [System.ThreadStatic] static StringBuilder _sb;
        static StringBuilder GetBuilder()
        {
            var sb = _sb ??= new StringBuilder(256);
            sb.Clear();
            return sb;
        }

        /// <summary>
        /// 따옴표 문자 처리 (SplitRecords와 SplitCsv 공통)
        /// isRecordMode=true 면 따옴표 문자 자체도 output에 유지 (레코드 분리 시 원본 보존용)
        /// </summary>
        static void HandleQuoteChar(
            string text, ref int i, StringBuilder current,
            ref bool inQuotes, ref bool fieldStart,
            bool isRecordMode)
        {
            // 따옴표 안에서 따옴표 두 개("")는 이스케이프
            if (inQuotes && i + 1 < text.Length && text[i + 1] == '"')
            {
                if (isRecordMode)
                {
                    current.Append('"');
                    current.Append('"');
                }
                else
                {
                    current.Append('"');
                }
                i++;
                return;
            }

            if (inQuotes)
            {
                bool canClose;
                if (isRecordMode)
                {
                    canClose = i + 1 >= text.Length
                        || text[i + 1] == ','
                        || text[i + 1] == '\r'
                        || text[i + 1] == '\n';
                }
                else
                {
                    canClose = i + 1 >= text.Length || text[i + 1] == ',';
                }

                if (canClose)
                {
                    inQuotes = false;
                    if (isRecordMode) current.Append('"');
                    fieldStart = false;
                }
                else
                {
                    current.Append('"');
                }
                return;
            }

            if (fieldStart)
            {
                inQuotes = true;
                if (isRecordMode) current.Append('"');
                return;
            }

            current.Append('"');
            fieldStart = false;
        }

        /// <summary>
        /// CSV 전체 텍스트를 레코드 단위로 분리한다. (따옴표 밖 개행만 레코드 구분)
        /// </summary>
        public static List<CsvRecord> SplitRecords(string csv)
        {
            var records = new List<CsvRecord>();
            if (string.IsNullOrEmpty(csv)) return records;

            var current = GetBuilder();
            bool inQuotes = false;
            bool fieldStart = true;
            int currentLine = 1;
            int recordStartLine = 1;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (c == '"')
                {
                    HandleQuoteChar(csv, ref i, current, ref inQuotes, ref fieldStart, isRecordMode: true);
                    continue;
                }

                if (c == '\r')
                {
                    bool isCrLf = i + 1 < csv.Length && csv[i + 1] == '\n';

                    if (inQuotes)
                    {
                        current.Append('\r');
                        if (isCrLf)
                        {
                            current.Append('\n');
                            i++;
                        }
                    }
                    else
                    {
                        records.Add(new CsvRecord(current.ToString(), recordStartLine));
                        current.Clear();
                        if (isCrLf) i++;
                        recordStartLine = currentLine + 1;
                        fieldStart = true;
                    }

                    currentLine++;
                    continue;
                }

                if (c == '\n')
                {
                    if (inQuotes)
                    {
                        current.Append('\n');
                    }
                    else
                    {
                        records.Add(new CsvRecord(current.ToString(), recordStartLine));
                        current.Clear();
                        recordStartLine = currentLine + 1;
                        fieldStart = true;
                    }

                    currentLine++;
                    continue;
                }

                current.Append(c);
                if (!inQuotes)
                {
                    fieldStart = c == ',';
                }
            }

            records.Add(new CsvRecord(current.ToString(), recordStartLine));
            return records;
        }

        /// <summary>
        /// CSV 한 레코드를 컬럼으로 분리한다. (따옴표 내부 쉼표/이스케이프 따옴표 지원)
        /// </summary>
        public static string[] SplitCsv(string line)
        {
            var result = new List<string>();
            bool inQuotes = false;
            bool fieldStart = true;
            var current = GetBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    HandleQuoteChar(line, ref i, current, ref inQuotes, ref fieldStart, isRecordMode: false);
                }
                else if (c == ',' && !inQuotes)
                {
                    result.Add(current.ToString());
                    current.Clear();
                    fieldStart = true;
                }
                else
                {
                    current.Append(c);
                    fieldStart = false;
                }
            }

            result.Add(current.ToString());
            return result.ToArray();
        }
    }
}
