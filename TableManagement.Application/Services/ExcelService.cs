// TableManagement.Application/Services/ExcelService.cs
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text;

namespace TableManagement.Application.Services
{
    public class ExcelService : IExcelService
    {
        private readonly ITableService _tableService;
        private readonly ILogger<ExcelService> _logger;

        public ExcelService(ITableService tableService, ILogger<ExcelService> logger)
        {
            _tableService = tableService;
            _logger = logger;
        }

        public async Task<FileResult> ExportTableToExcelAsync(int tableId, int userId, bool includeHeaders = true)
        {
            try
            {
                _logger.LogInformation("Excel export başlatıldı - Tablo ID: {TableId}, Kullanıcı: {UserId}", tableId, userId);

                // Tablo bilgilerini al (tablo adı ve açıklaması için)
                var tableInfo = await _tableService.GetTableByIdAsync(tableId, userId);
                if (tableInfo == null)
                {
                    throw new ArgumentException($"Tablo bulunamadı. ID: {tableId}");
                }

                // Tablo verilerini al
                var tableData = await _tableService.GetTableDataAsync(tableId, userId);
                if (tableData == null)
                {
                    throw new ArgumentException($"Tablo verileri bulunamadı. ID: {tableId}");
                }

                _logger.LogInformation("Tablo bulundu: {TableName}, Satır sayısı: {RowCount}",
                    tableData.TableName, tableData.Data?.Count ?? 0);

                // Excel dosyasını oluştur
                var excelBytes = await GenerateExcelBytesAsync(tableId, userId, includeHeaders);

                // Dosya adını oluştur
                var fileName = $"{SanitizeFileName(tableInfo.TableName)}_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

                _logger.LogInformation("Excel dosyası oluşturuldu: {FileName}", fileName);

                return new FileContentResult(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel export hatası - Tablo ID: {TableId}", tableId);
                throw;
            }
        }

        public async Task<byte[]> GenerateExcelBytesAsync(int tableId, int userId, bool includeHeaders = true)
        {
            try
            {
                // Tablo bilgilerini al
                var tableInfo = await _tableService.GetTableByIdAsync(tableId, userId);
                if (tableInfo == null)
                {
                    throw new ArgumentException($"Tablo bulunamadı. ID: {tableId}");
                }

                // Tablo verilerini al
                var tableData = await _tableService.GetTableDataAsync(tableId, userId);
                if (tableData == null)
                {
                    throw new ArgumentException($"Tablo verileri bulunamadı. ID: {tableId}");
                }

                using var memoryStream = new MemoryStream();

                // Excel belgesini oluştur
                using (var document = SpreadsheetDocument.Create(memoryStream, SpreadsheetDocumentType.Workbook))
                {
                    // Workbook oluştur
                    var workbookPart = document.AddWorkbookPart();
                    workbookPart.Workbook = new Workbook();

                    // Worksheet oluştur
                    var worksheetPart = workbookPart.AddNewPart<WorksheetPart>();
                    worksheetPart.Worksheet = new Worksheet(new SheetData());

                    // Sheet'i workbook'a ekle
                    var sheets = workbookPart.Workbook.AppendChild(new Sheets());
                    var sheet = new Sheet()
                    {
                        Id = workbookPart.GetIdOfPart(worksheetPart),
                        SheetId = 1,
                        Name = SanitizeSheetName(tableData.TableName)
                    };
                    sheets.Append(sheet);

                    // Stilleri oluştur
                    CreateStylesheet(workbookPart);

                    // Verileri yaz
                    WriteDataToWorksheet(worksheetPart.Worksheet, tableInfo, tableData, includeHeaders);

                    // Kaydet
                    workbookPart.Workbook.Save();
                }

                return memoryStream.ToArray();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excel bytes oluşturma hatası - Tablo ID: {TableId}", tableId);
                throw;
            }
        }

        public async Task<FileResult> ExportTableToCsvAsync(int tableId, int userId, bool includeHeaders = true, string delimiter = ",")
        {
            try
            {
                _logger.LogInformation("CSV export başlatıldı - Tablo ID: {TableId}, Kullanıcı: {UserId}", tableId, userId);

                // Tablo bilgilerini al
                var tableInfo = await _tableService.GetTableByIdAsync(tableId, userId);
                if (tableInfo == null)
                {
                    throw new ArgumentException($"Tablo bulunamadı. ID: {tableId}");
                }

                // Tablo verilerini al
                var tableData = await _tableService.GetTableDataAsync(tableId, userId);
                if (tableData == null)
                {
                    throw new ArgumentException($"Tablo verileri bulunamadı. ID: {tableId}");
                }

                var csv = new StringBuilder();

                // BOM ekle (UTF-8 için Excel uyumluluğu)
                var preamble = Encoding.UTF8.GetPreamble();

                // Başlık satırı
                if (includeHeaders && tableData.Columns?.Any() == true)
                {
                    var orderedColumns = new List<object>();
                    foreach (var column in tableData.Columns)
                    {
                        orderedColumns.Add(column);
                    }
                    orderedColumns = orderedColumns.OrderBy(c => GetPropertyValue(c, "DisplayOrder") ?? 0).ToList();

                    var headers = orderedColumns.Select(c => GetPropertyValue(c, "ColumnName")?.ToString() ?? "");
                    csv.AppendLine(string.Join(delimiter, headers.Select(h => EscapeCsvValue(h, delimiter))));
                }

                // Veri satırları
                if (tableData.Data?.Any() == true)
                {
                    var orderedColumns = new List<object>();
                    foreach (var column in tableData.Columns)
                    {
                        orderedColumns.Add(column);
                    }
                    orderedColumns = orderedColumns.OrderBy(c => GetPropertyValue(c, "DisplayOrder") ?? 0).ToList();

                    foreach (var row in tableData.Data)
                    {
                        var values = orderedColumns.Select(column =>
                        {
                            var columnName = GetPropertyValue(column, "ColumnName")?.ToString() ?? "";
                            // API'den gelen data yapısı: { "Id": 2, "RowIdentifier": 1, "isim": 11, "eposta": "asfcsdc", "yaş": 2 }
                            if (row is Dictionary<string, object> rowDict)
                            {
                                return rowDict.ContainsKey(columnName)
                                    ? rowDict[columnName]?.ToString() ?? ""
                                    : "";
                            }
                            return "";
                        });

                        var escapedValues = values.Select(v => EscapeCsvValue(v, delimiter));
                        csv.AppendLine(string.Join(delimiter, escapedValues));
                    }
                }

                // BOM + CSV içeriği
                var csvBytes = Encoding.UTF8.GetBytes(csv.ToString());
                var result = new byte[preamble.Length + csvBytes.Length];
                Array.Copy(preamble, 0, result, 0, preamble.Length);
                Array.Copy(csvBytes, 0, result, preamble.Length, csvBytes.Length);

                var fileName = $"{SanitizeFileName(tableInfo.TableName)}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";

                _logger.LogInformation("CSV dosyası oluşturuldu: {FileName}", fileName);

                return new FileContentResult(result, "text/csv")
                {
                    FileDownloadName = fileName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CSV export hatası - Tablo ID: {TableId}", tableId);
                throw;
            }
        }

        private void WriteDataToWorksheet(Worksheet worksheet, dynamic tableInfo, dynamic tableData, bool includeHeaders)
        {
            var sheetData = worksheet.GetFirstChild<SheetData>();
            uint rowIndex = 1;

            // Kolon bilgilerini al ve sırala
            var columns = new List<object>();
            if (tableData.Columns != null)
            {
                foreach (var column in tableData.Columns)
                {
                    columns.Add(column);
                }
                // DisplayOrder'a göre sırala
                columns = columns.OrderBy(col => GetPropertyValue(col, "DisplayOrder") ?? 0).ToList();
            }
            var data = tableData.Data ?? new List<object>();

            // Tablo bilgilerini üst satıra ekle (opsiyonel)
            if (!string.IsNullOrEmpty(tableInfo?.Description))
            {
                var descRow = new Row() { RowIndex = rowIndex };
                var descCell = new Cell()
                {
                    CellReference = GetCellReference(1, rowIndex),
                    DataType = CellValues.String,
                    CellValue = new CellValue($"Açıklama: {tableInfo.Description}"),
                    StyleIndex = 1
                };
                descRow.AppendChild(descCell);
                sheetData.AppendChild(descRow);
                rowIndex++;

                // Boş satır ekle
                rowIndex++;
            }

            // Başlık satırını yaz
            if (includeHeaders && columns.Any())
            {
                var headerRow = new Row() { RowIndex = rowIndex };

                for (int i = 0; i < columns.Count; i++)
                {
                    var columnName = GetPropertyValue(columns[i], "ColumnName")?.ToString() ?? $"Kolon_{i + 1}";
                    var cell = new Cell()
                    {
                        CellReference = GetCellReference(i + 1, rowIndex),
                        DataType = CellValues.String,
                        CellValue = new CellValue(columnName),
                        StyleIndex = 1 // Header style
                    };
                    headerRow.AppendChild(cell);
                }

                sheetData.AppendChild(headerRow);
                rowIndex++;
            }

            // Veri satırlarını yaz
            foreach (var rowData in data)
            {
                var row = new Row() { RowIndex = rowIndex };

                for (int i = 0; i < columns.Count; i++)
                {
                    var column = columns[i];
                    var columnName = GetPropertyValue(column, "ColumnName")?.ToString() ?? "";
                    var dataType = GetPropertyValue(column, "DataType") is int dt ? dt : 1;

                    // API'den gelen veri yapısını parse et
                    object value = null;
                    if (rowData is Dictionary<string, object> rowDict)
                    {
                        value = rowDict.ContainsKey(columnName) ? rowDict[columnName] : null;
                    }

                    var cell = new Cell()
                    {
                        CellReference = GetCellReference(i + 1, rowIndex)
                    };

                    if (value != null && !string.IsNullOrEmpty(value.ToString()))
                    {
                        FormatCell(cell, value, dataType);
                    }
                    else
                    {
                        cell.DataType = CellValues.String;
                        cell.CellValue = new CellValue("");
                    }

                    row.AppendChild(cell);
                }

                sheetData.AppendChild(row);
                rowIndex++;
            }
        }

        private void FormatCell(Cell cell, object value, int dataType)
        {
            var stringValue = value?.ToString() ?? "";

            switch (dataType)
            {
                case 1: // VARCHAR
                    cell.DataType = CellValues.String;
                    cell.CellValue = new CellValue(stringValue);
                    break;

                case 2: // INT
                    if (int.TryParse(stringValue, out int intValue))
                    {
                        cell.DataType = CellValues.Number;
                        cell.CellValue = new CellValue(intValue);
                    }
                    else
                    {
                        cell.DataType = CellValues.String;
                        cell.CellValue = new CellValue(stringValue);
                    }
                    break;

                case 3: // DECIMAL
                    if (decimal.TryParse(stringValue, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal decimalValue))
                    {
                        cell.DataType = CellValues.Number;
                        cell.CellValue = new CellValue(decimalValue);
                    }
                    else
                    {
                        cell.DataType = CellValues.String;
                        cell.CellValue = new CellValue(stringValue);
                    }
                    break;

                case 4: // DATETIME
                    if (DateTime.TryParse(stringValue, out DateTime dateValue))
                    {
                        cell.DataType = CellValues.Number;
                        cell.CellValue = new CellValue(dateValue.ToOADate());
                        cell.StyleIndex = 2; // Date format
                    }
                    else
                    {
                        cell.DataType = CellValues.String;
                        cell.CellValue = new CellValue(stringValue);
                    }
                    break;

                default:
                    cell.DataType = CellValues.String;
                    cell.CellValue = new CellValue(stringValue);
                    break;
            }
        }

        // Helper metodlar
        private object GetPropertyValue(object obj, string propertyName)
        {
            try
            {
                if (obj == null) return null;

                var type = obj.GetType();
                var property = type.GetProperty(propertyName);
                var value = property?.GetValue(obj);

                // DisplayOrder için özel kontrol
                if (propertyName == "DisplayOrder" && value != null)
                {
                    if (int.TryParse(value.ToString(), out int intValue))
                        return intValue;
                }

                return value;
            }
            catch
            {
                return propertyName == "DisplayOrder" ? 0 : null;
            }
        }

        private string GetCellReference(int columnIndex, uint rowIndex)
        {
            string columnName = "";
            while (columnIndex > 0)
            {
                columnIndex--;
                columnName = (char)('A' + columnIndex % 26) + columnName;
                columnIndex /= 26;
            }
            return columnName + rowIndex;
        }

        private string SanitizeFileName(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return "Tablo";

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(fileName.Where(c => !invalidChars.Contains(c)).ToArray());
            sanitized = sanitized.Replace(" ", "_");

            return string.IsNullOrEmpty(sanitized) ? "Tablo" : sanitized;
        }

        private string SanitizeSheetName(string sheetName)
        {
            if (string.IsNullOrEmpty(sheetName))
                return "Sheet1";

            // Excel sheet adı kısıtlamaları
            var invalidChars = new char[] { '\\', '/', '*', '[', ']', ':', '?' };
            var sanitized = new string(sheetName.Where(c => !invalidChars.Contains(c)).ToArray());

            // Maksimum 31 karakter
            if (sanitized.Length > 31)
                sanitized = sanitized.Substring(0, 31);

            return string.IsNullOrEmpty(sanitized) ? "Sheet1" : sanitized;
        }

        private string EscapeCsvValue(string value, string delimiter)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            bool needsQuoting = value.Contains(delimiter) || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");

            if (needsQuoting)
            {
                value = value.Replace("\"", "\"\"");
                return $"\"{value}\"";
            }

            return value;
        }

        private void CreateStylesheet(WorkbookPart workbookPart)
        {
            var stylesPart = workbookPart.AddNewPart<WorkbookStylesPart>();
            var stylesheet = new Stylesheet();

            // Fonts
            var fonts = new Fonts() { Count = 2 };
            fonts.Append(new Font()); // Default
            fonts.Append(new Font(new Bold())); // Bold for headers

            // Fills
            var fills = new Fills() { Count = 3 };
            fills.Append(new Fill(new PatternFill() { PatternType = PatternValues.None }));
            fills.Append(new Fill(new PatternFill() { PatternType = PatternValues.Gray125 }));
            fills.Append(new Fill(new PatternFill(new ForegroundColor() { Rgb = "FFE0E0E0" }) { PatternType = PatternValues.Solid }));

            // Borders
            var borders = new Borders() { Count = 1 };
            borders.Append(new Border());

            // Number formats
            var numberFormats = new NumberingFormats() { Count = 1 };
            numberFormats.Append(new NumberingFormat() { NumberFormatId = 164, FormatCode = "dd/mm/yyyy hh:mm:ss" });

            // Cell formats
            var cellFormats = new CellFormats() { Count = 3 };
            cellFormats.Append(new CellFormat()); // Default
            cellFormats.Append(new CellFormat() { FontId = 1, FillId = 2, ApplyFont = true, ApplyFill = true }); // Header
            cellFormats.Append(new CellFormat() { NumberFormatId = 164, ApplyNumberFormat = true }); // Date

            stylesheet.Append(numberFormats);
            stylesheet.Append(fonts);
            stylesheet.Append(fills);
            stylesheet.Append(borders);
            stylesheet.Append(cellFormats);

            stylesPart.Stylesheet = stylesheet;
        }
    }
}