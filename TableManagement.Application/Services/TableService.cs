using AutoMapper;
using DevExtreme.AspNet.Data;
using DevExtreme.AspNet.Data.ResponseModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.IO.Pipelines;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.DTOs.Requests;
using TableManagement.Core.Entities;
using TableManagement.Core.Enums;
using TableManagement.Core.Interfaces;
using TableManagement.Infrastructure.Data;

namespace TableManagement.Application.Services
{




    public class TableService : ITableService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IDataDefinitionService _dataDefinitionService;
        private readonly ILogger<TableService> _logger;
        private readonly ApplicationDbContext _context;
        public TableService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IDataDefinitionService dataDefinitionService,
            ILogger<TableService> logger, ApplicationDbContext context)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _dataDefinitionService = dataDefinitionService;
            _logger = logger;
            _context = context;
        }

        public async Task<IEnumerable<TableListDto>> GetUserTablesForDevExpressAsync(int userId)
        {
            _logger.LogInformation("Getting DevExpress tables for user {UserId}", userId);

            // UnitOfWork kullanarak veriyi al
            var tables = await _unitOfWork.CustomTables.GetUserTablesAsync(userId);

            // Manuel mapping yap
            var result = tables.Select(t => new TableListDto
            {
                Id = t.Id,
                TableName = t.TableName,
                Description = t.Description ?? "",
                CreatedAt = t.CreatedAt,
                UpdatedAt = t.UpdatedAt,
                ColumnCount = t.Columns?.Count() ?? 0,
                FormattedDate = t.CreatedAt.ToString("dd.MM.yyyy HH:mm"),
                StatusBadge = (t.Columns?.Count() ?? 0) <= 3 ? "Basit" :
                             (t.Columns?.Count() ?? 0) <= 6 ? "Orta" : "Karmaşık",
                StatusColor = (t.Columns?.Count() ?? 0) <= 3 ? "orange" :
                             (t.Columns?.Count() ?? 0) <= 6 ? "green" : "blue",
                Columns = t.Columns?.Select(c => new ColumnSummaryDto
                {
                    Id = c.Id,
                    ColumnName = c.ColumnName,
                    DataType = (int)c.DataType,
                    IsRequired = c.IsRequired,
                    DefaultValue = c.DefaultValue ?? "",
                    DataTypeLabel = GetDataTypeLabel((int)c.DataType),
                    DataTypeColor = GetDataTypeColor((int)c.DataType)
                }).ToList() ?? new List<ColumnSummaryDto>()
            }).ToList();

            _logger.LogInformation("Created {Count} TableListDto for user {UserId}", result.Count, userId);
            return result;
        }


        private static string GetDataTypeLabel(int dataType)
        {
            return dataType switch
            {
                1 => "VARCHAR",
                2 => "INT",
                3 => "DECIMAL",
                4 => "DATETIME",
                _ => "UNKNOWN"
            };
        }

        private static string GetDataTypeColor(int dataType)
        {
            return dataType switch
            {
                1 => "blue",
                2 => "green",
                3 => "orange",
                4 => "purple",
                _ => "grey"
            };
        }

        public async Task<TableResponse> CreateTableAsync(CreateTableRequest request, int userId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                // Check if table name exists for user
                var exists = await _unitOfWork.CustomTables.TableNameExistsForUserAsync(request.TableName, userId);
                if (exists)
                {
                    throw new ArgumentException("Bu tablo adı zaten kullanılıyor.");
                }

                // Create metadata table
                var table = new CustomTable
                {
                    TableName = request.TableName,
                    Description = request.Description,
                    UserId = userId
                };

                await _unitOfWork.CustomTables.AddAsync(table);
                await _unitOfWork.SaveChangesAsync();

                // Create columns metadata
                var columns = new List<CustomColumn>();
                foreach (var columnRequest in request.Columns)
                {
                    var column = new CustomColumn
                    {
                        ColumnName = columnRequest.ColumnName,
                        DataType = columnRequest.DataType,
                        IsRequired = columnRequest.IsRequired,
                        DisplayOrder = columnRequest.DisplayOrder,
                        DefaultValue = columnRequest.DefaultValue,
                        CustomTableId = table.Id
                    };

                    await _unitOfWork.Repository<CustomColumn>().AddAsync(column);
                    columns.Add(column);
                }

                await _unitOfWork.SaveChangesAsync();

                // Create actual database table using DDL
                var ddlResult = await _dataDefinitionService.CreateUserTableAsync(table.TableName, columns, userId);
                if (!ddlResult)
                {
                    await _unitOfWork.RollbackTransactionAsync();
                    throw new ApplicationException("Veritabanında tablo oluşturulamadı.");
                }

                await _unitOfWork.CommitTransactionAsync();
                return _mapper.Map<TableResponse>(table);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<IEnumerable<TableResponse>> GetUserTablesAsync(int userId)
        {
            var tables = await _unitOfWork.CustomTables.GetUserTablesAsync(userId);
            return _mapper.Map<IEnumerable<TableResponse>>(tables);
        }

        public async Task<TableResponse> GetTableByIdAsync(int tableId, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
            if (table == null)
                throw new ArgumentException("Tablo bulunamadı.");

            return _mapper.Map<TableResponse>(table);
        }

        public async Task<bool> DeleteTableAsync(int tableId, int userId)
        {
            using var transaction = await _unitOfWork.BeginTransactionAsync();

            try
            {
                var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
                if (table == null)
                    return false;

                // Delete actual database table
                await _dataDefinitionService.DropUserTableAsync(table.TableName, userId);

                // Delete metadata
                await _unitOfWork.CustomTables.DeleteAsync(table);
                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                return true;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync();
                throw;
            }
        }

        public async Task<TableDataResponse> GetTableDataAsync(int tableId, int userId)
        {
            try
            {
                // Tablonun mevcut olup olmadığını kontrol et
                var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
                if (table == null)
                {
                    _logger.LogWarning("Table with ID {TableId} not found for user {UserId}", tableId, userId);
                    throw new ArgumentException("Tablo bulunamadı.");
                }

                _logger.LogInformation("Found table: {TableName} (ID: {TableId}) for user {UserId}", table.TableName, tableId, userId);

                // Tablodan verileri çek
                var data = await _dataDefinitionService.SelectDataFromUserTableAsync(table.TableName, userId);

                _logger.LogInformation("Retrieved {DataCount} rows from table {TableName}", data?.Count ?? 0, table.TableName);

                // Columns'u sırala ve map et
                var orderedColumns = table.Columns?.OrderBy(c => c.DisplayOrder)?.ToList() ?? new List<CustomColumn>();

                // Response'u oluştur
                var response = new TableDataResponse
                {
                    TableId = tableId,
                    TableName = table.TableName,
                    Columns = _mapper.Map<List<ColumnResponse>>(orderedColumns),
                    Data = data ?? new List<Dictionary<string, object>>()
                };

                return response;
            }
            catch (ArgumentException)
            {
                // Zaten loglananları tekrar loglamayın
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting table data for table {TableId} and user {UserId}", tableId, userId);
                throw new ApplicationException("Tablo verileri getirilirken bir hata oluştu.", ex);
            }
        }

   

     

        // New methods for enhanced update system
        public async Task<ColumnUpdateResult> UpdateColumnAsync(int tableId, UpdateColumnRequest request, int userId)
        {
            var result = new ColumnUpdateResult();

            try
            {
                var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
                if (table == null)
                {
                    result.Message = "Tablo bulunamadı";
                    return result;
                }

                var column = table.Columns.FirstOrDefault(c => c.Id == request.ColumnId);
                if (column == null)
                {
                    result.Message = "Kolon bulunamadı";
                    return result;
                }

                // Perform DDL update
                var ddlResult = await _dataDefinitionService.UpdateColumnAsync(table.TableName, column, request, userId);

                if (ddlResult.Success)
                {
                    // Update metadata
                    column.ColumnName = request.ColumnName;
                    column.DataType = request.DataType;
                    column.IsRequired = request.IsRequired;
                    column.DisplayOrder = request.DisplayOrder;
                    column.DefaultValue = request.DefaultValue;
                    column.UpdatedAt = DateTime.UtcNow;

                    await _unitOfWork.SaveChangesAsync();
                }

                result.Success = ddlResult.Success;
                result.Message = ddlResult.Message;
                result.ExecutedQueries = ddlResult.ExecutedQueries ?? new List<string>();
                result.AffectedRows = ddlResult.AffectedRows;
                result.ValidationResult = ddlResult.ValidationResult;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating column {ColumnId} in table {TableId}", request.ColumnId, tableId);
                result.Message = "Kolon güncellenirken hata oluştu: " + ex.Message;
            }

            return result;
        }

        public async Task<ColumnValidationResult> ValidateColumnUpdateAsync(int tableId, UpdateColumnRequest request, int userId)
        {
            var result = new ColumnValidationResult();

            try
            {
                var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
                if (table == null)
                {
                    result.IsValid = false;
                    result.Issues.Add("Tablo bulunamadı");
                    return result;
                }

                var column = table.Columns.FirstOrDefault(c => c.Id == request.ColumnId);
                if (column == null)
                {
                    result.IsValid = false;
                    result.Issues.Add("Kolon bulunamadı");
                    return result;
                }

                // Validate with DDL service
                result = await _dataDefinitionService.ValidateColumnDataTypeChangeAsync(
                    table.TableName, column.ColumnName, column.DataType, request.DataType, userId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating column update for table {TableId}", tableId);
                result.IsValid = false;
                result.Issues.Add("Validasyon sırasında hata oluştu: " + ex.Message);
            }

            return result;
        }

        public async Task<TableValidationResult> ValidateTableUpdateAsync(int tableId, ValidateTableUpdateRequest request, int userId)
        {
            var result = new TableValidationResult();

            try
            {
                var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
                if (table == null)
                {
                    result.IsValid = false;
                    result.Issues.Add("Tablo bulunamadı");
                    return result;
                }

                // Check table name changes
                if (table.TableName != request.TableName)
                {
                    result.HasStructuralChanges = true;
                    var nameExists = await _unitOfWork.CustomTables.TableNameExistsForUserAsync(request.TableName, userId);
                    if (nameExists)
                    {
                        result.IsValid = false;
                        result.Issues.Add("Bu tablo adı zaten kullanılıyor");
                    }
                }

                // Validate columns if provided
                if (request.Columns != null && request.Columns.Any())
                {
                    await ValidateColumnChangesAsync(table, request.Columns, result, userId);
                }

                // Get affected row count
                result.AffectedRowCount = await _dataDefinitionService.GetTableRowCountAsync(table.TableName, userId);

                // Estimate backup size if structural changes exist
                if (result.HasStructuralChanges && result.AffectedRowCount > 0)
                {
                    result.EstimatedBackupSize = await _dataDefinitionService.EstimateTableSizeAsync(table.TableName, userId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating table update for table {TableId}", tableId);
                result.IsValid = false;
                result.Issues.Add("Validasyon sırasında hata oluştu: " + ex.Message);
            }

            return result;
        }

        private async Task ValidateColumnChangesAsync(CustomTable existingTable, List<UpdateColumnRequest> newColumns, TableValidationResult result, int userId)
        {
            var existingColumns = existingTable.Columns.ToList();

            _logger.LogInformation("🔍 Validating column changes - Existing: {ExistingCount}, New: {NewCount}",
                existingColumns.Count, newColumns.Count);

            // 🔥 KOLON SİLME KONTROLÜ - İyileştirilmiş
            var deletedColumns = existingColumns.Where(ec => !newColumns.Any(nc => nc.ColumnId == ec.Id)).ToList();

            _logger.LogInformation("📋 Found {DeletedCount} columns to delete: {DeletedColumns}",
                deletedColumns.Count, string.Join(", ", deletedColumns.Select(c => c.ColumnName)));

            foreach (var deletedColumn in deletedColumns)
            {
                result.HasStructuralChanges = true;

                _logger.LogInformation("🔍 Checking if column {ColumnName} has data...", deletedColumn.ColumnName);

                // 🔥 AKILLI KONTROL: Bu kolonda gerçekten veri var mı?
                var columnHasData = await _dataDefinitionService.ColumnHasDataAsync(existingTable.TableName, deletedColumn.ColumnName, userId);

                if (columnHasData)
                {
                    result.HasDataCompatibilityIssues = true;
                    result.RequiresForceUpdate = true;
                    result.ColumnIssues[$"{deletedColumn.ColumnName}"] = new List<string> { "⚠️ Kolonun silinmesi veri kaybına neden olacak" };
                    _logger.LogWarning("⚠️ Column {ColumnName} has data and will cause data loss if deleted", deletedColumn.ColumnName);
                }
                else
                {
                    // ✅ Güvenli silme - veri yok
                    _logger.LogInformation("✅ Column {ColumnName} has no data, deletion is safe", deletedColumn.ColumnName);
                    if (!result.ColumnIssues.ContainsKey(deletedColumn.ColumnName))
                    {
                        result.ColumnIssues[deletedColumn.ColumnName] = new List<string>();
                    }
                    result.ColumnIssues[deletedColumn.ColumnName].Add("✅ Kolon güvenle silinebilir (veri yok)");
                }
            }

            // Tabloda hiç veri yoksa, tüm kolon silme işlemlerini güvenli yap
            var tableRowCount = await _dataDefinitionService.GetTableRowCountAsync(existingTable.TableName, userId);

            _logger.LogInformation("📊 Table {TableName} has {RowCount} rows for validation", existingTable.TableName, tableRowCount);

            if (tableRowCount == 0 && deletedColumns.Any())
            {
                _logger.LogInformation("🆓 Table is empty, allowing all column deletions without force update");
                result.HasDataCompatibilityIssues = false;
                result.RequiresForceUpdate = false;

                // Tüm silinecek kolonları güvenli olarak işaretle
                foreach (var deletedColumn in deletedColumns)
                {
                    result.ColumnIssues[deletedColumn.ColumnName] = new List<string> { "✅ Tablo boş - kolon güvenle silinebilir" };
                }
            }

            // Yeni kolon ekleme kontrolü
            var newColumnRequests = newColumns.Where(nc => nc.ColumnId == null || nc.ColumnId == 0).ToList();
            foreach (var newColumn in newColumnRequests)
            {
                result.HasStructuralChanges = true;

                // 🔥 AKILLI KONTROL: Yeni kolon zorunlu ve default değer yok, ama tabloda veri var mı?
                if (newColumn.IsRequired && string.IsNullOrEmpty(newColumn.DefaultValue))
                {
                    if (tableRowCount > 0)
                    {
                        result.HasDataCompatibilityIssues = true;
                        result.RequiresForceUpdate = true;
                        result.ColumnIssues[newColumn.ColumnName] = new List<string> { "⚠️ Zorunlu kolon mevcut verilerle uyumlu değil" };
                        _logger.LogWarning("⚠️ Required column {ColumnName} without default value will conflict with existing {RowCount} rows",
                            newColumn.ColumnName, tableRowCount);
                    }
                    else
                    {
                        _logger.LogInformation("✅ Required column {ColumnName} is safe to add - no existing data", newColumn.ColumnName);
                    }
                }
            }

            // Kolon değişiklik kontrolü - mevcut kod
            foreach (var modifiedColumn in newColumns.Where(nc => nc.ColumnId.HasValue && nc.ColumnId > 0))
            {
                var existingColumn = existingColumns.FirstOrDefault(ec => ec.Id == modifiedColumn.ColumnId);
                if (existingColumn == null) continue;

                // Veri tipi değişikliği kontrolü
                if (existingColumn.DataType != modifiedColumn.DataType)
                {
                    result.HasStructuralChanges = true;

                    var validationResult = await _dataDefinitionService.ValidateColumnDataTypeChangeAsync(
                        existingTable.TableName, existingColumn.ColumnName, existingColumn.DataType, modifiedColumn.DataType, userId);

                    if (!validationResult.IsValid)
                    {
                        result.IsValid = false;
                        result.ColumnIssues[existingColumn.ColumnName] = validationResult.Issues;
                        _logger.LogError("❌ Data type change validation failed for column {ColumnName}: {Issues}",
                            existingColumn.ColumnName, string.Join(", ", validationResult.Issues));
                    }
                    else if (validationResult.HasDataCompatibilityIssues)
                    {
                        result.HasDataCompatibilityIssues = true;
                        if (validationResult.RequiresForceUpdate)
                        {
                            result.RequiresForceUpdate = true;
                        }
                        result.ColumnIssues[existingColumn.ColumnName] = validationResult.DataIssues;
                        _logger.LogWarning("⚠️ Data type change for column {ColumnName} has compatibility issues: {Issues}",
                            existingColumn.ColumnName, string.Join(", ", validationResult.DataIssues));
                    }
                    else
                    {
                        // 🔥 GÜVENLİ DEĞİŞİKLİK - Kullanıcıyı bilgilendir
                        _logger.LogInformation("✅ Data type change for column {ColumnName} from {OldType} to {NewType} is safe",
                            existingColumn.ColumnName, existingColumn.DataType, modifiedColumn.DataType);

                        if (!result.ColumnIssues.ContainsKey(existingColumn.ColumnName))
                        {
                            result.ColumnIssues[existingColumn.ColumnName] = new List<string>();
                        }

                        var safeChangeMessage = GetSafeChangeMessage(existingColumn.DataType, modifiedColumn.DataType);
                        if (!string.IsNullOrEmpty(safeChangeMessage))
                        {
                            result.ColumnIssues[existingColumn.ColumnName].Add($"✅ {safeChangeMessage}");
                        }
                    }
                }

                // Kolon adı değişiklik kontrolü
                if (existingColumn.ColumnName != modifiedColumn.ColumnName)
                {
                    result.HasStructuralChanges = true;

                    if (!result.ColumnIssues.ContainsKey(existingColumn.ColumnName))
                    {
                        result.ColumnIssues[existingColumn.ColumnName] = new List<string>();
                    }
                    result.ColumnIssues[existingColumn.ColumnName].Add($"ℹ️ Kolon adı '{existingColumn.ColumnName}' → '{modifiedColumn.ColumnName}' olarak değiştirilecek");
                }

                // Zorunluluk değişiklik kontrolü
                if (existingColumn.IsRequired != modifiedColumn.IsRequired)
                {
                    if (modifiedColumn.IsRequired && !existingColumn.IsRequired)
                    {
                        var columnHasNullData = await _dataDefinitionService.ColumnHasNullDataAsync(existingTable.TableName, existingColumn.ColumnName, userId);
                        if (columnHasNullData && string.IsNullOrEmpty(modifiedColumn.DefaultValue))
                        {
                            result.HasDataCompatibilityIssues = true;
                            result.RequiresForceUpdate = true;
                            if (!result.ColumnIssues.ContainsKey(existingColumn.ColumnName))
                            {
                                result.ColumnIssues[existingColumn.ColumnName] = new List<string>();
                            }
                            result.ColumnIssues[existingColumn.ColumnName].Add("⚠️ Kolon zorunlu yapılıyor ama NULL değerler mevcut");
                        }
                    }
                }
            }
        }

        // 🔥 YENİ YARDIMCI METOD: Güvenli değişiklik mesajları
        private string GetSafeChangeMessage(ColumnDataType from, ColumnDataType to)
        {
            return (from, to) switch
            {
                (ColumnDataType.Int, ColumnDataType.Decimal) => "INT'den DECIMAL'e güvenli dönüşüm",
                (ColumnDataType.Int, ColumnDataType.Varchar) => "INT'den VARCHAR'a güvenli dönüşüm",
                (ColumnDataType.Decimal, ColumnDataType.Varchar) => "DECIMAL'den VARCHAR'a güvenli dönüşüm",
                (ColumnDataType.DateTime, ColumnDataType.Varchar) => "DATETIME'dan VARCHAR'a güvenli dönüşüm",
                _ => ""
            };
        }

        public async Task<TableUpdateResult> UpdateTableAsync(int tableId, UpdateTableRequest request, int userId)
        {
            var result = new TableUpdateResult();
            var executedQueries = new List<string>();
            var totalAffectedRows = 0;

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
                if (table == null)
                {
                    result.Message = "Tablo bulunamadı.";
                    return result;
                }

                _logger.LogInformation("🔄 Starting table update for table {TableName} (ID: {TableId})", table.TableName, tableId);

                // Fiziksel tablo ismini bul
                var physicalTableName = await FindPhysicalTableNameAsync(table.TableName, userId);
                if (string.IsNullOrEmpty(physicalTableName))
                {
                    result.Message = "Fiziksel tablo bulunamadı.";
                    return result;
                }

                var existingColumns = table.Columns.ToList();
                var newColumns = request.Columns;

                _logger.LogInformation("📊 Column comparison - Existing: {ExistingCount}, New: {NewCount}",
                    existingColumns.Count, newColumns?.Count ?? 0);

                // 🔥 1. TABLO İSMİ DEĞİŞİKLİĞİ KONTROLÜ VE FİZİKSEL TABLO RENAME İŞLEMİ
                string? newPhysicalTableName = null;
                var originalTableName = table.TableName;

                if (!string.IsNullOrEmpty(request.TableName) && originalTableName != request.TableName)
                {
                    _logger.LogInformation("🏷️ Table name change detected: {OldName} -> {NewName}", originalTableName, request.TableName);

                    // Yeni fiziksel tablo ismini oluştur
                    newPhysicalTableName = _dataDefinitionService.GenerateSecureTableName(request.TableName, userId);

                    _logger.LogInformation("🏷️ Physical table rename: {OldPhysical} -> {NewPhysical}", physicalTableName, newPhysicalTableName);

                    // Fiziksel tabloyu yeniden adlandır
                    var renameResult = await _dataDefinitionService.RenamePhysicalTableAsync(physicalTableName, request.TableName, userId);

                    if (!renameResult)
                    {
                        result.Message = "Fiziksel tablo ismi değiştirilemedi.";
                        await _unitOfWork.RollbackTransactionAsync();
                        return result;
                    }

                    executedQueries.Add($"Fiziksel tablo ismi değiştirildi: {physicalTableName} -> {newPhysicalTableName}");
                    physicalTableName = newPhysicalTableName;
                }

                // 🔥 2. KOLON İŞLEMLERİ
                if (newColumns != null && newColumns.Any())
                {
                    // 🔥 2.1. SİLİNECEK KOLONLARI BUL VE SİL (İkinci metodun mantığı)
                    var columnsToDelete = existingColumns.Where(ec =>
                        !newColumns.Any(nc => nc.ColumnId == ec.Id)).ToList();

                    foreach (var columnToDelete in columnsToDelete)
                    {
                        _logger.LogInformation("🗑️ Deleting column: {ColumnName}", columnToDelete.ColumnName);

                        var deleteResult = await _dataDefinitionService.DropColumnDirectAsync(physicalTableName, columnToDelete.ColumnName);

                        if (deleteResult.Success)
                        {
                            executedQueries.Add($"Column dropped: {columnToDelete.ColumnName}");
                            totalAffectedRows += deleteResult.AffectedRows;

                            // Metadata'dan da sil
                            table.Columns.Remove(columnToDelete);

                            _logger.LogInformation("✅ Column {ColumnName} deleted successfully", columnToDelete.ColumnName);
                        }
                        else
                        {
                            result.Message = $"Kolon silinemedi: {columnToDelete.ColumnName} - {deleteResult.Message}";
                            await _unitOfWork.RollbackTransactionAsync();
                            return result;
                        }
                    }

                    // 🔥 2.2. YENİ KOLONLARI EKLE (İkinci metodun mantığı)
                    var newColumnRequests = newColumns.Where(nc =>
                        nc.ColumnId == null || nc.ColumnId == 0 ||
                        !existingColumns.Any(ec => ec.Id == nc.ColumnId)).ToList();

                    foreach (var newColumn in newColumnRequests)
                    {
                        _logger.LogInformation("➕ Adding new column: {ColumnName}", newColumn.ColumnName);

                        var addResult = await _dataDefinitionService.AddColumnDirectAsync(physicalTableName, newColumn);

                        if (addResult.Success)
                        {
                            executedQueries.AddRange(addResult.ExecutedQueries ?? new List<string>());
                            totalAffectedRows += addResult.AffectedRows;

                            // Metadata'ya da ekle
                            var newColumnEntity = new CustomColumn
                            {
                                ColumnName = newColumn.ColumnName,
                                DataType = newColumn.DataType,
                                IsRequired = newColumn.IsRequired,
                                DisplayOrder = newColumn.DisplayOrder,
                                DefaultValue = newColumn.DefaultValue ?? "",
                                CustomTableId = table.Id,
                                CreatedAt = DateTime.UtcNow,
                                UpdatedAt = DateTime.UtcNow
                            };

                            table.Columns.Add(newColumnEntity);

                            _logger.LogInformation("✅ Column {ColumnName} added successfully", newColumn.ColumnName);
                        }
                        else
                        {
                            result.Message = $"Kolon eklenemedi: {newColumn.ColumnName} - {addResult.Message}";
                            await _unitOfWork.RollbackTransactionAsync();
                            return result;
                        }
                    }

                    // 🔥 2.3. MEVCUT KOLONLARI GÜNCELLE (İlk metodun TAM mantığı - çalışıyor!)
                    foreach (var modifiedColumn in newColumns.Where(c => c.ColumnId.HasValue && c.ColumnId > 0))
                    {
                        var existingColumn = existingColumns.FirstOrDefault(ec => ec.Id == modifiedColumn.ColumnId);
                        if (existingColumn == null) continue;

                        // 🔥 VERİ TİPİ DEĞİŞİKLİĞİ
                        if (existingColumn.DataType != modifiedColumn.DataType)
                        {
                            _logger.LogInformation("🔄 Data type change: {ColumnName} from {OldType} to {NewType}",
                                existingColumn.ColumnName, existingColumn.DataType, modifiedColumn.DataType);

                            var forceUpdate = modifiedColumn.ForceUpdate == true;

                            // FİZİKSEL TABLODA VERİ TİPİNİ DEĞİŞTİR
                            var columnUpdateResult = await _dataDefinitionService.UpdateColumnDataTypeAsync(
                                table.TableName, existingColumn.ColumnName, modifiedColumn.DataType, forceUpdate, userId);

                            if (!columnUpdateResult.Success)
                            {
                                result.Message = $"Kolon {existingColumn.ColumnName} fiziksel güncelleme hatası: {columnUpdateResult.Message}";
                                await _unitOfWork.RollbackTransactionAsync();
                                return result;
                            }

                            executedQueries.AddRange(columnUpdateResult.ExecutedQueries);
                            totalAffectedRows += columnUpdateResult.AffectedRows;

                            // METADATA'YI GÜNCELLE
                            existingColumn.DataType = modifiedColumn.DataType;
                            existingColumn.UpdatedAt = DateTime.UtcNow;

                            _logger.LogInformation("✅ Column {ColumnName} type updated successfully to {NewType}",
                                existingColumn.ColumnName, modifiedColumn.DataType);
                        }

                        // 🔥 KOLON ADI DEĞİŞİKLİĞİ
                        if (existingColumn.ColumnName != modifiedColumn.ColumnName)
                        {
                            _logger.LogInformation("🔄 Column rename: {OldName} -> {NewName}",
                                existingColumn.ColumnName, modifiedColumn.ColumnName);

                            var renameSuccess = await _dataDefinitionService.RenameColumnAsync(
                                table.TableName, existingColumn.ColumnName, modifiedColumn.ColumnName, userId);

                            if (!renameSuccess)
                            {
                                result.Message = $"Kolon adı değiştirilemedi: {existingColumn.ColumnName} -> {modifiedColumn.ColumnName}";
                                await _unitOfWork.RollbackTransactionAsync();
                                return result;
                            }

                            executedQueries.Add($"Column renamed: {existingColumn.ColumnName} -> {modifiedColumn.ColumnName}");
                            existingColumn.ColumnName = modifiedColumn.ColumnName;
                            existingColumn.UpdatedAt = DateTime.UtcNow;

                            _logger.LogInformation("✅ Column renamed successfully: {NewName}", modifiedColumn.ColumnName);
                        }

                        // 🔥 REQUIRED/NULL DEĞİŞİKLİĞİ
                        if (existingColumn.IsRequired != modifiedColumn.IsRequired)
                        {
                            _logger.LogInformation("🔄 Nullability change: {ColumnName} IsRequired: {OldValue} -> {NewValue}",
                                existingColumn.ColumnName, existingColumn.IsRequired, modifiedColumn.IsRequired);

                            var nullabilitySuccess = await _dataDefinitionService.UpdateColumnNullabilityAsync(
                                table.TableName, existingColumn.ColumnName, modifiedColumn.IsRequired, userId);

                            if (!nullabilitySuccess)
                            {
                                result.Message = $"Kolon null durumu güncellenemedi: {existingColumn.ColumnName}";
                                await _unitOfWork.RollbackTransactionAsync();
                                return result;
                            }

                            executedQueries.Add($"Column nullability updated: {existingColumn.ColumnName}");
                            existingColumn.IsRequired = modifiedColumn.IsRequired;
                            existingColumn.UpdatedAt = DateTime.UtcNow;

                            _logger.LogInformation("✅ Column nullability updated: {ColumnName}", existingColumn.ColumnName);
                        }

                        // 🔥 DEFAULT VALUE DEĞİŞİKLİĞİ
                        if (existingColumn.DefaultValue != (modifiedColumn.DefaultValue ?? ""))
                        {
                            _logger.LogInformation("🔄 Default value change: {ColumnName} from '{OldValue}' to '{NewValue}'",
                                existingColumn.ColumnName, existingColumn.DefaultValue, modifiedColumn.DefaultValue);

                            var defaultValueSuccess = await _dataDefinitionService.UpdateColumnDefaultValueAsync(
                                table.TableName, existingColumn.ColumnName, modifiedColumn.DefaultValue ?? "", userId);

                            if (!defaultValueSuccess)
                            {
                                _logger.LogWarning("⚠️ Default value update failed for column: {ColumnName}", existingColumn.ColumnName);
                                // Default value hatası kritik değil, devam et
                            }
                            else
                            {
                                executedQueries.Add($"Column default value updated: {existingColumn.ColumnName}");
                                _logger.LogInformation("✅ Default value updated: {ColumnName}", existingColumn.ColumnName);
                            }

                            existingColumn.DefaultValue = modifiedColumn.DefaultValue ?? "";
                            existingColumn.UpdatedAt = DateTime.UtcNow;
                        }

                        // 🔥 DISPLAY ORDER DEĞİŞİKLİĞİ
                        if (existingColumn.DisplayOrder != modifiedColumn.DisplayOrder)
                        {
                            existingColumn.DisplayOrder = modifiedColumn.DisplayOrder;
                            existingColumn.UpdatedAt = DateTime.UtcNow;
                        }
                    }

                    _logger.LogInformation("🔧 Column operations completed successfully. " +
                        "Added: {AddedCount}, Updated columns processed, Deleted: {DeletedCount}",
                        newColumnRequests.Count, columnsToDelete.Count);
                }

                // 🔥 3. TABLO BİLGİLERİNİ GÜNCELLE (CustomTables tablosunda)
                table.TableName = request.TableName;
                table.Description = request.Description;
                table.UpdatedAt = DateTime.UtcNow;

                await _unitOfWork.SaveChangesAsync();
                await _unitOfWork.CommitTransactionAsync();

                result.Success = true;
                result.Message = "Tablo başarıyla güncellendi";
                result.Table = _mapper.Map<TableResponse>(table);
                result.ExecutedQueries = executedQueries;
                result.AffectedRows = totalAffectedRows;

                _logger.LogInformation("🎉 Table {TableId} updated successfully by user {UserId}. Executed {QueryCount} operations",
                    tableId, userId, executedQueries.Count);

                // İsim değiştirme işlemini logla
                if (newPhysicalTableName != null)
                {
                    _logger.LogInformation("🎉 Physical table renamed successfully: {OldName} -> {NewName}",
                        originalTableName, request.TableName);
                }

                return result;
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "💥 Error updating table {TableId} by user {UserId}", tableId, userId);
                result.Message = "Tablo güncellenirken hata oluştu: " + ex.Message;
                return result;
            }
        }
        private async Task<string?> FindPhysicalTableNameAsync(string logicalTableName, int userId)
        {
            try
            {
                _logger.LogInformation("=== PHYSICAL TABLE SEARCH DEBUG ===");
                _logger.LogInformation("Searching for physical table for logical name: {LogicalTableName}", logicalTableName);
                _logger.LogInformation("User ID: {UserId}", userId);

                // 1. Önce tüm kullanıcı tablolarını listele
                var allUserTables = await _dataDefinitionService.GetAllUserTablesAsync(userId);
                var allTables = await _dataDefinitionService.GetAllTablesDebugAsync();

                _logger.LogInformation("All user tables ({Count}): {Tables}",
                    allUserTables.Count, string.Join(", ", allUserTables));
                _logger.LogInformation("All system tables ({Count}): {Tables}",
                    allTables.Count, string.Join(", ", allTables.Take(10))); // İlk 10'unu göster

                if (!allUserTables.Any())
                {
                    _logger.LogWarning("No tables found for user {UserId}", userId);
                    return null;
                }

                // 2. Olası isimleri oluştur - Daha kapsamlı
                var possibleNames = new List<string>();

                // Standart format
                var standardName = _dataDefinitionService.GenerateSecureTableName(logicalTableName, userId);
                possibleNames.Add(standardName);

                // Boşlukları underscore ile değiştir
                possibleNames.Add($"Table_{userId}_{logicalTableName.Replace(" ", "_")}");

                // Türkçe karakterleri normalize et
                var normalizedName = NormalizeTableName(logicalTableName);
                possibleNames.Add($"Table_{userId}_{normalizedName}");

                // Tüm karakterleri lowercase + normalize
                var lowerNormalized = NormalizeTableName(logicalTableName.ToLower());
                possibleNames.Add($"Table_{userId}_{lowerNormalized}");

                // İlk kelimeyi al (çok kelimeli tablolar için)
                var firstWord = logicalTableName.Split(' ')[0];
                possibleNames.Add($"Table_{userId}_{firstWord}");
                possibleNames.Add($"Table_{userId}_{NormalizeTableName(firstWord)}");

                // Tüm olası isimleri logla
                _logger.LogInformation("Possible names to search: {Names}", string.Join(", ", possibleNames));

                // 3. Exact match ara - Case insensitive
                foreach (var expectedName in possibleNames.Distinct())
                {
                    var match = allUserTables.FirstOrDefault(t =>
                        string.Equals(t, expectedName, StringComparison.OrdinalIgnoreCase));

                    if (match != null)
                    {
                        _logger.LogInformation("Found EXACT match: {TableName} -> {Match}", expectedName, match);
                        return match;
                    }
                }

                // 4. Partial match ara - Daha esnek yaklaşım
                foreach (var userTable in allUserTables)
                {
                    // Tablo adından logical kısmı çıkar
                    if (userTable.StartsWith($"Table_{userId}_", StringComparison.OrdinalIgnoreCase))
                    {
                        var logicalPart = userTable.Substring($"Table_{userId}_".Length);
                        var decodedLogicalPart = logicalPart.Replace("_", " ");

                        _logger.LogDebug("Checking table: {UserTable}, logical part: '{LogicalPart}', decoded: '{DecodedLogicalPart}'",
                            userTable, logicalPart, decodedLogicalPart);

                        // Çeşitli eşleşme kontrolleri
                        if (string.Equals(logicalPart, logicalTableName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(decodedLogicalPart, logicalTableName, StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(logicalPart, NormalizeTableName(logicalTableName), StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(decodedLogicalPart, NormalizeTableName(logicalTableName), StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Found PARTIAL match: {TableName} (logical: {LogicalPart})", userTable, logicalPart);
                            return userTable;
                        }

                        // Contains kontrolü
                        if (logicalTableName.Contains(decodedLogicalPart, StringComparison.OrdinalIgnoreCase) ||
                            decodedLogicalPart.Contains(logicalTableName, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Found CONTAINS match: {TableName} (logical: {LogicalPart})", userTable, logicalPart);
                            return userTable;
                        }
                    }
                }

                // 5. Fuzzy matching - En son çare
                var fuzzyMatches = allUserTables.Where(t =>
                    t.Contains($"Table_{userId}_", StringComparison.OrdinalIgnoreCase) &&
                    (t.ToLower().Contains(logicalTableName.ToLower().Replace(" ", "")) ||
                     t.ToLower().Contains(NormalizeTableName(logicalTableName.ToLower()))))
                    .ToList();

                if (fuzzyMatches.Any())
                {
                    _logger.LogInformation("Found FUZZY matches: {Matches}", string.Join(", ", fuzzyMatches));
                    return fuzzyMatches.First();
                }

                // 6. Son çare: Tek tablo varsa onu al
                if (allUserTables.Count == 1)
                {
                    _logger.LogInformation("Single table found, using: {TableName}", allUserTables[0]);
                    return allUserTables[0];
                }

                _logger.LogError("=== NO PHYSICAL TABLE FOUND ===");
                _logger.LogError("Searched for: {LogicalTableName}", logicalTableName);
                _logger.LogError("Available tables: {Tables}", string.Join(", ", allUserTables));
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding physical table for {LogicalTableName}", logicalTableName);
                return null;
            }
        }

        private string NormalizeTableName(string tableName)
        {
            if (string.IsNullOrEmpty(tableName)) return tableName;

            return tableName
                .Replace(" ", "_")
                .Replace("ş", "s").Replace("Ş", "S")
                .Replace("ç", "c").Replace("Ç", "C")
                .Replace("ı", "i").Replace("İ", "I")
                .Replace("ğ", "g").Replace("Ğ", "G")
                .Replace("ü", "u").Replace("Ü", "U")
                .Replace("ö", "o").Replace("Ö", "O")
                .Replace("â", "a").Replace("Â", "A")
                .Replace("î", "i").Replace("Î", "I")
                .Replace("û", "u").Replace("Û", "U");
        }

        public async Task<TableCreateResult> CreateTableWithValidationAsync(CreateTableRequest request, int userId)
        {
            var result = new TableCreateResult();

            try
            {
                var tableResponse = await CreateTableAsync(request, userId);
                result.Success = true;
                result.Message = "Tablo başarıyla oluşturuldu";
                result.Table = tableResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating table for user {UserId}", userId);
                result.Message = "Tablo oluşturulurken hata oluştu: " + ex.Message;
            }

            return result;
        }

        private async Task<bool> HasStructuralChangesAsync(CustomTable existingTable, UpdateTableRequest request)
        {
            if (existingTable.TableName != request.TableName) return true;

            if (request.Columns == null || !request.Columns.Any()) return false;

            var existingColumnCount = existingTable.Columns.Count;
            var newColumnCount = request.Columns.Count;

            if (existingColumnCount != newColumnCount) return true;

            foreach (var requestColumn in request.Columns.Where(c => c.ColumnId.HasValue))
            {
                var existingColumn = existingTable.Columns.FirstOrDefault(c => c.Id == requestColumn.ColumnId);
                if (existingColumn == null) continue;

                if (existingColumn.ColumnName != requestColumn.ColumnName ||
                    existingColumn.DataType != requestColumn.DataType ||
                    existingColumn.IsRequired != requestColumn.IsRequired)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<ColumnUpdateResult> UpdateTableColumnsAsync(CustomTable table, List<UpdateColumnRequest> newColumns, int userId, string physicalTableName)
        {
            var result = new ColumnUpdateResult { Success = true, ExecutedQueries = new List<string>() };

            try
            {
                var existingColumns = table.Columns.ToList();
                _logger.LogInformation("Updating columns for table {TableName}. Existing: {ExistingCount}, New: {NewCount}",
                    physicalTableName, existingColumns.Count, newColumns.Count);

                // 1. Silinecek kolonları bul ve sil
                var columnsToDelete = existingColumns.Where(ec =>
                    !newColumns.Any(nc => nc.ColumnId == ec.Id)).ToList();

                foreach (var columnToDelete in columnsToDelete)
                {
                    _logger.LogInformation("Deleting column: {ColumnName}", columnToDelete.ColumnName);
                    var deleteResult = await _dataDefinitionService.DropColumnDirectAsync(physicalTableName, columnToDelete.ColumnName);

                    if (deleteResult.Success)
                    {
                        result.ExecutedQueries.Add($"Column dropped: {columnToDelete.ColumnName}");
                        table.Columns.Remove(columnToDelete);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete column {ColumnName}: {Message}",
                            columnToDelete.ColumnName, deleteResult.Message);
                    }
                }

                // 2. Yeni kolonları ekle
                var newColumnRequests = newColumns.Where(nc =>
                    nc.ColumnId == null || nc.ColumnId == 0 ||
                    !existingColumns.Any(ec => ec.Id == nc.ColumnId)).ToList();

                foreach (var newColumn in newColumnRequests)
                {
                    _logger.LogInformation("Adding new column: {ColumnName}", newColumn.ColumnName);
                    var addResult = await _dataDefinitionService.AddColumnDirectAsync(physicalTableName, newColumn);

                    if (addResult.Success)
                    {
                        result.ExecutedQueries.Add($"Column added: {newColumn.ColumnName}");

                        // Metadata'ya da ekle
                        var newColumnEntity = new CustomColumn
                        {
                            ColumnName = newColumn.ColumnName,
                            DataType = newColumn.DataType,
                            IsRequired = newColumn.IsRequired,
                            DisplayOrder = newColumn.DisplayOrder,
                            DefaultValue = newColumn.DefaultValue,
                            CustomTableId = table.Id,
                            CreatedAt = DateTime.UtcNow,
                            UpdatedAt = DateTime.UtcNow
                        };

                        table.Columns.Add(newColumnEntity);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to add column {ColumnName}: {Message}",
                            newColumn.ColumnName, addResult.Message);
                    }
                }

                // 3. Mevcut kolonları güncelle
                var columnsToUpdate = newColumns.Where(nc =>
                    nc.ColumnId.HasValue && nc.ColumnId > 0 &&
                    existingColumns.Any(ec => ec.Id == nc.ColumnId)).ToList();

                foreach (var updateRequest in columnsToUpdate)
                {
                    var existingColumn = existingColumns.First(ec => ec.Id == updateRequest.ColumnId);

                    // Değişiklik var mı kontrol et
                    if (existingColumn.ColumnName != updateRequest.ColumnName ||
                        existingColumn.DataType != updateRequest.DataType ||
                        existingColumn.IsRequired != updateRequest.IsRequired ||
                        existingColumn.DefaultValue != updateRequest.DefaultValue)
                    {
                        _logger.LogInformation("Updating column: {OldName} -> {NewName}",
                            existingColumn.ColumnName, updateRequest.ColumnName);

                        var updateResult = await _dataDefinitionService.UpdateColumnAsync(
                            table.TableName, existingColumn, updateRequest, userId);

                        if (updateResult.Success)
                        {
                            result.ExecutedQueries.AddRange(updateResult.ExecutedQueries ?? new List<string>());

                            // Metadata'yı güncelle
                            existingColumn.ColumnName = updateRequest.ColumnName;
                            existingColumn.DataType = updateRequest.DataType;
                            existingColumn.IsRequired = updateRequest.IsRequired;
                            existingColumn.DisplayOrder = updateRequest.DisplayOrder;
                            existingColumn.DefaultValue = updateRequest.DefaultValue;
                            existingColumn.UpdatedAt = DateTime.UtcNow;
                        }
                        else
                        {
                            _logger.LogWarning("Failed to update column {ColumnName}: {Message}",
                                existingColumn.ColumnName, updateResult.Message);
                        }
                    }
                }

                _logger.LogInformation("Column updates completed. Executed {QueryCount} operations.",
                    result.ExecutedQueries.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating table columns for {TableName}", physicalTableName);
                result.Success = false;
                result.Message = "Kolon güncellemelerinde hata oluştu: " + ex.Message;
            }

            return result;
        }













































        public async Task<bool> AddTableDataAsync(AddTableDataRequest request, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(request.TableId, userId);
            if (table == null)
                throw new ArgumentException("Tablo bulunamadı.");

            // Column name'leri kullanarak veri dictionary'sini oluştur
            var data = new Dictionary<string, object>();

            foreach (var columnValue in request.ColumnValues)
            {
                // Column name'in tabloda mevcut olup olmadığını kontrol et
                var column = table.Columns.FirstOrDefault(c =>
                    c.ColumnName.Equals(columnValue.Key, StringComparison.OrdinalIgnoreCase));

                if (column != null)
                {
                    data[column.ColumnName] = columnValue.Value;
                }
                else
                {
                    _logger.LogWarning("Column {ColumnName} not found in table {TableId}", columnValue.Key, request.TableId);
                }
            }

            if (data.Count == 0)
            {
                throw new ArgumentException("Geçerli column bilgisi bulunamadı.");
            }

            return await _dataDefinitionService.InsertDataToUserTableAsync(table.TableName, data, userId);
        }

        // YENİ: Column name bazlı güncelleme metodu
        public async Task<bool> UpdateTableDataAsync(UpdateTableDataRequest request, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(request.TableId, userId);
            if (table == null)
                throw new ArgumentException("Tablo bulunamadı.");

      
            var data = new Dictionary<string, object>();

            foreach (var columnValue in request.ColumnValues)
            {
                // Column name'in tabloda mevcut olup olmadığını kontrol et
                var column = table.Columns.FirstOrDefault(c =>
                    c.ColumnName.Equals(columnValue.Key, StringComparison.OrdinalIgnoreCase));

                if (column != null)
                {
                    data[column.ColumnName] = columnValue.Value;
                }
                else
                {
                    _logger.LogWarning("Column {ColumnName} not found in table {TableId}", columnValue.Key, request.TableId);
                }
            }

            if (data.Count == 0)
            {
                throw new ArgumentException("Geçerli column bilgisi bulunamadı.");
            }

            var whereClause = $"Id = {request.RowId}";
            return await _dataDefinitionService.UpdateDataInUserTableAsync(table.TableName, data, whereClause, userId);
        }



        // Backward compatibility için ID bazlı veri ekleme
        public async Task<bool> AddTableDataByIdAsync(AddTableDataByIdRequest request, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(request.TableId, userId);
            if (table == null)
                throw new ArgumentException("Tablo bulunamadı.");

            // Convert column values to proper format
            var data = new Dictionary<string, object>();
            foreach (var column in table.Columns)
            {
                if (request.ColumnValues.ContainsKey(column.Id))
                {
                    data[column.ColumnName] = request.ColumnValues[column.Id];
                }
            }

            return await _dataDefinitionService.InsertDataToUserTableAsync(table.TableName, data, userId);
        }

        // Backward compatibility için ID bazlı güncelleme metodu
        public async Task<bool> UpdateTableDataByIdAsync(int tableId, int rowIdentifier, Dictionary<int, string> values, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
            if (table == null)
                throw new ArgumentException("Tablo bulunamadı.");

            // Convert column values to proper format
            var data = new Dictionary<string, object>();
            foreach (var column in table.Columns)
            {
                if (values.ContainsKey(column.Id))
                {
                    data[column.ColumnName] = values[column.Id];
                }
            }

            var whereClause = $"RowIdentifier = {rowIdentifier}";
            return await _dataDefinitionService.UpdateDataInUserTableAsync(table.TableName, data, whereClause, userId);
        }

        
        public async Task<bool> DeleteTableDataAsync(int tableId, int rowIdentifier, int userId)
        {
            var table = await _unitOfWork.CustomTables.GetUserTableByIdAsync(tableId, userId);
            if (table == null)
                throw new ArgumentException("Tablo bulunamadı.");

            var whereClause = $"Id = {rowIdentifier}";
            return await _dataDefinitionService.DeleteDataFromUserTableAsync(table.TableName, whereClause, userId);
        }



    }
}