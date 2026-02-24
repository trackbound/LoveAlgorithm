using System.Collections.Generic;
using System.Text;

namespace LoveAlgo.Story
{
    /// <summary>
    /// CSV 파싱 공통 유틸리티 (따옴표 내부 개행/쉼표/이스케이프 따옴표 지원)
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

        /// <summary>
        /// CSV 전체 텍스트를 레코드 단위로 분리한다. (따옴표 밖 개행만 레코드 구분)
        /// </summary>
        public static List<CsvRecord> SplitRecords(string csv)
        {
            var records = new List<CsvRecord>();
            if (string.IsNullOrEmpty(csv)) return records;

            var current = new StringBuilder();
            bool inQuotes = false;
            bool fieldStart = true;
            int currentLine = 1;
            int recordStartLine = 1;

            for (int i = 0; i < csv.Length; i++)
            {
                char c = csv[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < csv.Length && csv[i + 1] == '"')
                    {
                        current.Append('"');
                        current.Append('"');
                        i++;
                    }
                    else if (inQuotes)
                    {
                        bool canClose = i + 1 >= csv.Length
                            || csv[i + 1] == ','
                            || csv[i + 1] == '\r'
                            || csv[i + 1] == '\n';

                        if (canClose)
                        {
                            inQuotes = false;
                            current.Append(c);
                            fieldStart = false;
                        }
                        else
                        {
                            current.Append(c);
                        }
                    }
                    else if (fieldStart)
                    {
                        inQuotes = true;
                        current.Append(c);
                    }
                    else
                    {
                        current.Append(c);
                        fieldStart = false;
                    }
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
            var current = new StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];

                if (c == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else if (inQuotes)
                    {
                        bool canClose = i + 1 >= line.Length || line[i + 1] == ',';
                        if (canClose)
                        {
                            inQuotes = false;
                            fieldStart = false;
                        }
                        else
                        {
                            current.Append(c);
                        }
                    }
                    else if (fieldStart)
                    {
                        inQuotes = true;
                    }
                    else
                    {
                        current.Append(c);
                        fieldStart = false;
                    }
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
