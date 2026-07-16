#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace ES
{
    public partial class ESSoTableDataRule
    {
        private static List<List<string>> ReadTableFileAuto(string path)
        {
            string extension = Path.GetExtension(path)?.ToLowerInvariant();
            if (extension == ".csv")
                return ReadCsv(path);
            if (extension == ".xlsx")
                return ReadXlsx(path);

            throw new NotSupportedException("Unsupported table type: " + extension);
        }

        private static void WriteCsv(string path, List<List<string>> table)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            var builder = new StringBuilder(4096);
            for (int i = 0; i < table.Count; i++)
            {
                List<string> row = table[i];
                for (int j = 0; j < row.Count; j++)
                {
                    if (j > 0)
                        builder.Append(',');
                    builder.Append(EscapeCsv(row[j]));
                }

                builder.AppendLine();
            }

            File.WriteAllText(path, builder.ToString(), new UTF8Encoding(true));
        }

        private static List<List<string>> ReadCsv(string path)
        {
            string text = File.ReadAllText(path, Encoding.UTF8);
            var table = new List<List<string>>();
            var row = new List<string>();
            var cell = new StringBuilder();
            bool inQuote = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (inQuote)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            cell.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuote = false;
                        }
                    }
                    else
                    {
                        cell.Append(c);
                    }

                    continue;
                }

                if (c == '"')
                {
                    inQuote = true;
                }
                else if (c == ',')
                {
                    row.Add(cell.ToString());
                    cell.Length = 0;
                }
                else if (c == '\r' || c == '\n')
                {
                    if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                        i++;

                    row.Add(cell.ToString());
                    cell.Length = 0;
                    table.Add(row);
                    row = new List<string>();
                }
                else
                {
                    cell.Append(c);
                }
            }

            if (cell.Length > 0 || row.Count > 0)
            {
                row.Add(cell.ToString());
                table.Add(row);
            }

            return table;
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needQuote = value.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
            if (!needQuote)
                return value;

            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private static void WriteXlsx(string path, List<List<string>> table, string sheet)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path))
                File.Delete(path);

            using (FileStream stream = File.Create(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                AddZipText(archive, "[Content_Types].xml", BuildContentTypesXml());
                AddZipText(archive, "_rels/.rels", BuildRootRelsXml());
                AddZipText(archive, "xl/workbook.xml", BuildWorkbookXml(sheet));
                AddZipText(archive, "xl/_rels/workbook.xml.rels", BuildWorkbookRelsXml());
                AddZipText(archive, "xl/styles.xml", BuildStylesXml());
                AddZipText(archive, "xl/worksheets/sheet1.xml", BuildWorksheetXml(table));
                AddZipText(archive, "xl/worksheets/_rels/sheet1.xml.rels", BuildWorksheetRelsXml());
                AddZipText(archive, "xl/comments1.xml", BuildCommentsXml(table));
                AddZipText(archive, "xl/drawings/vmlDrawing1.vml", BuildVmlDrawingXml(table));
            }
        }

        private static List<List<string>> ReadXlsx(string path)
        {
            using (FileStream stream = File.OpenRead(path))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                List<string> sharedStrings = ReadSharedStrings(archive);
                ZipArchiveEntry sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
                if (sheetEntry == null)
                    return new List<List<string>>();

                using (Stream sheetStream = sheetEntry.Open())
                    return ReadWorksheet(sheetStream, sharedStrings);
            }
        }

        private static List<string> ReadSharedStrings(ZipArchive archive)
        {
            var values = new List<string>();
            ZipArchiveEntry entry = archive.GetEntry("xl/sharedStrings.xml");
            if (entry == null)
                return values;

            var doc = new XmlDocument();
            using (Stream stream = entry.Open())
                doc.Load(stream);

            XmlNodeList items = doc.GetElementsByTagName("si", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            for (int i = 0; i < items.Count; i++)
                values.Add(GetCombinedText(items[i]));

            return values;
        }

        private static List<List<string>> ReadWorksheet(Stream sheetStream, List<string> sharedStrings)
        {
            var doc = new XmlDocument();
            doc.Load(sheetStream);

            var table = new List<List<string>>();
            XmlNodeList rows = doc.GetElementsByTagName("row", "http://schemas.openxmlformats.org/spreadsheetml/2006/main");
            for (int i = 0; i < rows.Count; i++)
            {
                var row = new List<string>();
                foreach (XmlNode cellNode in rows[i].ChildNodes)
                {
                    if (cellNode.LocalName != "c")
                        continue;

                    int columnIndex = GetCellColumnIndex(cellNode.Attributes?["r"]?.Value);
                    while (row.Count < columnIndex)
                        row.Add(string.Empty);

                    row.Add(ReadCellValue(cellNode, sharedStrings));
                }

                table.Add(row);
            }

            return table;
        }

        private static string ReadCellValue(XmlNode cellNode, List<string> sharedStrings)
        {
            string type = cellNode.Attributes?["t"]?.Value;
            if (type == "inlineStr")
            {
                XmlNode inlineNode = FindFirstChild(cellNode, "is");
                return inlineNode != null ? GetCombinedText(inlineNode) : string.Empty;
            }

            XmlNode valueNode = FindFirstChild(cellNode, "v");
            string raw = valueNode?.InnerText ?? string.Empty;
            if (type == "s" && int.TryParse(raw, out int sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Count)
                return sharedStrings[sharedIndex];

            return raw;
        }

        private static XmlNode FindFirstChild(XmlNode node, string localName)
        {
            if (node == null)
                return null;

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.LocalName == localName)
                    return child;
            }

            return null;
        }

        private static string GetCombinedText(XmlNode node)
        {
            if (node == null)
                return string.Empty;

            var builder = new StringBuilder();
            AppendTextNodes(node, builder);
            return builder.ToString();
        }

        private static void AppendTextNodes(XmlNode node, StringBuilder builder)
        {
            if (node.LocalName == "t")
                builder.Append(node.InnerText);

            foreach (XmlNode child in node.ChildNodes)
                AppendTextNodes(child, builder);
        }

        private static int GetCellColumnIndex(string cellRef)
        {
            if (string.IsNullOrEmpty(cellRef))
                return 0;

            int column = 0;
            for (int i = 0; i < cellRef.Length; i++)
            {
                char c = cellRef[i];
                if (c < 'A' || c > 'Z')
                    break;

                column = column * 26 + (c - 'A' + 1);
            }

            return Math.Max(0, column - 1);
        }

        private static void AddZipText(ZipArchive archive, string path, string content)
        {
            ZipArchiveEntry entry = archive.CreateEntry(path);
            using (Stream entryStream = entry.Open())
            using (var writer = new StreamWriter(entryStream, new UTF8Encoding(false)))
                writer.Write(content);
        }

        private static string BuildContentTypesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\">" +
                   "<Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/>" +
                   "<Default Extension=\"xml\" ContentType=\"application/xml\"/>" +
                   "<Default Extension=\"vml\" ContentType=\"application/vnd.openxmlformats-officedocument.vmlDrawing\"/>" +
                   "<Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/>" +
                   "<Override PartName=\"/xl/worksheets/sheet1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>" +
                   "<Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" +
                   "<Override PartName=\"/xl/comments1.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.comments+xml\"/>" +
                   "</Types>";
        }

        private static string BuildRootRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildWorkbookXml(string sheet)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">" +
                   "<sheets><sheet name=\"" + EscapeXml(sheet) + "\" sheetId=\"1\" r:id=\"rId1\"/></sheets>" +
                   "</workbook>";
        }

        private static string BuildWorkbookRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet1.xml\"/>" +
                   "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>" +
                   "</Relationships>";
        }

        private static string BuildWorksheetRelsXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" +
                   "<Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/comments\" Target=\"../comments1.xml\"/>" +
                   "<Relationship Id=\"rId2\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/vmlDrawing\" Target=\"../drawings/vmlDrawing1.vml\"/>" +
                   "</Relationships>";
        }

        private static string BuildStylesXml()
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                   "<styleSheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                   "<fonts count=\"2\"><font><sz val=\"11\"/><name val=\"Calibri\"/></font><font><b/><sz val=\"11\"/><name val=\"Calibri\"/></font></fonts>" +
                   "<fills count=\"5\"><fill><patternFill patternType=\"none\"/></fill><fill><patternFill patternType=\"gray125\"/></fill>" +
                   "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFD9EAF7\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                   "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFFFF2CC\"/><bgColor indexed=\"64\"/></patternFill></fill>" +
                   "<fill><patternFill patternType=\"solid\"><fgColor rgb=\"FFE7E6E6\"/><bgColor indexed=\"64\"/></patternFill></fill></fills>" +
                   "<borders count=\"1\"><border><left/><right/><top/><bottom/><diagonal/></border></borders>" +
                   "<cellStyleXfs count=\"1\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\"/></cellStyleXfs>" +
                   "<cellXfs count=\"4\"><xf numFmtId=\"0\" fontId=\"0\" fillId=\"0\" borderId=\"0\" xfId=\"0\"/>" +
                   "<xf numFmtId=\"0\" fontId=\"1\" fillId=\"2\" borderId=\"0\" xfId=\"0\" applyFont=\"1\" applyFill=\"1\"/>" +
                   "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"3\" borderId=\"0\" xfId=\"0\" applyFill=\"1\"/>" +
                   "<xf numFmtId=\"0\" fontId=\"0\" fillId=\"4\" borderId=\"0\" xfId=\"0\" applyFill=\"1\"/></cellXfs>" +
                   "<cellStyles count=\"1\"><cellStyle name=\"Normal\" xfId=\"0\" builtinId=\"0\"/></cellStyles>" +
                   "</styleSheet>";
        }

        private static string BuildWorksheetXml(List<List<string>> table)
        {
            var builder = new StringBuilder(8192);
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\">");
            builder.Append("<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"4\" topLeftCell=\"A5\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews>");
            builder.Append("<sheetData>");

            for (int rowIndex = 0; rowIndex < table.Count; rowIndex++)
            {
                builder.Append("<row r=\"").Append(rowIndex + 1).Append("\">");
                List<string> row = table[rowIndex];
                for (int columnIndex = 0; columnIndex < row.Count; columnIndex++)
                {
                    string cellRef = GetExcelColumnName(columnIndex + 1) + (rowIndex + 1).ToString(CultureInfo.InvariantCulture);
                    builder.Append("<c r=\"").Append(cellRef).Append("\"");
                    int styleIndex = GetXlsxStyleIndex(table, rowIndex, columnIndex);
                    if (styleIndex > 0)
                        builder.Append(" s=\"").Append(styleIndex).Append("\"");
                    builder.Append(" t=\"inlineStr\"><is><t>");
                    builder.Append(EscapeXml(row[columnIndex]));
                    builder.Append("</t></is></c>");
                }
                builder.Append("</row>");
            }

            builder.Append("</sheetData>");
            builder.Append(BuildDataValidationsXml(table));
            builder.Append("<legacyDrawing r:id=\"rId2\"/>");
            builder.Append("</worksheet>");
            return builder.ToString();
        }

        private static int GetXlsxStyleIndex(List<List<string>> table, int rowIndex, int columnIndex)
        {
            if (rowIndex < 4)
                return 1;
            if (columnIndex == 0)
                return 2;

            string columnName = table != null && table.Count > 0 ? GetCell(table[0], columnIndex) : string.Empty;
            string normalized = (columnName ?? string.Empty).Replace("_", string.Empty).Replace("-", string.Empty).Trim();
            if (normalized.Equals("id", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("key", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("itemid", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("rowkey", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("keyname", StringComparison.OrdinalIgnoreCase))
                return 2;

            string group = table != null && table.Count > 2 ? GetCell(table[2], columnIndex) : string.Empty;
            if (group.IndexOf("ignore", StringComparison.OrdinalIgnoreCase) >= 0
                || group.IndexOf("locked", StringComparison.OrdinalIgnoreCase) >= 0)
                return 3;

            return 0;
        }

        private static string BuildDataValidationsXml(List<List<string>> table)
        {
            int assertRowIndex = FindAssertRowIndex(table);
            int validationCount = assertRowIndex >= 0 ? 2 : 1;
            int dataStartRow = GetDataStartRowIndex(table) + 1;
            var builder = new StringBuilder();
            builder.Append("<dataValidations count=\"").Append(validationCount).Append("\">");
            builder.Append("<dataValidation type=\"list\" allowBlank=\"1\" showErrorMessage=\"1\" sqref=\"A").Append(dataStartRow).Append(":A1048576\"><formula1>\"skip,ignore,required,patch,replace,owner,delete,debug,debug:patch,debug:delete,comment:\"</formula1></dataValidation>");
            if (assertRowIndex >= 0 && table != null && table.Count > 0 && table[0].Count > 1)
            {
                string lastColumn = GetExcelColumnName(table[0].Count);
                int row = assertRowIndex + 1;
                builder.Append("<dataValidation type=\"list\" allowBlank=\"1\" showErrorMessage=\"1\" sqref=\"B")
                    .Append(row).Append(":").Append(lastColumn).Append(row)
                    .Append("\"><formula1>\"required,unique,required;unique,json,asset,range:1..99,regex:^[a-z0-9_]+$\"</formula1></dataValidation>");
            }
            builder.Append("</dataValidations>");
            return builder.ToString();
        }

        private static int FindAssertRowIndex(List<List<string>> table)
        {
            if (table == null)
                return -1;
            for (int i = 0; i < table.Count; i++)
            {
                if (string.Equals(GetCell(table[i], 0), "##assert", StringComparison.OrdinalIgnoreCase))
                    return i;
            }
            return -1;
        }

        private static string BuildCommentsXml(List<List<string>> table)
        {
            var builder = new StringBuilder();
            builder.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
            builder.Append("<comments xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><authors><author>ESSoTable</author></authors><commentList>");
            AppendXlsxComment(builder, "A1", "行指令列：空=正常导入；skip/ignore/disabled=跳过；comment:=备注；required=必填；patch=只写非空；replace=强制覆盖；owner=切换/写 SO 本体；delete=删除；debug=打印本行追踪；debug:patch/debug:delete=按真实指令执行并打印。");
            if (table != null && table.Count > 0)
            {
                int max = Math.Min(table[0].Count, 80);
                for (int columnIndex = 1; columnIndex < max; columnIndex++)
                {
                    string cellRef = GetExcelColumnName(columnIndex + 1) + "1";
                    string text = "字段：" + GetCell(table[0], columnIndex)
                        + "\n类型：" + (table.Count > 1 ? GetCell(table[1], columnIndex) : string.Empty)
                        + "\n分组：" + (table.Count > 2 ? GetCell(table[2], columnIndex) : string.Empty)
                        + "\n说明：" + (table.Count > 3 ? GetCell(table[3], columnIndex) : string.Empty);
                    AppendXlsxComment(builder, cellRef, text);
                }
            }
            builder.Append("</commentList></comments>");
            return builder.ToString();
        }

        private static void AppendXlsxComment(StringBuilder builder, string cellRef, string text)
        {
            builder.Append("<comment ref=\"").Append(cellRef).Append("\" authorId=\"0\"><text><r><t>");
            builder.Append(EscapeXml(text));
            builder.Append("</t></r></text></comment>");
        }

        private static string BuildVmlDrawingXml(List<List<string>> table)
        {
            int count = table != null && table.Count > 0 ? Math.Min(table[0].Count, 80) : 1;
            var builder = new StringBuilder();
            builder.Append("<xml xmlns:v=\"urn:schemas-microsoft-com:vml\" xmlns:o=\"urn:schemas-microsoft-com:office:office\" xmlns:x=\"urn:schemas-microsoft-com:office:excel\">");
            builder.Append("<o:shapelayout v:ext=\"edit\"><o:idmap v:ext=\"edit\" data=\"1\"/></o:shapelayout>");
            builder.Append("<v:shapetype id=\"_x0000_t202\" coordsize=\"21600,21600\" o:spt=\"202\" path=\"m,l,21600r21600,l21600,xe\"><v:stroke joinstyle=\"miter\"/><v:path gradientshapeok=\"t\" o:connecttype=\"rect\"/></v:shapetype>");
            for (int i = 0; i < count; i++)
            {
                builder.Append("<v:shape id=\"_x0000_s").Append(1025 + i).Append("\" type=\"#_x0000_t202\" style=\"position:absolute;margin-left:80pt;margin-top:5pt;width:160pt;height:80pt;z-index:")
                    .Append(i + 1).Append(";visibility:hidden\" fillcolor=\"#ffffe1\" o:insetmode=\"auto\"><v:fill color2=\"#ffffe1\"/><v:shadow on=\"t\" color=\"black\" obscured=\"t\"/><v:path o:connecttype=\"none\"/><v:textbox style=\"mso-direction-alt:auto\"><div style=\"text-align:left\"></div></v:textbox><x:ClientData ObjectType=\"Note\"><x:MoveWithCells/><x:SizeWithCells/><x:Anchor>1, 15, 0, 2, 3, 15, 4, 16</x:Anchor><x:AutoFill>False</x:AutoFill><x:Row>0</x:Row><x:Column>")
                    .Append(i).Append("</x:Column></x:ClientData></v:shape>");
            }
            builder.Append("</xml>");
            return builder.ToString();
        }

        private static string GetExcelColumnName(int columnNumber)
        {
            var columnName = new StringBuilder();
            while (columnNumber > 0)
            {
                int modulo = (columnNumber - 1) % 26;
                columnName.Insert(0, (char)('A' + modulo));
                columnNumber = (columnNumber - modulo) / 26;
            }

            return columnName.ToString();
        }

        private static string EscapeXml(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("&", "&amp;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;")
                .Replace("\"", "&quot;")
                .Replace("'", "&apos;");
        }
    }
}
#endif
