using AutoMapper;
using TableManagement.Application.DTOs.Requests;
using TableManagement.Application.DTOs.Responses;
using TableManagement.Core.DTOs.Requests;
using TableManagement.Core.Entities;
using TableManagement.Core.Interfaces;
using TableManagement.Core.Enums;
using Microsoft.Extensions.Logging;

namespace TableManagement.Application.Services
{




    public class TableService : ITableService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;
        private readonly IDataDefinitionService _dataDefinitionService;
        private readonly ILogger<TableService> _logger;

        public TableService(
            IUnitOfWork unitOfWork,
            IMapper mapper,
            IDataDefinitionService dataDefinitionService,
            ILogger<TableService> logger)
        {
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            _dataDefinitionService = dataDefinitionService;
            _logger = logger;
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

            // Check for column deletions
            var deletedColumns = existingColumns.Where(ec => !newColumns.Any(nc => nc.ColumnId == ec.Id)).ToList();
            foreach (var deletedColumn in deletedColumns)
            {
                result.HasStructuralChanges = true;
                var hasData = await _dataDefinitionService.ColumnHasDataAsync(existingTable.TableName, deletedColumn.ColumnName, userId);
                if (hasData)
                {
                    result.HasDataCompatibilityIssues = true;
                    result.RequiresForceUpdate = true;
                    result.ColumnIssues[$"{deletedColumn.ColumnName}"] = new List<string> { "Kolonun silinmesi veri kaybına neden olacak" };
                }
            }

            // Check for new columns
            var newColumnRequests = newColumns.Where(nc => nc.ColumnId == null || nc.ColumnId == 0).ToList();
            foreach (var newColumn in newColumnRequests)
            {
                result.HasStructuralChanges = true;

                if (newColumn.IsRequired && string.IsNullOrEmpty(newColumn.DefaultValue))
                {
                    var hasExistingData = await _dataDefinitionService.GetTableRowCountAsync(existingTable.TableName, userId) > 0;
                    if (hasExistingData)
                    {
                        result.HasDataCompatibilityIssues = true;
                        result.RequiresForceUpdate = true;
                        result.ColumnIssues[newColumn.ColumnName] = new List<string> { "Zorunlu kolon mevcut verilerle uyumlu değil" };
                    }
                }
            }

            // Check for column modifications
            foreach (var modifiedColumn in newColumns.Where(nc => nc.ColumnId.HasValue && nc.ColumnId > 0))
            {
                var existingColumn = existingColumns.FirstOrDefault(ec => ec.Id == modifiedColumn.ColumnId);
                if (existingColumn == null) continue;

                if (existingColumn.DataType != modifiedColumn.DataType)
                {
                    result.HasStructuralChanges = true;

                    var validationResult = await _dataDefinitionService.ValidateColumnDataTypeChangeAsync(
                        existingTable.TableName, existingColumn.ColumnName, existingColumn.DataType, modifiedColumn.DataType, userId);

                    if (!validationResult.IsValid)
                    {
                        result.IsValid = false;
                        result.ColumnIssues[existingColumn.ColumnName] = validationResult.Issues;
                    }
                    else if (validationResult.HasDataCompatibilityIssues)
                    {
                        result.HasDataCompatibilityIssues = true;
                        if (validationResult.RequiresForceUpdate)
                        {
                            result.RequiresForceUpdate = true;
                        }
                        result.ColumnIssues[existingColumn.ColumnName] = validationResult.DataIssues;
                    }
                }

                // Check required constraint changes
                if (!existingColumn.IsRequired && modifiedColumn.IsRequired)
                {
                    var hasNullData = await _dataDefinitionService.ColumnHasNullDataAsync(existingTable.TableName, existingColumn.ColumnName, userId);
                    if (hasNullData)
                    {
                        result.HasDataCompatibilityIssues = true;
                        result.RequiresForceUpdate = true;
                        if (!result.ColumnIssues.ContainsKey(existingColumn.ColumnName))
                            result.ColumnIssues[existingColumn.ColumnName] = new List<string>();
                        result.ColumnIssues[existingColumn.ColumnName].Add("Kolonun zorunlu yapılması NULL verilerle uyumlu değil");
                    }
                }
            }
        }


