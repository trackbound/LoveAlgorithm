#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml;

namespace LoveAlgo.NarrativeEditor
{
    /// <summary>
    /// 의존성 없는 최소 xlsx 리더. (zip + XML 직접 파싱)
    /// 셀 값은 문자열로 반환. 수식/스타일/병합 무시.
    /// </summary>
    public static class MiniXlsx
    {
        public class Sheet
        {
            public string Name;
            public List<List<string>> Rows = new();
        }

        public static Dictionary<string, Sheet> Read(string xlsxPath)
        {
            var result = new Dictionary<string, Sheet>();
            using var fs = File.OpenRead(xlsxPath);
            using var zip = new ZipArchive(fs, ZipArchiveMode.Read);

            // 1) sharedStrings
            var shared = ReadSharedStrings(zip);

            // 2) workbook.xml → sheetId/name/rId
            var sheetMetas = ReadWorkbook(zip);

            // 3) rels → rId → target path
            var rels = ReadWorkbookRels(zip);

            // 4) 각 시트 파싱
            foreach (var meta in sheetMetas)
            {
                if (!rels.TryGetValue(meta.rId, out var target)) continue;
                var sheetEntry = zip.GetEntry("xl/" + target);
                if (sheetEntry == null) continue;

                var sheet = new Sheet { Name = meta.name };
                ReadSheet(sheetEntry, shared, sheet.Rows);
                result[meta.name] = sheet;
            }
            return result;
        }

        // ─── sharedStrings.xml ─────────────────────────────
        static List<string> ReadSharedStrings(ZipArchive zip)
        {
            var list = new List<string>();
            var entry = zip.GetEntry("xl/sharedStrings.xml");
            if (entry == null) return list;
            using var stream = entry.Open();
            var doc = new XmlDocument();
            doc.Load(stream);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");

            var siNodes = doc.SelectNodes("//s:si", ns);
            if (siNodes == null) return list;
            foreach (XmlNode si in siNodes)
            {
                // <si><t>...</t></si> 또는 <si><r><t>...</t></r>...</si>
                var sb = new System.Text.StringBuilder();
                var tNodes = si.SelectNodes(".//s:t", ns);
                if (tNodes != null)
                {
                    foreach (XmlNode t in tNodes) sb.Append(t.InnerText);
                }
                list.Add(sb.ToString());
            }
            return list;
        }

        // ─── workbook.xml ──────────────────────────────────
        struct SheetMeta { public string name; public string rId; }
        static List<SheetMeta> ReadWorkbook(ZipArchive zip)
        {
            var list = new List<SheetMeta>();
            var entry = zip.GetEntry("xl/workbook.xml");
            if (entry == null) return list;
            using var stream = entry.Open();
            var doc = new XmlDocument();
            doc.Load(stream);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("s", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            ns.AddNamespace("r", "http://schemas.openxmlformats.org/officeDocument/2006/relationships");

            var sheets = doc.SelectNodes("//s:sheet", ns);
            if (sheets == null) return list;
            foreach (XmlNode sh in sheets)
            {
                list.Add(new SheetMeta
                {
                    name = sh.Attributes?["name"]?.Value,
                    rId = sh.Attributes?["r:id"]?.Value ?? sh.Attributes?["id", "http://schemas.openxmlformats.org/officeDocument/2006/relationships"]?.Value
                });
            }
            return list;
        }

        // ─── _rels/workbook.xml.rels ───────────────────────
        static Dictionary<string, string> ReadWorkbookRels(ZipArchive zip)
        {
            var map = new Dictionary<string, string>();
            var entry = zip.GetEntry("xl/_rels/workbook.xml.rels");
            if (entry == null) return map;
            using var stream = entry.Open();
            var doc = new XmlDocument();
            doc.Load(stream);
            var ns = new XmlNamespaceManager(doc.NameTable);
            ns.AddNamespace("r", "http://schemas.openxmlformats.org/package/2006/relationships");
            var rels = doc.SelectNodes("//r:Relationship", ns);
            if (rels == null) return map;
            foreach (XmlNode rel in rels)
            {
                var id = rel.Attributes?["Id"]?.Value;
                var target = rel.Attributes?["Target"]?.Value;
                if (!string.IsNullOrEmpty(id) && !string.IsNullOrEmpty(target))
                    map[id] = target;
            }
            return map;
        }

        // ─── sheetN.xml ────────────────────────────────────
        static void ReadSheet(ZipArchiveEntry entry, List<string> shared, List<List<string>> rows)
        {
            using var stream = entry.Open();
            using var reader = XmlReader.Create(stream, new XmlReaderSettings { IgnoreWhitespace = false });

            List<string> currentRow = null;
            int currentCol = -1;
            string cellType = null;
            string cellValue = null;
            bool inV = false;
            bool inIsT = false;

            while (reader.Read())
            {
                if (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.LocalName)
                    {
                        case "row":
                            currentRow = new List<string>();
                            break;
                        case "c":
                            currentCol = ColumnFromRef(reader.GetAttribute("r"));
                            cellType = reader.GetAttribute("t");
                            cellValue = null;
                            // Pad missing columns
                            while (currentRow.Count < currentCol) currentRow.Add("");
                            break;
                        case "v":
                            inV = true;
                            break;
                        case "t" when reader.NamespaceURI == "http://schemas.openxmlformats.org/spreadsheetml/2006/main":
                            // <is><t> inline string
                            inIsT = true;
                            break;
                    }
                }
                else if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.SignificantWhitespace || reader.NodeType == XmlNodeType.Whitespace)
                {
                    if (inV) cellValue = (cellValue ?? "") + reader.Value;
                    else if (inIsT) cellValue = (cellValue ?? "") + reader.Value;
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    switch (reader.LocalName)
                    {
                        case "row":
                            rows.Add(currentRow);
                            currentRow = null;
                            break;
                        case "c":
                            string finalVal = cellValue ?? "";
                            if (cellType == "s" && int.TryParse(finalVal, out int idx) && idx >= 0 && idx < shared.Count)
                                finalVal = shared[idx];
                            else if (cellType == "b")
                                finalVal = finalVal == "1" ? "True" : "False";
                            if (currentRow != null) currentRow.Add(finalVal);
                            break;
                        case "v":
                            inV = false;
                            break;
                        case "t" when reader.NamespaceURI == "http://schemas.openxmlformats.org/spreadsheetml/2006/main":
                            inIsT = false;
                            break;
                    }
                }
            }
        }

        // "B5" → 1 (0-based column index)
        static int ColumnFromRef(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef)) return -1;
            int col = 0;
            foreach (var c in cellRef)
            {
                if (c >= 'A' && c <= 'Z') col = col * 26 + (c - 'A' + 1);
                else if (c >= 'a' && c <= 'z') col = col * 26 + (c - 'a' + 1);
                else break;
            }
            return col - 1;
        }
    }
}
#endif
