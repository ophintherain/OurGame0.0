using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Xml.Linq;

namespace Spotlight.Exploration.StoryEditor
{
    /// <summary>一行 Excel 数据。Cells[0] 对应 A 列。</summary>
    internal sealed class XlsxRow
    {
        public int RowNumber;
        public string[] Cells = Array.Empty<string>();

        public string Get(int index)
        {
            return index >= 0 && index < Cells.Length ? Cells[index] : string.Empty;
        }
    }

    /// <summary>读取到的第一个工作表。</summary>
    internal sealed class XlsxTable
    {
        public readonly List<XlsxRow> Rows = new List<XlsxRow>();
    }

    /// <summary>
    /// 轻量 xlsx 读取器：直接解压 xlsx 并解析第一个工作表的 XML，不依赖第三方库。
    /// 只处理文本、共享字符串和普通数值，够剧情表使用。
    /// </summary>
    internal static class XlsxTableReader
    {
        private const string FallbackSheetPath = "xl/worksheets/sheet1.xml";
        private static readonly XNamespace MainNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        private static readonly XNamespace RelNs = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        private static readonly XNamespace PackageRelNs = "http://schemas.openxmlformats.org/package/2006/relationships";

        public static XlsxTable ReadFirstWorksheet(string path)
        {
            using (FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                string sheetPath = ResolveFirstSheetPath(archive);
                List<string> sharedStrings = ReadSharedStrings(archive);
                XDocument sheet = LoadXml(archive, sheetPath);

                XlsxTable table = new XlsxTable();
                XElement sheetData = sheet.Descendants(MainNs + "sheetData").FirstOrDefault();
                if (sheetData == null)
                {
                    return table;
                }

                int fallbackRow = 0;
                foreach (XElement rowElement in sheetData.Elements(MainNs + "row"))
                {
                    int rowNumber = ParseInt((string)rowElement.Attribute("r"), fallbackRow + 1);
                    fallbackRow = rowNumber;

                    Dictionary<int, string> values = new Dictionary<int, string>();
                    int fallbackColumn = 0;

                    foreach (XElement cell in rowElement.Elements(MainNs + "c"))
                    {
                        string reference = (string)cell.Attribute("r");
                        int column = reference != null ? ColumnIndex(reference) : fallbackColumn;
                        fallbackColumn = column + 1;

                        string value = ReadCellValue(cell, sharedStrings);
                        if (!string.IsNullOrEmpty(value))
                        {
                            values[column] = value;
                        }
                    }

                    int maxColumn = values.Count == 0 ? 0 : values.Keys.Max() + 1;
                    string[] cells = new string[maxColumn];
                    for (int index = 0; index < cells.Length; index++)
                    {
                        cells[index] = string.Empty;
                    }

                    foreach (KeyValuePair<int, string> pair in values)
                    {
                        cells[pair.Key] = pair.Value;
                    }

                    table.Rows.Add(new XlsxRow { RowNumber = rowNumber, Cells = cells });
                }

                return table;
            }
        }

        private static string ResolveFirstSheetPath(ZipArchive archive)
        {
            try
            {
                XDocument workbook = LoadXml(archive, "xl/workbook.xml");
                XElement firstSheet = workbook.Descendants(MainNs + "sheet").FirstOrDefault();
                if (firstSheet == null)
                {
                    return FallbackSheetPath;
                }

                string relationshipId = (string)firstSheet.Attribute(RelNs + "id");
                if (string.IsNullOrEmpty(relationshipId))
                {
                    return FallbackSheetPath;
                }

                XDocument relationships = LoadXml(archive, "xl/_rels/workbook.xml.rels");
                XElement relationship = relationships
                    .Descendants(PackageRelNs + "Relationship")
                    .FirstOrDefault(element => (string)element.Attribute("Id") == relationshipId);

                string target = (string)relationship?.Attribute("Target");
                if (string.IsNullOrEmpty(target))
                {
                    return FallbackSheetPath;
                }

                target = target.Replace('\\', '/');
                if (target.StartsWith("/"))
                {
                    return target.TrimStart('/');
                }

                return target.StartsWith("xl/") ? target : "xl/" + target;
            }
            catch
            {
                return FallbackSheetPath;
            }
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
            {
                return new List<string>();
            }

            XDocument document = LoadXml(archive, "xl/sharedStrings.xml");
            return document
                .Descendants(MainNs + "si")
                .Select(item => string.Concat(item.Descendants(MainNs + "t").Select(text => text.Value)))
                .ToList();
        }

        private static XDocument LoadXml(ZipArchive archive, string entryPath)
        {
            ZipArchiveEntry entry = archive.GetEntry(entryPath);
            if (entry == null)
            {
                throw new FileNotFoundException($"xlsx 内找不到 {entryPath}");
            }

            using (Stream stream = entry.Open())
            {
                return XDocument.Load(stream);
            }
        }

        private static string ReadCellValue(XElement cell, List<string> sharedStrings)
        {
            string type = (string)cell.Attribute("t");

            if (type == "inlineStr")
            {
                XElement inline = cell.Element(MainNs + "is");
                if (inline != null)
                {
                    return string.Concat(inline.Descendants(MainNs + "t").Select(text => text.Value));
                }
            }

            string raw = cell.Element(MainNs + "v")?.Value ?? string.Empty;

            if (type == "s" && int.TryParse(raw, out int sharedIndex))
            {
                return sharedIndex >= 0 && sharedIndex < sharedStrings.Count
                    ? sharedStrings[sharedIndex]
                    : string.Empty;
            }

            if (type == "b")
            {
                return raw == "1" ? "TRUE" : "FALSE";
            }

            return raw;
        }

        private static int ColumnIndex(string reference)
        {
            int index = 0;
            for (int position = 0; position < reference.Length; position++)
            {
                char character = reference[position];
                if (character >= 'A' && character <= 'Z')
                {
                    index = index * 26 + (character - 'A' + 1);
                }
                else if (character >= 'a' && character <= 'z')
                {
                    index = index * 26 + (character - 'a' + 1);
                }
                else
                {
                    break;
                }
            }

            return Math.Max(0, index - 1);
        }

        private static int ParseInt(string value, int fallback)
        {
            return int.TryParse(value, out int result) ? result : fallback;
        }
    }
}