        public async Task<TableUpdateResult> UpdateTableAsync(int tableId, UpdateTableRequest request, int userId)
        {
            var result = new TableUpdateResult();
            var executedQueries = new List<string>();
            var totalAffectedRows = 0;

            try
            {
                await _unitOfWork.BeginTransactionAsync();

                var table = await _unitOfWork.CustomTables.GetUserTableWithColumnsAsync(tableId, userId);
                if (table == null)
                {
                    result.Message = "Tablo bulunamadı";
                    return result;
                }

                var originalTableName = table.TableName;
                _logger.LogInformation("Starting table update: ID={TableId}, Original={OriginalName}, New={NewName}",
                    tableId, originalTableName, request.TableName);

                // 1. FIND ACTUAL PHYSICAL TABLE
                var physicalTableName = await FindPhysicalTableNameAsync(originalTableName, userId);
                if (string.IsNullOrEmpty(physicalTableName))
                {
                    result.Message = $"Fiziksel tablo bulunamadı. Aradığımız: {originalTableName}";
                    return result;
                }

                _logger.LogInformation("Found physical table: {PhysicalTableName}", physicalTableName);

                // 2. RENAME PHYSICAL TABLE IF NAME CHANGED
                if (originalTableName != request.TableName)
                {
                    var newPhysicalTableName = _dataDefinitionService.GenerateSecureTableName(request.TableName, userId);

                    _logger.LogInformation("Renaming physical table: {OldName} -> {NewName}",
                        physicalTableName, newPhysicalTableName);

                    var renameResult = await _dataDefinitionService.RenamePhysicalTableDirectAsync(
                        physicalTableName, newPhysicalTableName);

                    if (!renameResult)
                    {
                        result.Message = $"Fiziksel tablo yeniden adlandırılamadı: {physicalTableName} -> {newPhysicalTableName}";
                        return result;
                    }

                    executedQueries.Add($"Physical table renamed: {physicalTableName} -> {newPhysicalTableName}");
                    physicalTableName = newPhysicalTableName; // Update reference
                }

                // 3. UPDATE COLUMN STRUCTURE
                if (request.Columns != null && request.Columns.Any())
                {
                    var columnUpdateResult = await UpdateTableColumnsAsync(table, request.Columns, userId, physicalTableName);
                    executedQueries.AddRange(columnUpdateResult.ExecutedQueries);
                    totalAffectedRows += columnUpdateResult.AffectedRows;

                    if (!columnUpdateResult.Success)
                    {
                        result.Message = "Kolon güncellemesinde hata: " + columnUpdateResult.Message;
                        return result;
                    }
                }

                // 4. UPDATE METADATA
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

                _logger.LogInformation("Table {TableId} updated successfully by user {UserId}", tableId, userId);
            }
            catch (Exception ex)
            {
                await _unitOfWork.RollbackTransactionAsync();
                _logger.LogError(ex, "Error updating table {TableId} by user {UserId}", tableId, userId);
                result.Message = "Tablo güncellenirken hata oluştu: " + ex.Message;
            }

            return result;
        }

        // Fiziksel tablo adını bulan gelişmiş metod
        private async Task<string?> FindPhysicalTableNameAsync(string logicalTableName, int userId)
        {
            try
            {
                _logger.LogInformation("Searching for physical table for logical name: {LogicalTableName}", logicalTableName);

                // 1. Önce tüm kullanıcı tablolarını listele
                var allUserTables = await _dataDefinitionService.GetAllUserTablesAsync(userId);
                var allTables = await _dataDefinitionService.GetAllTablesDebugAsync();

                _logger.LogInformation("All user tables: {Tables}", string.Join(", ", allUserTables));
                _logger.LogInformation("All system tables: {Tables}", string.Join(", ", allTables));

                if (!allUserTables.Any())
                {
                    _logger.LogWarning("No tables found for user {UserId}", userId);
                    return null;
                }

                // 2. Olası isimleri oluştur
                var possibleNames = new List<string>
        {
            // Standart format
            _dataDefinitionService.GenerateSecureTableName(logicalTableName, userId),
            
            // Space'leri underscore ile değiştir
            $"Table_{userId}_{logicalTableName.Replace(" ", "_")}",
            
            // Türkçe karakterleri düzelt
            $"Table_{userId}_{logicalTableName.Replace("ş", "s").Replace("ç", "c").Replace("ı", "i").Replace("ğ", "g").Replace("ü", "u").Replace("ö", "o").Replace(" ", "_")}",
            
            // Kısaltılmış versiyonlar
            $"Table_{userId}_{logicalTableName.Split(' ')[0]}",
        };

                // 3. Exact match ara
                foreach (var expectedName in possibleNames)
                {
                    if (allUserTables.Contains(expectedName, StringComparer.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Found exact match: {TableName}", expectedName);
                        return expectedName;
                    }
                }

                // 4. Partial match ara
                foreach (var userTable in allUserTables)
                {
                    // Tablo adından logical kısmı çıkar
                    var logicalPart = userTable.Replace($"Table_{userId}_", "").Replace("_", " ");

                    if (string.Equals(logicalPart, logicalTableName, StringComparison.OrdinalIgnoreCase) ||
                        logicalTableName.Contains(logicalPart, StringComparison.OrdinalIgnoreCase) ||
                        logicalPart.Contains(logicalTableName, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation("Found partial match: {TableName} (logical: {LogicalPart})", userTable, logicalPart);
                        return userTable;
                    }
                }

                // 5. Son çare: Tek tablo varsa onu al
                if (allUserTables.Count == 1)
                {
                    _logger.LogInformation("Single table found, using: {TableName}", allUserTables[0]);
                    return allUserTables[0];
                }

                _logger.LogWarning("No physical table found for logical name: {LogicalTableName}", logicalTableName);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error finding physical table for {LogicalTableName}", logicalTableName);
                return null;
            }
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
                    table.TableName, existingColumns.Count, newColumns.Count);

                // 1. COLUMN MAPPING - Hangi kolonların silineceği, ekleneceği, güncelleneceği
                var columnsToDelete = existingColumns.Where(ec =>
                    !newColumns.Any(nc => nc.ColumnName.Equals(ec.ColumnName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var columnsToAdd = newColumns.Where(nc =>
                    !existingColumns.Any(ec => ec.ColumnName.Equals(nc.ColumnName, StringComparison.OrdinalIgnoreCase)))
                    .ToList();

                var columnsToUpdate = newColumns.Where(nc =>
                    existingColumns.Any(ec => ec.ColumnName.Equals(nc.ColumnName, StringComparison.OrdinalIgnoreCase) &&
                    (ec.DataType != nc.DataType || ec.IsRequired != nc.IsRequired || ec.DefaultValue != nc.DefaultValue)))
                    .ToList();

                _logger.LogInformation("Column changes - Delete: {DeleteCount}, Add: {AddCount}, Update: {UpdateCount}",
                    columnsToDelete.Count, columnsToAdd.Count, columnsToUpdate.Count);

                // 2. DELETE COLUMNS
                foreach (var column in columnsToDelete)
                {
                    _logger.LogInformation("Deleting column: {ColumnName}", column.ColumnName);
                    var deleteResult = await _dataDefinitionService.DropColumnDirectAsync(physicalTableName, column.ColumnName);
                    if (deleteResult.Success)
                    {
                        result.ExecutedQueries.Add($"Dropped column: {column.ColumnName}");
                        table.Columns.Remove(column);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to delete column {ColumnName}: {Message}", column.ColumnName, deleteResult.Message);
                    }
                }

                // 3. ADD NEW COLUMNS
                foreach (var columnRequest in columnsToAdd)
                {
                    _logger.LogInformation("Adding column: {ColumnName} ({DataType})", columnRequest.ColumnName, columnRequest.DataType);
                    var addResult = await _dataDefinitionService.AddColumnDirectAsync(physicalTableName, columnRequest);
                    if (addResult.Success)
                    {
                        result.ExecutedQueries.Add($"Added column: {columnRequest.ColumnName}");

                        var newColumn = new CustomColumn
                        {
                            ColumnName = columnRequest.ColumnName,
                            DataType = columnRequest.DataType,
                            IsRequired = columnRequest.IsRequired,
                            DisplayOrder = columnRequest.DisplayOrder,
                            DefaultValue = columnRequest.DefaultValue,
                            CreatedAt = DateTime.UtcNow
                        };
                        table.Columns.Add(newColumn);
                    }
                    else
                    {
                        _logger.LogWarning("Failed to add column {ColumnName}: {Message}", columnRequest.ColumnName, addResult.Message);
                    }
                }

                // 4. UPDATE EXISTING COLUMNS
                foreach (var columnRequest in columnsToUpdate)
                {
                    var existingColumn = existingColumns.FirstOrDefault(c =>
                        c.ColumnName.Equals(columnRequest.ColumnName, StringComparison.OrdinalIgnoreCase));

                    if (existingColumn != null)
                    {
                        _logger.LogInformation("Updating column: {ColumnName}", columnRequest.ColumnName);

                        // Update EF entity
                        existingColumn.DataType = columnRequest.DataType;
                        existingColumn.IsRequired = columnRequest.IsRequired;
                        existingColumn.DisplayOrder = columnRequest.DisplayOrder;
                        existingColumn.DefaultValue = columnRequest.DefaultValue;
                        existingColumn.UpdatedAt = DateTime.UtcNow;

                        result.ExecutedQueries.Add($"Updated column metadata: {columnRequest.ColumnName}");
                    }
                }

                _logger.LogInformation("Column update completed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating table columns for table {TableName}", table.TableName);
                result.Success = false;
                result.Message = "Kolon güncellemesinde hata: " + ex.Message;
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